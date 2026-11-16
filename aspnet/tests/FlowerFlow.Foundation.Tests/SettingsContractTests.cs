using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FlowerFlow.Application.Catalog;
using FlowerFlow.Application.Identity;
using FlowerFlow.Application.ShopSettings;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace FlowerFlow.Foundation.Tests;

public sealed class SettingsContractTests
{
    [Fact]
    public async Task Settings_HTTP_matches_JavaScript_validation_authorization_responses_and_atomic_write_intent()
    {
        var start = new ProcessStartInfo("node") { RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 };
        start.ArgumentList.Add(Path.Combine(ImageContractTests.Root(), "aspnet/tests/FlowerFlow.Foundation.Tests/settings-oracle.cjs"));
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try { await process.WaitForExitAsync(timeout.Token); } catch { process.Kill(true); throw; }
        Assert.True(process.ExitCode == 0, await errors);
        using var scenarios = JsonDocument.Parse(await output);
        Assert.Equal(47, scenarios.RootElement.GetArrayLength());
        foreach (var input in scenarios.RootElement.EnumerateArray())
        {
            var wrapping = input.GetProperty("kind").GetString() == "wrapping";
            foreach (var route in wrapping ? new[] { "/api/bouquet-wrapping?", "/api/delivery-zones?route=bouquet-wrapping&" } : new[] { "/api/delivery-zones?" })
            {
                var backend = new Backend(input);
                await using var host = new Host(backend);
                using var client = host.CreateClient();
                var query = new List<string>();
                if (input.TryGetProperty("query", out var values))
                    foreach (var pair in values.EnumerateObject())
                        foreach (var value in pair.Value.ValueKind == JsonValueKind.Array ? pair.Value.EnumerateArray().ToArray() : new[] { pair.Value })
                            query.Add(Uri.EscapeDataString(pair.Name) + "=" + Uri.EscapeDataString(value.GetString()!));
                using var request = new HttpRequestMessage(new HttpMethod(input.TryGetProperty("method", out var method) ? method.GetString()! : "GET"), route + string.Join("&", query));
                var authorization = input.TryGetProperty("authorization", out var auth) ? auth.GetString()! : "Bearer admin";
                if (authorization.Length > 0) request.Headers.TryAddWithoutValidation("Authorization", authorization);
                if (input.TryGetProperty("body", out var body)) request.Content = new StringContent(body.GetRawText(), Encoding.UTF8, "application/json");
                var response = await client.SendAsync(request);
                var actual = JsonSerializer.SerializeToNode(new
                {
                    status = (int)response.StatusCode, body = JsonNode.Parse(await response.Content.ReadAsStringAsync()),
                    cache = response.Headers.NonValidated.TryGetValues("Cache-Control", out var cache) ? string.Join(", ", cache) : null,
                    allow = response.Content.Headers.TryGetValues("Allow", out var allow) ? string.Join(", ", allow) : null,
                    tokenReads = backend.TokenReads, roleReads = backend.RoleReads, metadataReads = backend.MetadataReads, writes = backend.Writes
                });
                var expected = JsonNode.Parse(input.GetProperty("expected").GetRawText());
                Assert.True(JsonNode.DeepEquals(expected, actual), $"{input.GetProperty("name")} via {route}: expected {expected}; actual {actual}");
            }
        }
    }

    internal sealed class Host(Backend backend, ISettingsStore? store = null, IAdminRoleReader? roles = null) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            { ["ConnectionStrings:FlowerFlow"] = "Server=127.0.0.1,1;Database=DoNotUse;Connect Timeout=1;User Id=test;Password=test;Encrypt=False" }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IFirebaseTokenVerifier>(); services.AddSingleton<IFirebaseTokenVerifier>(backend);
                services.RemoveAll<IAdminRoleReader>(); services.AddSingleton<IAdminRoleReader>(roles ?? backend);
                services.RemoveAll<ISettingsStore>(); services.AddSingleton<ISettingsStore>(store ?? backend);
            });
        }
    }
    internal sealed class Backend(JsonElement input) : IFirebaseTokenVerifier, IAdminRoleReader, ISettingsStore, ISettingsTransaction
    {
        public int TokenReads { get; private set; }
        public int RoleReads { get; private set; }
        public int MetadataReads { get; private set; }
        public JsonArray Writes { get; } = [];
        private string document = "";
        private bool Flag(string key) => input.TryGetProperty(key, out var field) && field.ValueKind == JsonValueKind.True;
        public bool IsConfigured => !Flag("authError");
        public void EnsureConfigured() { if (Flag("configError")) throw new FirebaseConfigurationException(); }
        public Task<RequestIdentity> VerifyAsync(string token, CancellationToken cancellationToken)
        {
            TokenReads++;
            if (Flag("tokenError")) throw new InvalidFirebaseTokenException();
            object admin = input.TryGetProperty("admin", out var field) ? field.ValueKind == JsonValueKind.String ? field.GetString()! : field.GetBoolean() : true;
            return Task.FromResult(new RequestIdentity("user-a", new Dictionary<string, object> { ["admin"] = admin, ["email"] = "admin@example.test" }));
        }
        public Task<AdminRole?> ReadAsync(string uid, CancellationToken cancellationToken)
        {
            Assert.Equal("user-a", uid); RoleReads++;
            if (Flag("roleError")) throw new Exception("provider-private");
            return Task.FromResult<AdminRole?>(Flag("roleMissing") ? null : new("admin"));
        }
        Task<JsonObject?> ISettingsStore.ReadAsync(string id, CancellationToken cancellationToken) => ReadMetadataAsync(id, cancellationToken);
        private Task<JsonObject?> ReadMetadataAsync(string id, CancellationToken cancellationToken)
        {
            MetadataReads++;
            if (Flag("readError")) throw new Exception("provider-private");
            return Task.FromResult(Flag("missing") || !input.TryGetProperty("document", out var fields) ? null : JsonNode.Parse(fields.GetRawText())!.AsObject());
        }
        public Task<SettingsResult> RunTransactionAsync(string id, Func<ISettingsTransaction, Task<SettingsResult>> operation, CancellationToken cancellationToken)
        {
            if (Flag("transactionError")) throw new Exception("provider-private");
            document = id;
            return operation(this);
        }
        Task<JsonObject?> ISettingsTransaction.ReadAsync(CancellationToken cancellationToken) => ReadMetadataAsync(document, cancellationToken);
        public void Replace(JsonObject fields, SettingsAudit audit)
        {
            var data = (JsonObject)fields.DeepClone(); data["updatedAt"] = "$serverTimestamp";
            Writes.Add(new JsonObject { ["collection"] = "metadata", ["id"] = document, ["operation"] = "set", ["data"] = data });
            Writes.Add(new JsonObject
            {
                ["collection"] = "adminAudit", ["id"] = "audit-entry", ["operation"] = "create",
                ["data"] = new JsonObject
                {
                    ["actorUid"] = audit.Actor.Uid, ["actorEmail"] = FlowerPriceRules.Email(audit.Actor),
                    ["action"] = audit.Action, ["resourceType"] = audit.ResourceType, ["resourceId"] = audit.ResourceId,
                    ["details"] = audit.Details.DeepClone(), ["createdAt"] = "$serverTimestamp"
                }
            });
        }
    }
}
