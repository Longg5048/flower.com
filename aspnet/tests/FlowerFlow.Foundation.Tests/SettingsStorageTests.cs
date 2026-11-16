using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using FlowerFlow.Application.Identity;
using FlowerFlow.Application.ShopSettings;
using FlowerFlow.Infrastructure.Firebase;
using Google.Cloud.Firestore;
using Xunit;

namespace FlowerFlow.Foundation.Tests;

public sealed class SettingsStorageTests
{
    [FirestoreEmulatorFact, Trait("Category", "Emulator")]
    public async Task Settings_transactions_preserve_other_zones_reject_duplicates_and_commit_audit()
    {
        await using var fixture = await Fixture.CreateAsync();
        using var input = JsonDocument.Parse("{}");
        var backend = new SettingsContractTests.Backend(input.RootElement);
        await using var host = new SettingsContractTests.Host(backend, fixture.Store, new FirestoreAdminRoleReader(() => fixture.Database));
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "admin");
        var first = new { id = "a", province = "Hà Nội", district = "Ba Đình", fee = 10000, enabled = true };
        var second = new { id = "b", province = "Hà Nội", district = "Đống Đa", fee = 20000, enabled = false };
        var responses = await Task.WhenAll(
            client.PostAsJsonAsync("/api/delivery-zones", new { action = "upsert", zone = first }, fixture.Token),
            client.PostAsJsonAsync("/api/delivery-zones", new { action = "upsert", zone = second }, fixture.Token));
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        var metadata = fixture.Database.Collection("metadata").Document("deliveryZones");
        var before = await metadata.GetSnapshotAsync(fixture.Token);
        Assert.Equal(2, before.GetValue<List<Dictionary<string, object>>>("zones").Count);
        var audits = (await fixture.Database.Collection("adminAudit").GetSnapshotAsync(fixture.Token)).Documents;
        Assert.Equal(2, audits.Count);
        Assert.All(audits, audit => Assert.Equal("delivery-zone.upsert", audit.GetValue<string>("action")));
        Assert.Contains(audits, audit => audit.GetValue<Timestamp>("createdAt") == before.GetValue<Timestamp>("updatedAt"));
        var duplicate = await client.PostAsJsonAsync("/api/delivery-zones", new { action = "upsert", zone = new
            { id = "c", province = "HÀ NỘI", district = "BA ĐÌNH", fee = 0, enabled = false } }, fixture.Token);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(before.UpdateTime, (await metadata.GetSnapshotAsync(fixture.Token)).UpdateTime);
        Assert.Equal(2, (await fixture.Database.Collection("adminAudit").GetSnapshotAsync(fixture.Token)).Count);
        using var anonymous = host.CreateClient();
        var visible = JsonNode.Parse(await (await anonymous.GetAsync("/api/delivery-zones", fixture.Token)).Content.ReadAsStringAsync(fixture.Token))!;
        Assert.Single(visible["zones"]!.AsArray());
        Assert.Equal("a", visible["zones"]![0]!["id"]!.GetValue<string>());
        var deleted = await client.PostAsJsonAsync("/api/delivery-zones", new { action = "delete", id = "a" }, fixture.Token);
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        Assert.Single((await metadata.GetSnapshotAsync(fixture.Token)).GetValue<List<Dictionary<string, object>>>("zones"));
        Assert.Equal(3, (await fixture.Database.Collection("adminAudit").GetSnapshotAsync(fixture.Token)).Count);
        var wrapping = new { tiers = new[] { new { maxStems = 10, fee = 0 }, new { maxStems = 50, fee = 30000 } } };
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/delivery-zones?route=bouquet-wrapping", wrapping, fixture.Token)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/bouquet-wrapping", wrapping, fixture.Token)).StatusCode);
        Assert.Equal(5, (await fixture.Database.Collection("adminAudit").GetSnapshotAsync(fixture.Token)).Count);
        var tiers = JsonNode.Parse(await (await anonymous.GetAsync("/api/bouquet-wrapping", fixture.Token)).Content.ReadAsStringAsync(fixture.Token))!;
        Assert.True(tiers["configured"]!.GetValue<bool>());
        Assert.Equal(2, tiers["tiers"]!.AsArray().Count);
        await fixture.Database.Collection("adminRoles").Document("user-a").DeleteAsync(cancellationToken: fixture.Token);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/bouquet-wrapping", wrapping, fixture.Token)).StatusCode);
        Assert.Equal(5, (await fixture.Database.Collection("adminAudit").GetSnapshotAsync(fixture.Token)).Count);
    }

    [FirestoreEmulatorFact, Trait("Category", "Emulator")]
    public async Task Metadata_and_audit_rollback_together_before_commit()
    {
        await using var fixture = await Fixture.CreateAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Store.RunTransactionAsync("bouquetWrapping", async transaction =>
        {
            Assert.Null(await transaction.ReadAsync(fixture.Token));
            var fields = JsonNode.Parse("{\"tiers\":[{\"maxStems\":50,\"fee\":1000}]}")!.AsObject();
            transaction.Replace(fields, new(new RequestIdentity("user-a", new Dictionary<string, object>()),
                "bouquet-wrapping.update", "bouquet-wrapping", "bouquetWrapping", new JsonObject()));
            throw new InvalidOperationException("Injected failure before commit.");
        }, fixture.Token));
        Assert.False((await fixture.Database.Collection("metadata").Document("bouquetWrapping").GetSnapshotAsync(fixture.Token)).Exists);
        Assert.Empty((await fixture.Database.Collection("adminAudit").GetSnapshotAsync(fixture.Token)).Documents);
    }
    private sealed class Fixture : IAsyncDisposable
    {
        public required FirestoreDb Database { get; init; }
        public required FirestoreSettingsStore Store { get; init; }
        private readonly CancellationTokenSource timeout = new(TimeSpan.FromSeconds(40));
        public CancellationToken Token => timeout.Token;
        public static async Task<Fixture> CreateAsync()
        {
            var endpoint = Environment.GetEnvironmentVariable("FLOWERFLOW_TEST_FIRESTORE_HOST")!;
            if (!Uri.TryCreate("http://" + endpoint, UriKind.Absolute, out var uri) || !uri.IsLoopback)
                throw new InvalidOperationException("Only loopback emulator endpoints are permitted.");
            var db = new FirestoreDbBuilder
            {
                ProjectId = "demo-settings-" + Guid.NewGuid().ToString("N")[..12], Endpoint = endpoint,
                ChannelCredentials = Grpc.Core.ChannelCredentials.Insecure,
                Settings = new Google.Cloud.Firestore.V1.FirestoreSettings
                { CallSettings = Google.Api.Gax.Grpc.CallSettings.FromHeader("Authorization", "Bearer owner") }
            }.Build();
            var fixture = new Fixture { Database = db, Store = new(() => db) };
            await db.Collection("adminRoles").Document("user-a").SetAsync(new Dictionary<string, object> { ["role"] = "admin" }, cancellationToken: fixture.Token);
            return fixture;
        }
        public async ValueTask DisposeAsync()
        {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            foreach (var collection in new[] { "metadata", "adminRoles", "adminAudit" })
                foreach (var document in (await Database.Collection(collection).GetSnapshotAsync(cleanup.Token)).Documents)
                    await document.Reference.DeleteAsync(cancellationToken: cleanup.Token);
            timeout.Dispose();
        }
    }
}
