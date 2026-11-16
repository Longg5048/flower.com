using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using FlowerFlow.Application.Admin;
using FlowerFlow.Application.Identity;
using FlowerFlow.Infrastructure.Firebase;
using Google.Cloud.Firestore;
using Xunit;

namespace FlowerFlow.Foundation.Tests;

public sealed class AdminStorageTests
{
    private static AuthUser User(string uid, bool admin, string role = "admin") => new(uid, uid + "@example.test", uid, false,
        admin ? new Dictionary<string, object> { ["admin"] = true, ["role"] = role } : new Dictionary<string, object>(), [], "", "");
    [FirestoreEmulatorFact, Trait("Category", "Emulator")]
    public async Task Role_backfill_preserves_existing_admins_removes_stale_roles_and_merge_updates_preserve_extra_fields()
    {
        await using var fixture = await Fixture.CreateAsync();
        var roles = fixture.Database.Collection("adminRoles");
        await roles.Document("existing").SetAsync(new Dictionary<string, object> { ["role"] = "admin", ["email"] = "preserved", ["extra"] = true }, cancellationToken: fixture.Token);
        await roles.Document("stale").SetAsync(new Dictionary<string, object> { ["role"] = "admin" }, cancellationToken: fixture.Token);
        await roles.Document("untouched").SetAsync(new Dictionary<string, object> { ["role"] = "admin" }, cancellationToken: fixture.Token);
        await fixture.Store.SyncRolesAsync([User("existing", true), User("new-owner", true, "owner"), User("stale", false)], fixture.Token);
        Assert.Equal("preserved", (await roles.Document("existing").GetSnapshotAsync(fixture.Token)).GetValue<string>("email"));
        Assert.Equal("owner", (await roles.Document("new-owner").GetSnapshotAsync(fixture.Token)).GetValue<string>("role"));
        Assert.False((await roles.Document("stale").GetSnapshotAsync(fixture.Token)).Exists);
        Assert.True((await roles.Document("untouched").GetSnapshotAsync(fixture.Token)).Exists);
        await fixture.Store.SyncRoleAsync(User("existing", true), true, fixture.Token);
        var merged = await roles.Document("existing").GetSnapshotAsync(fixture.Token);
        Assert.True(merged.GetValue<bool>("extra"));
        Assert.Equal("existing@example.test", merged.GetValue<string>("email"));
        await fixture.Store.SyncRoleAsync(User("existing", false), false, fixture.Token);
        Assert.False((await roles.Document("existing").GetSnapshotAsync(fixture.Token)).Exists);
        Assert.Empty((await fixture.Database.Collection("adminAudit").GetSnapshotAsync(fixture.Token)).Documents);
    }
    [FirestoreEmulatorFact, Trait("Category", "Emulator")]
    public async Task Owner_HTTP_updates_role_mirror_and_audit_then_revocation_blocks_subsequent_requests()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.Database.Collection("adminRoles").Document("actor").SetAsync(new Dictionary<string, object> { ["role"] = "owner" }, cancellationToken: fixture.Token);
        using var input = JsonDocument.Parse("{\"users\":[{\"uid\":\"customer-1\",\"email\":\"customer@example.test\",\"displayName\":\"Customer\",\"disabled\":false,\"customClaims\":{\"custom\":true},\"providerData\":[],\"metadata\":{\"creationTime\":\"\",\"lastSignInTime\":\"\"}}]}");
        var backend = new AdminContractTests.Backend(input.RootElement);
        await using var host = new AdminContractTests.Host(backend, fixture.Store, new FirestoreAdminRoleReader(() => fixture.Database));
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "owner");
        var response = await client.PostAsJsonAsync("/api/owner/users", new { action = "grant-admin", uid = "customer-1" }, fixture.Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("revoke:customer-1", backend.Events);
        Assert.Equal("admin", (await fixture.Database.Collection("adminRoles").Document("customer-1").GetSnapshotAsync(fixture.Token)).GetValue<string>("role"));
        var entry = Assert.Single((await fixture.Database.Collection("adminAudit").GetSnapshotAsync(fixture.Token)).Documents);
        Assert.Equal("owner-user.grant-admin", entry.GetValue<string>("action"));
        Assert.Equal("actor", entry.GetValue<string>("actorUid"));
        Assert.Equal("customer", entry.GetValue<Dictionary<string, object>>("details")["previousRole"]);
        var audit = await client.GetAsync("/api/admin/users?route=audit", fixture.Token);
        Assert.Equal(HttpStatusCode.OK, audit.StatusCode);
        Assert.Single(JsonNode.Parse(await audit.Content.ReadAsStringAsync(fixture.Token))!["entries"]!.AsArray());
        await fixture.Database.Collection("adminRoles").Document("actor").DeleteAsync(cancellationToken: fixture.Token);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/audit", fixture.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/owner/users", new { action = "grant-admin", uid = "customer-1" }, fixture.Token)).StatusCode);
        Assert.Single((await fixture.Database.Collection("adminAudit").GetSnapshotAsync(fixture.Token)).Documents);
    }
    [FirestoreEmulatorFact, Trait("Category", "Emulator")]
    public async Task Audit_reader_orders_and_limits_latest_one_hundred_server_timestamps()
    {
        await using var fixture = await Fixture.CreateAsync();
        var batch = fixture.Database.StartBatch();
        for (var index = 0; index < 105; index++) batch.Set(fixture.Database.Collection("adminAudit").Document(index.ToString("D3")), new Dictionary<string, object>
            { ["createdAt"] = Timestamp.FromDateTime(new DateTime(2026, 11, 16, 0, 0, 0, DateTimeKind.Utc).AddSeconds(index)), ["details"] = new Dictionary<string, object> { ["index"] = index } });
        await batch.CommitAsync(fixture.Token);
        var entries = await fixture.Store.ReadAuditAsync(fixture.Token);
        Assert.Equal(100, entries.Count); Assert.Equal("104", entries[0].Id); Assert.Equal("005", entries[^1].Id);
        Assert.All(entries, entry => Assert.True(entry.ServerTimestamp));
    }
    private sealed class Fixture : IAsyncDisposable
    {
        public required FirestoreDb Database { get; init; }
        public required FirestoreAdminStore Store { get; init; }
        private readonly CancellationTokenSource timeout = new(TimeSpan.FromSeconds(45));
        public CancellationToken Token => timeout.Token;
        public static Task<Fixture> CreateAsync()
        {
            var endpoint = Environment.GetEnvironmentVariable("FLOWERFLOW_TEST_FIRESTORE_HOST")!;
            if (!Uri.TryCreate("http://" + endpoint, UriKind.Absolute, out var uri) || !uri.IsLoopback)
                throw new InvalidOperationException("Only loopback emulator endpoints are permitted.");
            var db = new FirestoreDbBuilder
            {
                ProjectId = "demo-admin-" + Guid.NewGuid().ToString("N")[..12], Endpoint = endpoint,
                ChannelCredentials = Grpc.Core.ChannelCredentials.Insecure,
                Settings = new Google.Cloud.Firestore.V1.FirestoreSettings
                { CallSettings = Google.Api.Gax.Grpc.CallSettings.FromHeader("Authorization", "Bearer owner") }
            }.Build();
            return Task.FromResult(new Fixture { Database = db, Store = new(() => db) });
        }
        public async ValueTask DisposeAsync()
        {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var batch = Database.StartBatch();
            foreach (var collection in new[] { "adminRoles", "adminAudit" })
                foreach (var document in (await Database.Collection(collection).GetSnapshotAsync(cleanup.Token)).Documents) batch.Delete(document.Reference);
            await batch.CommitAsync(cleanup.Token);
            timeout.Dispose();
        }
    }
}
