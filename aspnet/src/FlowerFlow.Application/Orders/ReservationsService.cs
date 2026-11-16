using System.Globalization;
using FlowerFlow.Application.Commerce;
using FlowerFlow.Domain.Models.Compatibility;

namespace FlowerFlow.Application.Orders;

public sealed class ReservationsService(ICommerceStore store, TimeProvider time)
{
    public async Task ReleaseQuietlyAsync(CancellationToken cancellationToken)
    {
        try { await ExpireAsync(500, cancellationToken); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { /* Cleanup failures must not block quotes/orders. */ }
    }
    public async Task<int> ExpireAsync(int maximum, CancellationToken cancellationToken)
    {
        var released = 0; var scanned = 0;
        while (released < maximum && scanned < maximum)
        {
            var now = DocumentTimestamp.FromMilliseconds(time.GetUtcNow().ToUnixTimeMilliseconds());
            var documents = await store.QueryAsync(new("orders", [new("inventoryReserved", "==", true), new("reservationExpiresAt", "<=", now)],
                "reservationExpiresAt", Limit: Math.Min(100, maximum - scanned)), cancellationToken);
            if (documents.Count == 0) break;
            scanned += documents.Count;
            var results = await Task.WhenAll(documents.Select(document => ExpireOneAsync(document.Path, now, cancellationToken)));
            released += results.Count(result => result);
        }
        return released;
    }
    public Task<bool> ExpireOneAsync(string path, DocumentTimestamp now, CancellationToken cancellationToken) => store.RunAsync(async transaction =>
    {
        var snapshot = await transaction.ReadAsync(path, cancellationToken);
        if (snapshot is null) return false;
        var order = snapshot.Data; var expires = order.GetValueOrDefault("reservationExpiresAt");
        if (order.GetValueOrDefault("inventoryReserved") is true && CommerceValues.Text(order, "status") != "Chờ xác nhận" && Js.Truthy(Js.Element(expires)))
        { transaction.Update(path, new() { ["reservationExpiresAt"] = StoreValue.Delete }); return false; }
        if (order.GetValueOrDefault("inventoryReserved") is not true || expires is not DocumentTimestamp expiration || expiration.Milliseconds > now.Milliseconds) return false;
        var paid = CommerceValues.Text(order, "paymentStatus").Trim().ToLower(CultureInfo.GetCultureInfo("vi-VN"));
        if (order.GetValueOrDefault("paymentVerified") is true || new[] { "đã thanh toán", "paid", "da_thanh_toan", "đã hoàn tiền" }.Contains(paid))
        { transaction.Update(path, new() { ["reservationExpiresAt"] = StoreValue.Delete }); return false; }
        var items = PricingService.AggregateItems(Js.Element(order.GetValueOrDefault("items")));
        var attemptPath = CommerceValues.Text(order, "paymentMethod") == "vnpay" && CommerceValues.Text(order, "vnpTxnRef").Length > 0
            ? "paymentTransactions/" + CommerceValues.Text(order, "vnpTxnRef") : null;
        var attempt = attemptPath is null ? null : await transaction.ReadAsync(attemptPath, cancellationToken);
        var restore = CommerceValues.Text(order, "status") != "Đã giao";
        var products = restore ? await transaction.ReadManyAsync(items.Select(item => "products/" + (string)item["id"]).ToArray(), cancellationToken) : [];
        for (var index = 0; index < products.Count; index++)
            if (products[index] is { } product) transaction.Update(product.Path, new()
                { ["quantity"] = CommerceValues.NumberOrZero(product.Data, "quantity") + Convert.ToDouble(items[index]["quantity"], CultureInfo.InvariantCulture) });
        var status = restore ? "Đã hủy" : CommerceValues.Text(order, "status");
        var location = restore ? "Đơn hàng hết thời hạn giữ sản phẩm" : CommerceValues.Text(order, "currentLocation");
        var history = new List<object>(CommerceValues.Array(order, "trackingHistory"))
            { new Dictionary<string, object> { ["status"] = status, ["location"] = location, ["estimatedDelivery"] = "", ["updatedAt"] = now.Iso } };
        var changes = new Dictionary<string, object>
        {
            ["status"] = status, ["currentLocation"] = location, ["inventoryReserved"] = false,
            ["inventoryState"] = restore ? "released" : "sold", ["reservationExpiredAt"] = now, ["trackingUpdatedAt"] = now, ["trackingHistory"] = history
        };
        if (CommerceValues.Text(order, "paymentMethod") == "qr" && order.GetValueOrDefault("paymentVerified") is not true) changes["paymentStatus"] = "Hết hạn chờ thanh toán";
        else if (CommerceValues.Text(order, "paymentMethod") == "vnpay" && order.GetValueOrDefault("paymentVerified") is not true)
        { changes["paymentStatus"] = "that_bai"; changes["vnpResponseCode"] = "expired"; }
        transaction.Update(path, changes);
        if (attempt is not null && CommerceValues.Text(attempt.Data, "status") == "pending")
            transaction.Update(attempt.Path, new() { ["status"] = "expired", ["processedAt"] = now });
        return true;
    }, cancellationToken);
}
