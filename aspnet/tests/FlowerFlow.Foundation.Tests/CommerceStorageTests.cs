using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using FlowerFlow.Application.Commerce;
using FlowerFlow.Infrastructure.Firebase;
using FlowerFlow.Infrastructure.Vnpay;
using Google.Cloud.Firestore;
using Xunit;

namespace FlowerFlow.Foundation.Tests;

public sealed class CommerceStorageTests
{
    [FirestoreEmulatorFact, Trait("Category", "Emulator")]
    public async Task Design_quote_can_be_accepted_once_and_never_reserves_inventory()
    {
        await using var f = await Fixture.CreateAsync(10); using var client = f.Host.CreateClient();
        await f.Db.Document("products/rose").SetAsync(new { title = "Hồng", price = 12000, quantity = 20, saleType = "flower", colors = new[] { "đỏ" } }, cancellationToken: f.Token);
        await f.Db.Document("metadata/bouquetWrapping").SetAsync(new { tiers = new[] { new { maxStems = 50, fee = 15000 } } }, cancellationToken: f.Token);
        var create = await client.PostAsJsonAsync("/api/design-requests", new { action = "create", customerName = "Khách", phone = "0901234567", address = "1 đường Hoa", deliveryDate = "2027-01-01", deliveryTime = "08:00–11:00", deliveryZoneId = "zone-a", design = new { wrapStyle = "Bó tròn" }, stems = new[] { new { productId = "rose", quantity = 5, color = "đỏ" } } }, f.Token);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var result = JsonNode.Parse(await create.Content.ReadAsStringAsync(f.Token))!; var id = result["request"]!["id"]!.GetValue<string>(); var token = result["accessToken"]!.GetValue<string>();
        client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", "Bearer admin");
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/design-requests", new { action = "admin-update", id, status = "Chờ khách xác nhận", quotedTotal = 90000 }, f.Token)).StatusCode);
        client.DefaultRequestHeaders.Remove("Authorization");
        var body = new { action = "respond", id, accessToken = token, response = "accept" };
        var responses = await Task.WhenAll(client.PostAsJsonAsync("/api/design-requests", body, f.Token), client.PostAsJsonAsync("/api/design-requests", body, f.Token));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK); Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
        var saved = await f.Db.Document("designRequests/" + id).GetSnapshotAsync(f.Token);
        Assert.Equal("Đang thực hiện", saved.GetValue<string>("status")); Assert.Equal(3, saved.GetValue<List<object>>("history").Count);
        Assert.False(saved.ContainsField("accessToken")); Assert.DoesNotContain("accessTokenHash", await (await client.GetAsync($"/api/design-requests?id={id}&token={token}", f.Token)).Content.ReadAsStringAsync(f.Token));
        Assert.Equal(20, (await f.Db.Document("products/rose").GetSnapshotAsync(f.Token)).GetValue<long>("quantity"));
    }
    [FirestoreEmulatorFact, Trait("Category", "Emulator")]
    public async Task Review_and_binary_photos_commit_together_and_repeat_submission_is_rejected()
    {
        await using var f = await Fixture.CreateAsync(10); using var client = f.Host.CreateClient();
        await f.Db.Document("orders/delivered").SetAsync(new { uid = "customer", status = "Đã giao", customerName = "Khách", items = new[] { new { id = "ready", title = "Bó hoa", quantity = 1 } } }, cancellationToken: f.Token);
        client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", "Bearer customer");
        var review = new { action = "review", orderId = "delivered", rating = 5, comment = "Hoa đẹp", photos = new[] { "data:image/jpeg;base64," + Convert.ToBase64String(ImageContractTests.Sample) } };
        var replies = await Task.WhenAll(client.PostAsJsonAsync("/api/orders", review, f.Token), client.PostAsJsonAsync("/api/orders", review, f.Token));
        Assert.Single(replies, r => r.StatusCode == HttpStatusCode.Created); Assert.Single(replies, r => r.StatusCode == HttpStatusCode.Conflict);
        var saved = (await f.Db.Collection("reviews").GetSnapshotAsync(f.Token)).Documents.Single();
        var photo = (await f.Db.Collection("reviewImages").GetSnapshotAsync(f.Token)).Documents.Single();
        Assert.Equal(saved.Id, photo.GetValue<string>("reviewId")); Assert.Equal(ImageContractTests.Sample, photo.GetValue<Blob>("bytes").ByteString.ToByteArray());
        Assert.Equal("pending", saved.GetValue<string>("moderationStatus"));
    }
    [FirestoreEmulatorFact, Trait("Category", "Emulator")]
    public async Task Browser_frontend_uses_real_ASPNET_HTTP_and_Firestore_for_guest_checkout()
    {
        await using var f = await Fixture.CreateAsync(10);
        f.Host.UseKestrel(); using var client = f.Host.CreateClient();
        var start = new System.Diagnostics.ProcessStartInfo("node") { RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add(Path.Combine(ImageContractTests.Root(), "aspnet/tests/FlowerFlow.Foundation.Tests/browser-commerce.cjs"));
        start.ArgumentList.Add(client.BaseAddress!.ToString());
        using var process = System.Diagnostics.Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync(); var errors = process.StandardError.ReadToEndAsync();
        try { await process.WaitForExitAsync(f.Token); } catch { process.Kill(true); throw; }
        Assert.True(process.ExitCode == 0, await output + await errors);
        Assert.Single((await f.Db.Collection("orders").GetSnapshotAsync(f.Token)).Documents);
        Assert.Single((await f.Db.Collection("paymentTransactions").GetSnapshotAsync(f.Token)).Documents);
        Assert.Equal(9, (await f.Db.Document("products/ready").GetSnapshotAsync(f.Token)).GetValue<long>("quantity"));
    }
    private static object Checkout(string key, string phone = "0901234567", string method = "qr") => new
    {
        customerName = "Khách", phone, address = "1 đường Hoa", deliveryDate = "2027-01-01", deliveryTime = "08:00–11:00", deliveryZoneId = "zone-a", paymentMethod = method,
        idempotencyKey = key, items = new[] { new { id = "ready", quantity = 1 } }, expectedTotals = new { subtotal = 100000, savings = 0, tax = 0, shippingFee = 10000, total = 110000 }
    };
    [FirestoreEmulatorFact, Trait("Category", "Emulator")]
    public async Task Concurrent_checkout_never_oversells_and_expiry_restores_stock_once()
    {
        await using var f = await Fixture.CreateAsync(1); using var client = f.Host.CreateClient();
        var results = await Task.WhenAll(client.PostAsJsonAsync("/api/orders", Checkout(Guid.NewGuid().ToString()), f.Token), client.PostAsJsonAsync("/api/orders", Checkout(Guid.NewGuid().ToString(), "0912345678"), f.Token));
        Assert.Single(results, r => r.StatusCode == HttpStatusCode.Created); Assert.Single(results, r => r.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(0, (await f.Db.Document("products/ready").GetSnapshotAsync(f.Token)).GetValue<long>("quantity"));
        Assert.Single((await f.Db.Collection("orders").GetSnapshotAsync(f.Token)).Documents);
        f.Clock.Now += 1800001;
        client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", "Bearer test-cron");
        var expirations = await Task.WhenAll(client.GetAsync("/api/orders?action=expire", f.Token), client.GetAsync("/api/orders?action=expire", f.Token));
        Assert.All(expirations, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        Assert.Equal(1, (await f.Db.Document("products/ready").GetSnapshotAsync(f.Token)).GetValue<long>("quantity"));
        Assert.Equal("released", (await f.Db.Collection("orders").GetSnapshotAsync(f.Token)).Documents.Single().GetValue<string>("inventoryState"));
    }
    [FirestoreEmulatorFact, Trait("Category", "Emulator")]
    public async Task Concurrent_idempotent_requests_make_one_order_and_one_stock_decrement()
    {
        await using var f = await Fixture.CreateAsync(10); using var client = f.Host.CreateClient(); var body = Checkout(Guid.NewGuid().ToString());
        var replies = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => client.PostAsJsonAsync("/api/orders", body, f.Token)));
        foreach (var reply in replies) Assert.True(reply.StatusCode == HttpStatusCode.Created, await reply.Content.ReadAsStringAsync(f.Token));
        Assert.Single((await f.Db.Collection("orders").GetSnapshotAsync(f.Token)).Documents);
        Assert.Single((await f.Db.Collection("orderRequests").GetSnapshotAsync(f.Token)).Documents);
        Assert.Equal(9, (await f.Db.Document("products/ready").GetSnapshotAsync(f.Token)).GetValue<long>("quantity"));
    }
    [FirestoreEmulatorFact, Trait("Category", "Emulator")]
    public async Task VNPay_duplicate_IPN_and_refund_commit_once_without_changing_stock()
    {
        await using var f = await Fixture.CreateAsync(10); using var client = f.Host.CreateClient();
        var created = await client.PostAsJsonAsync("/api/orders", Checkout(Guid.NewGuid().ToString(), method: "vnpay"), f.Token);
        Assert.True(created.StatusCode == HttpStatusCode.Created, await created.Content.ReadAsStringAsync(f.Token));
        var order = (await f.Db.Collection("orders").GetSnapshotAsync(f.Token)).Documents.Single();
        var payment = await client.PostAsJsonAsync("/api/payments", new { action = "create", orderId = order.Id, paymentReference = order.GetValue<string>("paymentReference") }, f.Token);
        Assert.Equal(HttpStatusCode.OK, payment.StatusCode);
        var attempt = (await f.Db.Collection("paymentTransactions").GetSnapshotAsync(f.Token)).Documents.Single();
        var query = new JsonObject { ["vnp_TmnCode"] = "test-tmn", ["vnp_TxnRef"] = attempt.Id, ["vnp_Amount"] = "11000000", ["vnp_CurrCode"] = "VND", ["vnp_ResponseCode"] = "00", ["vnp_TransactionStatus"] = "00", ["vnp_TransactionNo"] = "BANK-1" };
        query["vnp_SecureHash"] = Convert.ToHexStringLower(HMACSHA512.HashData(Encoding.UTF8.GetBytes("test-vnpay-hash"), Encoding.UTF8.GetBytes(VnpayGateway.CanonicalQuery(query))));
        var url = "/api/vnpay-ipn?" + string.Join("&", query.Select(p => Uri.EscapeDataString(p.Key) + "=" + Uri.EscapeDataString(p.Value!.GetValue<string>())));
        var replies = await Task.WhenAll(client.GetAsync(url, f.Token), client.GetAsync(url, f.Token));
        foreach (var reply in replies) { Assert.Equal(HttpStatusCode.OK, reply.StatusCode); Assert.Equal("00", JsonNode.Parse(await reply.Content.ReadAsStringAsync(f.Token))!["RspCode"]!.GetValue<string>()); }
        Assert.True((await order.Reference.GetSnapshotAsync(f.Token)).GetValue<bool>("paymentVerified"));
        Assert.Equal(9, (await f.Db.Document("products/ready").GetSnapshotAsync(f.Token)).GetValue<long>("quantity"));
        client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", "Bearer admin");
        var refund = new { action = "record-payment-refund", orderId = order.Id, refundReference = "REFUND-1", amount = 110000 };
        var refunds = await Task.WhenAll(client.PostAsJsonAsync("/api/orders", refund, f.Token), client.PostAsJsonAsync("/api/orders", refund, f.Token));
        Assert.Single(refunds, r => r.StatusCode == HttpStatusCode.OK); Assert.Single(refunds, r => r.StatusCode == HttpStatusCode.Conflict);
        Assert.Single((await f.Db.Collection("orderPaymentAudit").GetSnapshotAsync(f.Token)).Documents);
        Assert.Equal(9, (await f.Db.Document("products/ready").GetSnapshotAsync(f.Token)).GetValue<long>("quantity"));
    }
    private sealed class Fixture : IAsyncDisposable
    {
        public required FirestoreDb Db { get; init; }
        public required CommerceHost Host { get; init; }
        public required LoginContractTests.Clock Clock { get; init; }
        private readonly CancellationTokenSource timeout = new(TimeSpan.FromSeconds(90));
        public CancellationToken Token => timeout.Token;
        public static async Task<Fixture> CreateAsync(int stock)
        {
            var endpoint = Environment.GetEnvironmentVariable("FLOWERFLOW_TEST_FIRESTORE_HOST")!;
            if (!Uri.TryCreate("http://" + endpoint, UriKind.Absolute, out var uri) || !uri.IsLoopback) throw new InvalidOperationException("Loopback emulator required.");
            var db = new FirestoreDbBuilder { ProjectId = "demo-commerce-" + Guid.NewGuid().ToString("N")[..12], Endpoint = endpoint, ChannelCredentials = Grpc.Core.ChannelCredentials.Insecure,
                Settings = new Google.Cloud.Firestore.V1.FirestoreSettings { CallSettings = Google.Api.Gax.Grpc.CallSettings.FromHeader("Authorization", "Bearer owner") } }.Build();
            var clock = new LoginContractTests.Clock { Now = DateTimeOffset.Parse("2026-11-16T03:00:00.123Z").ToUnixTimeMilliseconds() };
            var fixture = new Fixture { Db = db, Clock = clock, Host = new(new FirestoreCommerceStore(() => db), clock) };
            await db.Document("products/ready").SetAsync(new { title = "Bó hoa", price = 100000, quantity = stock }, cancellationToken: fixture.Token);
            await db.Document("metadata/deliveryZones").SetAsync(new { zones = new[] { new { id = "zone-a", province = "Hà Nội", district = "Ba Đình", fee = 10000, enabled = true } } }, cancellationToken: fixture.Token);
            await db.Document("adminRoles/admin").SetAsync(new { role = "admin" }, cancellationToken: fixture.Token);
            return fixture;
        }
        public async ValueTask DisposeAsync()
        {
            await Host.DisposeAsync(); using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            foreach (var collection in new[] { "products", "metadata", "adminRoles", "orders", "orderRequests", "orderCreationLimits", "orderLookupLimits", "paymentTransactions", "orderPaymentAudit", "designRequests", "reviews", "reviewImages" })
                foreach (var doc in (await Db.Collection(collection).GetSnapshotAsync(cleanup.Token)).Documents) await doc.Reference.DeleteAsync(cancellationToken: cleanup.Token);
            timeout.Dispose();
        }
    }
}
