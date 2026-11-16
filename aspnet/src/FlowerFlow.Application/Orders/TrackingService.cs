using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using FlowerFlow.Application.Commerce;
using FlowerFlow.Application.Identity;
using FlowerFlow.Domain.Models.Compatibility;

namespace FlowerFlow.Application.Orders;

public sealed class TrackingService(ICommerceStore store, CustomerIdentityService identities, OrderSecurityOptions security, TimeProvider time)
{
    public static bool AwaitsQr(Dictionary<string, object> data) => CommerceValues.Text(data, "paymentMethod") == "qr"
        && data.GetValueOrDefault("paymentVerified") is not true && new[] { "Chờ xác nhận", "Đang chuẩn bị" }.Contains(CommerceValues.Text(data, "status", "Chờ xác nhận"));
    public static Dictionary<string, object> AccountOrder(string id, Dictionary<string, object> data) => new(data)
    {
        ["id"] = id, ["createdAt"] = TimestampOrOriginal(data.GetValueOrDefault("createdAt")),
        ["reservationExpiresAt"] = TimestampOrOriginal(data.GetValueOrDefault("reservationExpiresAt")),
        ["trackingHistory"] = CommerceValues.Array(data, "trackingHistory").Select(item =>
        {
            var entry = CommerceValues.Object(item);
            return (object)new Dictionary<string, object>(entry) { ["updatedAt"] = TimestampOrOriginal(entry.GetValueOrDefault("updatedAt")) };
        }).ToList()
    };
    private static object TimestampOrOriginal(object? value) => CommerceValues.Timestamp(value) is { Length: > 0 } text ? text : value is not null && Js.Truthy(Js.Element(value)) ? value : "";
    public static Dictionary<string, object> PublicOrder(string id, Dictionary<string, object> data)
    {
        var result = new Dictionary<string, object>
        {
            ["id"] = id, ["status"] = CommerceValues.Or(data, "status", "Chờ xác nhận"), ["estimatedDelivery"] = CommerceValues.Or(data, "estimatedDelivery", ""),
            ["trackingHistory"] = CommerceValues.Array(data, "trackingHistory").Select(item =>
            {
                var entry = CommerceValues.Object(item);
                return (object)new Dictionary<string, object> { ["status"] = CommerceValues.Or(entry, "status", CommerceValues.Or(data, "status", "Chờ xác nhận")),
                    ["estimatedDelivery"] = CommerceValues.Or(entry, "estimatedDelivery", ""), ["updatedAt"] = TimestampOrOriginal(entry.GetValueOrDefault("updatedAt")) };
            }).ToList(), ["privacyLimited"] = true
        };
        if (data.GetValueOrDefault("paymentRefunded") is true)
        { result["paymentMethod"] = data.GetValueOrDefault("paymentMethod") ?? ""; result["paymentStatus"] = "Đã hoàn tiền"; }
        else if (AwaitsQr(data))
        {
            result["paymentMethod"] = "qr"; result["paymentStatus"] = CommerceValues.Text(data, "paymentStatus", "Chờ xác minh thanh toán");
            result["total"] = CommerceValues.NumberOrZero(data, "total"); result["wrappingFee"] = CommerceValues.NumberOrZero(data, "wrappingFee");
            result["paymentReference"] = data.GetValueOrDefault("paymentReference") ?? ""; result["reservationExpiresAt"] = CommerceValues.Timestamp(data.GetValueOrDefault("reservationExpiresAt"));
        }
        else if (CommerceValues.Text(data, "paymentMethod") == "vnpay")
        { result["paymentMethod"] = "vnpay"; result["paymentStatus"] = CommerceValues.Text(data, "paymentStatus", "chua_thanh_toan"); }
        return result;
    }
    public static string Hmac(string secret, string value) => Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(value)));
    public async Task<CommerceReply> LookupAsync(JsonElement body, OrderRequestContext request, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrEmpty(request.ClientIp) || request.ClientIp.Length > 64 || string.IsNullOrEmpty(security.Secret)) throw new InvalidOperationException("Trusted order source is unavailable.");
            var now = DocumentTimestamp.FromMilliseconds(time.GetUtcNow().ToUnixTimeMilliseconds());
            var path = "orderLookupLimits/" + Hmac(security.Secret, request.ClientIp);
            var retry = await store.RunAsync(async transaction =>
            {
                var record = (await transaction.ReadAsync(path, cancellationToken))?.Data ?? new();
                var start = CommerceValues.Milliseconds(record.GetValueOrDefault("windowStartedAt"));
                var same = now.Milliseconds - start < OrderConfig.OrderLookupWindowMs;
                var attempts = same ? CommerceValues.NumberOrZero(record, "attempts") : 0;
                if (attempts >= OrderConfig.OrderLookupMaxAttempts) return (int)Math.Max(1, Math.Ceiling((start + OrderConfig.OrderLookupWindowMs - now.Milliseconds) / 1000));
                transaction.Set(path, new() { ["windowStartedAt"] = same ? record["windowStartedAt"] : now,
                    ["attempts"] = attempts + 1, ["expiresAt"] = DocumentTimestamp.FromMilliseconds(now.Milliseconds + 86_400_000) });
                return 0;
            }, cancellationToken);
            if (retry > 0) return CommerceReply.Error(429, "rate-limited", "Bạn đã tra cứu quá nhiều lần. Vui lòng thử lại sau.", retry);
            RequestIdentity? user = null;
            var authorization = request.Authorization?.Trim() ?? "";
            if (authorization.Length > 0)
            {
                if (!Regex.IsMatch(authorization, @"\ABearer\s+\S+\z", RegexOptions.IgnoreCase)) return CommerceReply.Error(401, "unauthenticated", "Phiên đăng nhập không hợp lệ.");
                try { user = await identities.ReadAsync(request.Authorization, cancellationToken); }
                catch (InvalidFirebaseTokenException) { return CommerceReply.Error(401, "unauthenticated", "Phiên đăng nhập đã hết hạn. Hãy đăng nhập lại."); }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                catch (Exception) { return CommerceReply.Error(503, "order-service-unavailable", "Chưa thể xác minh phiên đăng nhập. Vui lòng thử lại."); }
            }
            var id = Js.StringOrEmpty(Js.Get(body, "id")).Trim(); if (id.StartsWith('#')) id = id[1..];
            var phone = OrderValidationService.NormalizePhone(Js.Get(body, "phone"));
            if (id.Length is < 1 or > 128 || id.Contains('/') || user is null && phone.Length is < 8 or > 15)
                return CommerceReply.Error(400, "invalid-argument", "Mã đơn hàng hoặc số điện thoại không hợp lệ.");
            var order = await store.ReadAsync("orders/" + id, cancellationToken);
            if (order is null) return Missing(false);
            if (user is not null)
                return CommerceValues.Text(order.Data, "uid") != user.Uid ? Missing(true) : new(200, new() { ["order"] = AccountOrder(id, order.Data) });
            if (OrderValidationService.NormalizePhone(Js.Element(order.Data.GetValueOrDefault("phone"))) != phone) return Missing(false);
            return new(200, new() { ["order"] = PublicOrder(id, order.Data) });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { return CommerceReply.Error(503, "order-service-unavailable", "Chưa thể tra cứu đơn hàng. Vui lòng thử lại sau."); }
    }
    public async Task<CommerceReply> MineAsync(string? authorization, CancellationToken cancellationToken)
    {
        try
        {
            var user = await identities.ReadAsync(authorization, cancellationToken);
            if (user is null) return CommerceReply.Error(401, "unauthenticated", "Đăng nhập để xem đơn hàng.");
            var orders = await store.QueryAsync(new("orders", [new("uid", "==", user.Uid)], "createdAt", true, 100), cancellationToken);
            return new(200, new() { ["accountUid"] = user.Uid, ["orders"] = orders.Select(order => AccountOrder(order.Id, order.Data)).ToList() });
        }
        catch (InvalidFirebaseTokenException) { return CommerceReply.Error(401, "unauthenticated", "Phiên đăng nhập hết hạn."); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { return CommerceReply.Error(503, "order-service-unavailable", "Chưa thể tải danh sách đơn hàng."); }
    }
    private static CommerceReply Missing(bool account) => CommerceReply.Error(404, "order-not-found", account ? "Không tìm thấy đơn hàng trong tài khoản."
        : "Không tìm thấy đơn hàng. Hãy kiểm tra lại mã đơn và số điện thoại.");
}
