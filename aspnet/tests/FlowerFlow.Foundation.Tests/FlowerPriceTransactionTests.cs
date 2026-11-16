using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using FlowerFlow.Application.Catalog;
using FlowerFlow.Application.Identity;
using FlowerFlow.Infrastructure.Firebase;
using Google.Cloud.Firestore;
using Xunit;

namespace FlowerFlow.Foundation.Tests;

public sealed class FlowerPriceTransactionTests
{
    [FirestoreEmulatorFact, Trait("Category", "Emulator")]
    public async Task HTTP_update_commits_price_history_audit_and_noop_preserves_all_three()
    {
        await using var fixture = await Fixture.CreateAsync();
        using var input = JsonDocument.Parse("{}");
        var backend = new FlowerPricesContractTests.Backend(input.RootElement);
        await using var host = new FlowerPricesContractTests.Host(backend, fixture.Store, new FirestoreAdminRoleReader(() => fixture.Database));
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "owner");
        var response = await client.PatchAsJsonAsync("/api/products?route=flower-prices", new { flowerId = "rose", price = 15000 }, fixture.Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = JsonNode.Parse(await response.Content.ReadAsStringAsync(fixture.Token))!;
        Assert.True(result["changed"]!.GetValue<bool>());
        Assert.Equal("2026-11-16T18:30:00.123Z", result["flower"]!["priceUpdatedAt"]!.GetValue<string>());
        Assert.Equal("owner@example.test", result["flower"]!["updatedBy"]!.GetValue<string>());
        var product = await fixture.Product.GetSnapshotAsync(fixture.Token);
        var history = Assert.Single((await fixture.Database.Collection("priceHistory").GetSnapshotAsync(fixture.Token)).Documents);
        var audit = Assert.Single((await fixture.Database.Collection("adminAudit").GetSnapshotAsync(fixture.Token)).Documents);
        Assert.Equal(15000L, product.GetValue<long>("price"));
        Assert.Equal(7L, product.GetValue<long>("quantity"));
        Assert.Equal("2026-11-17", product.GetValue<string>("priceUpdatedAt"));
        Assert.Equal("owner-a", product.GetValue<string>("priceUpdatedBy"));
        Assert.Equal(12000, history.GetValue<double>("oldPrice"));
        Assert.Equal(15000L, history.GetValue<long>("newPrice"));
        Assert.Equal("owner@example.test", history.GetValue<string>("updatedByEmail"));
        Assert.Equal("flower-price.update", audit.GetValue<string>("action"));
        Assert.Equal("flower", audit.GetValue<string>("resourceType"));
        Assert.Equal("rose", audit.GetValue<string>("resourceId"));
        Assert.Equal("owner-a", audit.GetValue<string>("actorUid"));
        Assert.Equal("owner@example.test", audit.GetValue<string>("actorEmail"));
        Assert.Equal(15000, Convert.ToDouble(audit.GetValue<Dictionary<string, object>>("details")["newPrice"]));
        Assert.Equal(product.GetValue<Timestamp>("priceChangedAt"), history.GetValue<Timestamp>("updatedAt"));
        Assert.Equal(history.GetValue<Timestamp>("updatedAt"), audit.GetValue<Timestamp>("createdAt"));

        var noChange = await client.PatchAsJsonAsync("/api/flower-prices", new { flowerId = "rose", price = 15000 }, fixture.Token);
        Assert.Equal(HttpStatusCode.OK, noChange.StatusCode);
        Assert.False(JsonNode.Parse(await noChange.Content.ReadAsStringAsync(fixture.Token))!["changed"]!.GetValue<bool>());
        Assert.Equal(product.UpdateTime, (await fixture.Product.GetSnapshotAsync(fixture.Token)).UpdateTime);
        Assert.Single((await fixture.Database.Collection("priceHistory").GetSnapshotAsync(fixture.Token)).Documents);
        Assert.Single((await fixture.Database.Collection("adminAudit").GetSnapshotAsync(fixture.Token)).Documents);
        var historyResponse = await client.GetAsync("/api/flower-prices?flowerId=rose", fixture.Token);
        Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);
        Assert.Single(JsonNode.Parse(await historyResponse.Content.ReadAsStringAsync(fixture.Token))!["history"]!.AsArray());

