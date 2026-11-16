using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FlowerFlow.Application.Catalog;
using FlowerFlow.Application.Identity;
using FlowerFlow.Infrastructure.Firebase;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace FlowerFlow.Foundation.Tests;

public sealed class FlowerPricesContractTests
{
    [Fact]
    public async Task Both_HTTP_routes_match_JavaScript_responses_authorization_and_staged_writes()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "flower.com/backend/catalog/flower-prices.js"))) root = root.Parent;
        var start = new ProcessStartInfo("node")
        {
            RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
        };
        start.ArgumentList.Add(Path.Combine(root!.FullName, "aspnet/tests/FlowerFlow.Foundation.Tests/flower-prices-oracle.cjs"));
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch { process.Kill(true); throw; }
        Assert.True(process.ExitCode == 0, await errors);
        using var scenarios = JsonDocument.Parse(await output);
        Assert.Equal(46, scenarios.RootElement.GetArrayLength());
        foreach (var scenario in scenarios.RootElement.EnumerateArray())
        foreach (var route in new[] { "/api/flower-prices?", "/api/products?route=flower-prices&" })
        {
            var backend = new Backend(scenario);
            await using var host = new Host(backend);
            using var client = host.CreateClient();
            var query = new List<string>();
            if (scenario.TryGetProperty("query", out var values))
                foreach (var pair in values.EnumerateObject())
                    foreach (var value in pair.Value.ValueKind == JsonValueKind.Array ? pair.Value.EnumerateArray().ToArray() : new[] { pair.Value })
                        query.Add(Uri.EscapeDataString(pair.Name) + "=" + Uri.EscapeDataString(value.GetString()!));
            using var request = new HttpRequestMessage(new HttpMethod(scenario.TryGetProperty("method", out var method) ? method.GetString()! : "GET"), route + string.Join("&", query));
            var authorization = scenario.TryGetProperty("authorization", out var auth) ? auth.GetString()! : "Bearer owner";
            if (authorization.Length > 0) request.Headers.TryAddWithoutValidation("Authorization", authorization);
            if (scenario.TryGetProperty("body", out var body)) request.Content = new StringContent(body.GetRawText(), Encoding.UTF8, "application/json");
            var response = await client.SendAsync(request);
            var actual = JsonSerializer.SerializeToNode(new
            {
                status = (int)response.StatusCode, body = JsonNode.Parse(await response.Content.ReadAsStringAsync()),
                cache = response.Headers.NonValidated.TryGetValues("Cache-Control", out var cache) ? string.Join(", ", cache) : null,
                allow = response.Content.Headers.TryGetValues("Allow", out var allow) ? string.Join(", ", allow) : null,
                roleReads = backend.RoleReads, tokenReads = backend.TokenReads, writes = backend.Writes
            });
            var expected = JsonNode.Parse(scenario.GetProperty("expected").GetRawText());
            Assert.True(JsonNode.DeepEquals(expected, actual), $"{scenario.GetProperty("name")} via {route}: expected {expected}; actual {actual}");
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Malformed_JSON_is_sanitized_and_does_not_bypass_authentication(bool authenticated)
    {
        using var input = JsonDocument.Parse("{}");
        var backend = new Backend(input.RootElement);
        await using var host = new Host(backend);
        using var client = host.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Patch, "/api/flower-prices")
        { Content = new StringContent("{private-input-invalid", Encoding.UTF8, "application/json") };
        if (authenticated) request.Headers.TryAddWithoutValidation("Authorization", "Bearer owner");
        var response = await client.SendAsync(request);
        Assert.Equal(authenticated ? 400 : 401, (int)response.StatusCode);
        var responseText = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("private-input-invalid", responseText);
        Assert.Equal(authenticated ? "invalid-argument" : "unauthenticated", JsonNode.Parse(responseText)!["code"]!.GetValue<string>());
        Assert.Empty(backend.Writes);
    }

    internal sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.Parse("2026-11-16T18:30:00.123Z", CultureInfo.InvariantCulture);
    }

    internal sealed class Host(Backend backend, IFlowerPriceStore? store = null, IAdminRoleReader? roles = null) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:FlowerFlow"] = "Server=127.0.0.1,1;Database=DoNotUse;Connect Timeout=1;User Id=test;Password=test;Encrypt=False"
            }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IFirebaseTokenVerifier>(); services.AddSingleton<IFirebaseTokenVerifier>(backend);
                services.RemoveAll<IAdminRoleReader>(); services.AddSingleton<IAdminRoleReader>(roles ?? backend);
                services.RemoveAll<IFlowerPriceStore>(); services.AddSingleton<IFlowerPriceStore>(store ?? backend);
                services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider, FixedTime>();
            });
        }
    }

    internal sealed class Backend(JsonElement input) : IFirebaseTokenVerifier, IAdminRoleReader, IFlowerPriceStore, IFlowerPriceTransaction
    {
        private static readonly JsonElement DefaultToken = JsonSerializer.SerializeToElement(new
            { uid = "owner-a", admin = true, role = "owner", email = "owner@example.test" });
        public int RoleReads { get; private set; }
        public int TokenReads { get; private set; }
        public JsonArray Writes { get; } = [];
        private string transactionId = "";
        private bool Flag(string name) => input.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;
        public bool IsConfigured => !Flag("authConfigError");
        public void EnsureConfigured() { if (Flag("storeConfigError")) throw new FirebaseConfigurationException(); }
        public Task<RequestIdentity> VerifyAsync(string token, CancellationToken cancellationToken)
        {
            TokenReads++;
            if (Flag("tokenError")) throw new InvalidFirebaseTokenException();
            var actor = input.TryGetProperty("token", out var provided) ? provided : DefaultToken;
            return Task.FromResult(new RequestIdentity(actor.GetProperty("uid").GetString()!, actor.EnumerateObject()
                .Where(pair => pair.Name != "uid").ToDictionary(pair => pair.Name, pair => pair.Value.ValueKind switch
                {
                    JsonValueKind.True => (object)true, JsonValueKind.False => false, JsonValueKind.String => pair.Value.GetString()!, _ => pair.Value.Clone()
                })));
        }
        public Task<AdminRole?> ReadAsync(string uid, CancellationToken cancellationToken)
        {
            Assert.Equal("owner-a", uid); RoleReads++;
            if (Flag("roleError")) throw new Exception("provider-private");
            return Task.FromResult<AdminRole?>(input.TryGetProperty("roleExists", out var exists) && !exists.GetBoolean() ? null
                : new(input.TryGetProperty("documentRole", out var role) ? role.GetString() : "owner"));
        }
        private IEnumerable<ProductDocument> Documents(string field)
        {
            if (Flag("readError")) throw new Exception("provider-private");
            return input.GetProperty(field).EnumerateArray().Select(doc => new ProductDocument(doc.GetProperty("id").GetString()!,
                (JsonObject)Revive(JsonNode.Parse(doc.GetProperty("fields").GetRawText()))!));
        }
        private static JsonNode? Revive(JsonNode? node)
        {
            if (node is JsonObject fields)
            {
                if (fields["$timestamp"] is { } timestamp) return JsonValue.Create(timestamp.GetValue<string>());
                return new JsonObject(fields.Select(pair => KeyValuePair.Create(pair.Key, Revive(pair.Value))));
            }
            return node?.DeepClone();
        }
        public Task<IReadOnlyList<ProductDocument>> ListFlowersAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ProductDocument>>(Documents("seed").Where(doc => doc.Fields["saleType"]?.GetValue<string>() == "flower").ToArray());
        public Task<ProductDocument?> ReadProductAsync(string id, CancellationToken cancellationToken) => Task.FromResult(Documents("seed").FirstOrDefault(doc => doc.Id == id));
        public Task<IReadOnlyList<ProductDocument>> ReadHistoryAsync(string id, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ProductDocument>>(Documents("history").Where(doc => doc.Fields["flowerId"]?.GetValue<string>() == id).ToArray());
        public async Task<FlowerPriceResult> RunTransactionAsync(string id, Func<IFlowerPriceTransaction, Task<FlowerPriceResult>> operation, CancellationToken cancellationToken)
        {
            if (Flag("transactionError")) throw new Exception("provider-private");
            transactionId = id;
            return await operation(this);
        }
        public Task<ProductDocument?> ReadProductAsync(CancellationToken cancellationToken) => ReadProductAsync(transactionId, cancellationToken);
        public void StageChange(FlowerPriceChange change)
        {
            Assert.Equal(transactionId, change.FlowerId);
            var email = FlowerPriceRules.Email(change.Actor);
            void Add(string collection, string id, string operation, JsonObject data) => Writes.Add(new JsonObject
                { ["collection"] = collection, ["id"] = id, ["operation"] = operation, ["data"] = data });
            Add("products", change.FlowerId, "update", new JsonObject
            {
                ["price"] = change.NewPrice, ["priceUpdatedAt"] = change.LocalDate,
                ["priceChangedAt"] = "$serverTimestamp", ["priceUpdatedBy"] = change.Actor.Uid
            });
            Add("priceHistory", "history-entry", "create", new JsonObject
            {
                ["flowerId"] = change.FlowerId, ["oldPrice"] = change.OldPrice, ["newPrice"] = change.NewPrice,
                ["updatedAt"] = "$serverTimestamp", ["updatedBy"] = change.Actor.Uid, ["updatedByEmail"] = email
            });
            Add("adminAudit", "audit-entry", "create", new JsonObject
            {
                ["actorUid"] = change.Actor.Uid, ["actorEmail"] = email,
                ["action"] = "flower-price.update", ["resourceType"] = "flower", ["resourceId"] = change.FlowerId,
                ["details"] = new JsonObject { ["oldPrice"] = change.OldPrice, ["newPrice"] = change.NewPrice }, ["createdAt"] = "$serverTimestamp"
            });
        }
    }
}
