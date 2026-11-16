using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FlowerFlow.Application.Admin;
using FlowerFlow.Application.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace FlowerFlow.Foundation.Tests;

public sealed class AdminContractTests
{
    [Fact]
    public async Task Admin_owner_and_audit_HTTP_match_JavaScript_including_partial_role_update_failures()
    {
        var start = new ProcessStartInfo("node") { RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 };
        start.ArgumentList.Add(Path.Combine(ImageContractTests.Root(), "aspnet/tests/FlowerFlow.Foundation.Tests/admin-oracle.cjs"));
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync(); var errors = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try { await process.WaitForExitAsync(timeout.Token); } catch { process.Kill(true); throw; }
        Assert.True(process.ExitCode == 0, await errors);
        using var scenarios = JsonDocument.Parse(await output);
        Assert.Equal(35, scenarios.RootElement.GetArrayLength());
        foreach (var input in scenarios.RootElement.EnumerateArray())
        {
            var kind = input.GetProperty("kind").GetString();
            var routes = kind == "audit" ? new[] { "/api/admin/audit?", "/api/admin/users?route=audit&" }
                : new[] { kind == "owner" ? "/api/owner/users?" : "/api/admin/users?" };
            foreach (var route in routes)
            {
                var backend = new Backend(input);
                await using var host = new Host(backend);
                using var client = host.CreateClient();
                var query = input.TryGetProperty("query", out var values) ? string.Join("&", values.EnumerateObject().Select(pair =>
                    Uri.EscapeDataString(pair.Name) + "=" + Uri.EscapeDataString(pair.Value.GetString()!))) : "";
                using var request = new HttpRequestMessage(new HttpMethod(input.TryGetProperty("method", out var method) ? method.GetString()! : kind == "audit" ? "GET" : "POST"), route + query);
                var authorization = input.TryGetProperty("authorization", out var auth) ? auth.GetString()! : "Bearer actor";
                if (authorization.Length > 0) request.Headers.TryAddWithoutValidation("Authorization", authorization);
                if (input.TryGetProperty("body", out var body)) request.Content = new StringContent(body.GetRawText(), Encoding.UTF8, "application/json");
                var response = await client.SendAsync(request);
                var actual = JsonSerializer.SerializeToNode(new
                {
                    status = (int)response.StatusCode, body = JsonNode.Parse(await response.Content.ReadAsStringAsync()),
                    cache = response.Headers.NonValidated.TryGetValues("Cache-Control", out var cache) ? string.Join(", ", cache) : null,
                    allow = response.Content.Headers.TryGetValues("Allow", out var allow) ? string.Join(", ", allow) : null,
                    tokenReads = backend.TokenReads, roleReads = backend.RoleReads, events = backend.Events,
                    claims = backend.Claims, mirrors = backend.Mirrors, audits = backend.Audits
                });
                var expected = JsonNode.Parse(input.GetProperty("expected").GetRawText());
                Assert.True(JsonNode.DeepEquals(expected, actual), $"{input.GetProperty("name")} via {route}: expected {expected}; actual {actual}");
            }
        }
    }
    internal sealed class Host(Backend backend, IAdminStore? store = null, IAdminRoleReader? roles = null) : WebApplicationFactory<Program>
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
                services.RemoveAll<IAccountsAdminGateway>(); services.AddSingleton<IAccountsAdminGateway>(backend);
                services.RemoveAll<IAdminStore>(); services.AddSingleton<IAdminStore>(store ?? backend);
            });
        }
    }
    internal sealed class Backend(JsonElement input) : IFirebaseTokenVerifier, IAdminRoleReader, IAccountsAdminGateway, IAdminStore
    {
        public int TokenReads { get; private set; } public int RoleReads { get; private set; }
        public List<string> Events { get; } = []; public JsonArray Claims { get; } = []; public JsonArray Mirrors { get; } = []; public JsonArray Audits { get; } = [];
        private bool Flag(string key) => input.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.True;
        public bool IsConfigured => !Flag("authError");
        public void EnsureConfigured() { if (Flag("storeError")) throw new FirebaseConfigurationException(); }
        public Task<RequestIdentity> VerifyAsync(string token, CancellationToken cancellationToken)
        {
            TokenReads++; if (Flag("tokenError")) throw new InvalidFirebaseTokenException();
            object admin = input.TryGetProperty("admin", out var value) ? value.GetBoolean() : true;
            return Task.FromResult(new RequestIdentity("actor", new Dictionary<string, object> { ["admin"] = admin,
                ["role"] = input.TryGetProperty("claimRole", out var role) ? role.GetString()! : "owner", ["email"] = "owner@example.test" }));
        }
        public Task<AdminRole?> ReadAsync(string uid, CancellationToken cancellationToken)
        {
            Assert.Equal("actor", uid); RoleReads++; if (Flag("roleError")) throw new Exception("provider-private");
            return Task.FromResult<AdminRole?>(Flag("roleMissing") ? null : new(input.TryGetProperty("documentRole", out var role) ? role.GetString() : "owner"));
        }
        private static IReadOnlyDictionary<string, object> ClaimMap(JsonElement value) => value.EnumerateObject().ToDictionary(pair => pair.Name,
            pair => pair.Value.ValueKind switch { JsonValueKind.True => (object)true, JsonValueKind.False => false, JsonValueKind.String => pair.Value.GetString()!, _ => pair.Value.Clone() });
        private static AuthUser User(JsonElement user) => new(user.GetProperty("uid").GetString()!, user.GetProperty("email").GetString()!,
            user.GetProperty("displayName").GetString()!, user.GetProperty("disabled").GetBoolean(), ClaimMap(user.GetProperty("customClaims")),
            user.GetProperty("providerData").EnumerateArray().Select(provider => provider.GetProperty("providerId").GetString()!).ToArray(),
            user.GetProperty("metadata").GetProperty("creationTime").GetString()!, user.GetProperty("metadata").GetProperty("lastSignInTime").GetString()!);
        public Task<AuthUsersPage> ListAsync(string? pageToken, CancellationToken cancellationToken)
        {
            Events.Add("list:" + (pageToken ?? "")); if (Flag("listError")) throw new Exception("provider-private");
            return Task.FromResult(new AuthUsersPage(input.GetProperty("users").EnumerateArray().Select(User).ToArray(), "next-page"));
        }
        public Task<AuthUser> GetAsync(string uid, CancellationToken cancellationToken)
        {
            Events.Add("get:" + uid);
            foreach (var user in input.GetProperty("users").EnumerateArray()) if (user.GetProperty("uid").GetString() == uid) return Task.FromResult(User(user));
            throw new AuthUserNotFoundException();
        }
        public Task SetClaimsAsync(string uid, IReadOnlyDictionary<string, object> claims, CancellationToken cancellationToken)
        {
            Events.Add("claims:" + uid); if (Flag("claimsError")) throw new Exception("provider-private");
            Claims.Add(new JsonObject { ["uid"] = uid, ["claims"] = JsonSerializer.SerializeToNode(claims) }); return Task.CompletedTask;
        }
        public Task RevokeAsync(string uid, CancellationToken cancellationToken)
        { Events.Add("revoke:" + uid); if (Flag("revokeError")) throw new Exception("provider-private"); return Task.CompletedTask; }
        public Task SyncRoleAsync(AuthUser user, bool admin, CancellationToken cancellationToken)
        {
            Events.Add("mirror:" + user.Uid); if (Flag("mirrorError")) throw new Exception("provider-private");
            Mirrors.Add(new JsonObject { ["uid"] = user.Uid, ["admin"] = admin, ["claims"] = JsonSerializer.SerializeToNode(user.Claims) }); return Task.CompletedTask;
        }
        public Task SyncRolesAsync(IReadOnlyList<AuthUser> users, CancellationToken cancellationToken)
        { Events.Add("backfill"); if (Flag("backfillError")) throw new Exception("provider-private"); return Task.CompletedTask; }
        public Task RecordAuditAsync(RequestIdentity actor, string action, string resourceType, string resourceId, JsonObject details, CancellationToken cancellationToken)
        {
            Events.Add("audit-write"); if (Flag("auditError")) throw new Exception("provider-private");
            Audits.Add(new JsonObject { ["actorUid"] = actor.Uid, ["action"] = action, ["resourceType"] = resourceType, ["resourceId"] = resourceId, ["details"] = details.DeepClone() });
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<AdminAuditDocument>> ReadAuditAsync(CancellationToken cancellationToken)
        {
            Events.Add("audit-read"); if (Flag("auditReadError")) throw new Exception("provider-private");
            return Task.FromResult<IReadOnlyList<AdminAuditDocument>>(input.GetProperty("entries").EnumerateArray().Select(entry =>
            {
                var fields = JsonNode.Parse(entry.GetProperty("fields").GetRawText())!.AsObject();
                var timestamp = fields["createdAt"] is JsonObject;
                if (timestamp) fields["createdAt"] = fields["createdAt"]!["$timestamp"]!.GetValue<string>();
                return new AdminAuditDocument(entry.GetProperty("id").GetString()!, fields, timestamp);
            }).ToArray());
        }
    }
}