        await fixture.Role.DeleteAsync(cancellationToken: fixture.Token);
        var denied = await client.PatchAsJsonAsync("/api/flower-prices", new { flowerId = "rose", price = 18000 }, fixture.Token);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal(15000L, (await fixture.Product.GetSnapshotAsync(fixture.Token)).GetValue<long>("price"));
        Assert.Single((await fixture.Database.Collection("priceHistory").GetSnapshotAsync(fixture.Token)).Documents);
    }

    [FirestoreEmulatorFact, Trait("Category", "Emulator")]
    public async Task Exception_after_staging_writes_rolls_back_product_history_and_audit()
    {
        await using var fixture = await Fixture.CreateAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Store.RunTransactionAsync("rose", async transaction =>
        {
            Assert.NotNull(await transaction.ReadProductAsync(fixture.Token));
            transaction.StageChange(new("rose", 12000, 15000, "2026-11-17", Actor()));
            throw new InvalidOperationException("Injected failure before transaction commit.");
        }, fixture.Token));
        Assert.Equal(12000L, (await fixture.Product.GetSnapshotAsync(fixture.Token)).GetValue<long>("price"));
        Assert.Empty((await fixture.Database.Collection("priceHistory").GetSnapshotAsync(fixture.Token)).Documents);
        Assert.Empty((await fixture.Database.Collection("adminAudit").GetSnapshotAsync(fixture.Token)).Documents);
    }

    [FirestoreEmulatorFact, Trait("Category", "Emulator")]
    public async Task Concurrent_updates_produce_a_consistent_price_history_chain()
    {
        await using var fixture = await Fixture.CreateAsync();
        using var input = JsonDocument.Parse("{}");
        var backend = new FlowerPricesContractTests.Backend(input.RootElement);
        await using var host = new FlowerPricesContractTests.Host(backend, fixture.Store, new FirestoreAdminRoleReader(() => fixture.Database));
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "owner");
        var responses = await Task.WhenAll(
            client.PatchAsJsonAsync("/api/flower-prices", new { flowerId = "rose", price = 15000 }, fixture.Token),
            client.PatchAsJsonAsync("/api/flower-prices", new { flowerId = "rose", price = 18000 }, fixture.Token));
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        var history = (await fixture.Database.Collection("priceHistory").GetSnapshotAsync(fixture.Token)).Documents;
        var audits = (await fixture.Database.Collection("adminAudit").GetSnapshotAsync(fixture.Token)).Documents;
        Assert.Equal(2, history.Count);
        Assert.Equal(2, audits.Count);
        var first = Assert.Single(history, doc => doc.GetValue<double>("oldPrice") == 12000);
        var second = Assert.Single(history, doc => doc.Id != first.Id);
        Assert.Equal(first.GetValue<double>("newPrice"), second.GetValue<double>("oldPrice"));
        Assert.Equal(second.GetValue<double>("newPrice"), (await fixture.Product.GetSnapshotAsync(fixture.Token)).GetValue<double>("price"));
        foreach (var entry in history)
            Assert.Single(audits, audit =>
            {
                var details = audit.GetValue<Dictionary<string, object>>("details");
                return Convert.ToDouble(details["oldPrice"]) == entry.GetValue<double>("oldPrice")
                    && Convert.ToDouble(details["newPrice"]) == entry.GetValue<double>("newPrice");
            });
    }

    private static RequestIdentity Actor() => new("owner-a", new Dictionary<string, object> { ["email"] = "owner@example.test" });

    private sealed class Fixture : IAsyncDisposable
    {
        public required FirestoreDb Database { get; init; }
        public required FirestoreFlowerPriceStore Store { get; init; }
        private readonly CancellationTokenSource timeout = new(TimeSpan.FromSeconds(45));
        public CancellationToken Token => timeout.Token;
        public DocumentReference Product => Database.Collection("products").Document("rose");
        public DocumentReference Role => Database.Collection("adminRoles").Document("owner-a");
        public static async Task<Fixture> CreateAsync()
        {
            var endpoint = Environment.GetEnvironmentVariable("FLOWERFLOW_TEST_FIRESTORE_HOST")!;
            if (!Uri.TryCreate("http://" + endpoint, UriKind.Absolute, out var uri) || !uri.IsLoopback)
                throw new InvalidOperationException("Only loopback emulator endpoints are permitted.");
            var db = new FirestoreDbBuilder
            {
                ProjectId = "demo-prices-" + Guid.NewGuid().ToString("N")[..12], Endpoint = endpoint,
                ChannelCredentials = Grpc.Core.ChannelCredentials.Insecure,
                Settings = new Google.Cloud.Firestore.V1.FirestoreSettings
                { CallSettings = Google.Api.Gax.Grpc.CallSettings.FromHeader("Authorization", "Bearer owner") }
            }.Build();
            var fixture = new Fixture { Database = db, Store = new(() => db) };
            await fixture.Product.SetAsync(new Dictionary<string, object>
            { ["saleType"] = "flower", ["price"] = 12000L, ["title"] = "Hoa hồng", ["quantity"] = 7L }, cancellationToken: fixture.Token);
            await fixture.Role.SetAsync(new Dictionary<string, object> { ["role"] = "owner" }, cancellationToken: fixture.Token);
            return fixture;
        }
        public async ValueTask DisposeAsync()
        {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            foreach (var collection in new[] { "products", "adminRoles", "priceHistory", "adminAudit" })
                foreach (var document in (await Database.Collection(collection).GetSnapshotAsync(cleanup.Token)).Documents)
                    await document.Reference.DeleteAsync(cancellationToken: cleanup.Token);
            timeout.Dispose();
        }
    }
}
