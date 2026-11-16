using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using FlowerFlow.Application.Catalog;
using FlowerFlow.Application.Commerce;
using FlowerFlow.Application.Identity;
using FlowerFlow.Domain.Models.Compatibility;

namespace FlowerFlow.Application.Orders;

public sealed class PaymentAdminService(ICommerceStore store, OrderAdminIdentity identities, TimeProvider time)
{
    private static bool ValidId(string id) => id.Length is > 0 and <= 128 && !id.Contains('/');
    private static bool ValidReference(string reference) => reference.Length is > 0 and <= 120 && !Regex.IsMatch(reference, @"[\u0000-\u001f\u007f]");
    private static CommerceReply Denied(IdentityFailure failure) => CommerceReply.Error(failure.Status, failure.Code, failure.Message);
    private static Dictionary<string, object> Audit(string id, string action, RequestIdentity actor, Dictionary<string, object> order, string reference) => new()
    {
        ["orderId"] = id, ["action"] = action, ["actorUid"] = actor.Uid, ["actorEmail"] = Js.Cut(FlowerPriceRules.Email(actor), 254),
        ["actorRole"] = "admin", ["amountVnd"] = order["total"], ["paymentReference"] = Js.Cut(CommerceValues.Text(order, "paymentReference"), 120),
        ["reconciliationReference"] = reference, ["previousPaymentStatus"] = Js.Cut(CommerceValues.Text(order, "paymentStatus"), 120),
        ["orderStatus"] = Js.Cut(CommerceValues.Text(order, "status"), 80), ["createdAt"] = StoreValue.ServerTimestamp
    };
    public async Task<CommerceReply> ReconcileAsync(JsonElement body, string? authorization, bool late, CancellationToken cancellationToken)
    {
        var id = Js.StringOrEmpty(Js.Get(body, "orderId")).Trim();
        var reference = Js.StringOrEmpty(Js.Get(body, "reconciliationReference")).Trim();
        var amount = Js.Number(Js.Get(body, "amount"));
        if (!ValidId(id) || !ValidReference(reference) || !Js.SafeInteger(amount) || amount <= 0)
            return CommerceReply.Error(400, "invalid-argument", "Cần mã đơn, mã giao dịch ngân hàng và số tiền đã đối chiếu hợp lệ.");
        try
        {
            var access = await identities.VerifyAsync(authorization, cancellationToken);
            if (access.Failure is { } failure) return Denied(failure);
            var auditId = store.NewId("orderPaymentAudit");
            await store.RunAsync(async transaction =>
            {
                var snapshot = await transaction.ReadAsync("orders/" + id, cancellationToken);
                if (snapshot is null) throw new BackendException("order-not-found", "Không tìm thấy đơn hàng.");
                var order = snapshot.Data;
                if (CommerceValues.Text(order, "paymentMethod") != "qr") throw new BackendException("payment-method-mismatch", "Đơn hàng này không sử dụng chuyển khoản QR.");
                if (order.GetValueOrDefault("paymentVerified") is true) throw new BackendException("payment-already-complete", "Thanh toán của đơn này đã được xác nhận.");
                if (!Js.SafeInteger(Js.Element(order.GetValueOrDefault("total"))) || CommerceValues.Number(order, "total") <= 0 || amount != CommerceValues.Number(order, "total"))
                    throw new BackendException("payment-amount-mismatch", "Số tiền đối chiếu không khớp tổng tiền đơn hàng.");
                var cancelled = CommerceValues.Text(order, "status") == "Đã hủy";
                if (late ? !cancelled : cancelled) throw new BackendException(late ? "order-not-cancelled" : "order-cancelled", late
                    ? "Đơn này chưa bị hủy. Hãy dùng xác minh thanh toán thông thường." : "Đơn này đã bị hủy. Hãy dùng thao tác ghi nhận thanh toán trễ.");
                if (!late && CommerceValues.Text(order, "inventoryState") == "released") throw new BackendException("order-inventory-released", "Đơn đã giải phóng tồn kho. Hãy khôi phục đơn trước khi xác nhận thanh toán.");
                var actor = access.Identity!;
                var changes = new Dictionary<string, object>
                {
                    ["paymentVerified"] = true, ["paymentStatus"] = late ? "Đã nhận tiền sau khi đơn bị hủy – cần khôi phục đơn hoặc hoàn tiền" : "Đã thanh toán",
                    ["paidAt"] = DocumentTimestamp.FromMilliseconds(time.GetUtcNow().ToUnixTimeMilliseconds()).Iso, ["paymentVerifiedAt"] = StoreValue.ServerTimestamp,
                    ["paymentVerifiedBy"] = actor.Uid, ["paymentReconciliationReference"] = reference
                };
                if (late)
                {
                    changes["latePayment"] = true;
                    changes["paymentAttention"] = "Thanh toán đến sau khi đơn đã hủy; cần kiểm tra và xử lý hoàn tiền hoặc khôi phục đơn.";
                }
                transaction.Update(snapshot.Path, changes);
                transaction.Create("orderPaymentAudit/" + auditId, Audit(id, late ? "qr-late-payment-recorded" : "qr-payment-verified", actor, order, reference));
                return true;
            }, cancellationToken);
            return new(200, new() { ["ok"] = true, ["auditId"] = auditId });
        }
        catch (InvalidFirebaseTokenException) { return Expired(); }
        catch (BackendException error) { return CommerceReply.Error(error.Code == "order-not-found" ? 404 : 409, error.Code, error.Message); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { return CommerceReply.Error(503, "payment-reconciliation-unavailable", "Chưa thể ghi nhận đối soát. Vui lòng thử lại."); }
    }
    public async Task<CommerceReply> RefundAsync(JsonElement body, string? authorization, CancellationToken cancellationToken)
    {
        var id = Js.StringOrEmpty(Js.Get(body, "orderId")).Trim(); var reference = Js.StringOrEmpty(Js.Get(body, "refundReference")).Trim();
        if (!ValidId(id) || !ValidReference(reference)) return CommerceReply.Error(400, "invalid-argument", "Cần mã đơn và mã giao dịch hoàn tiền hợp lệ.");
        try
        {
            var access = await identities.VerifyAsync(authorization, cancellationToken);
            if (access.Failure is { } failure) return Denied(failure);
            var auditId = store.NewId("orderPaymentAudit");
            var amount = await store.RunAsync(async transaction =>
            {
                var snapshot = await transaction.ReadAsync("orders/" + id, cancellationToken);
                if (snapshot is null) throw new BackendException("order-not-found", "Không tìm thấy đơn hàng.");
                var order = snapshot.Data;
                if (order.GetValueOrDefault("paymentRefunded") is true) throw new BackendException("payment-already-refunded", "Đơn hàng này đã được ghi nhận hoàn tiền.");
                var status = CommerceValues.Text(order, "paymentStatus").Trim().ToLower(CultureInfo.GetCultureInfo("vi-VN"));
                if (order.GetValueOrDefault("paymentVerified") is not true && !new[] { "đã thanh toán", "paid", "da_thanh_toan" }.Contains(status))
                    throw new BackendException("payment-not-complete", "Chỉ ghi nhận hoàn tiền cho đơn đã xác nhận thanh toán.");
                if (!Js.SafeInteger(Js.Element(order.GetValueOrDefault("total"))) || CommerceValues.Number(order, "total") <= 0)
                    throw new BackendException("invalid-refund-amount", "Tổng tiền đơn hàng không hợp lệ để hoàn tiền.");
                transaction.Update(snapshot.Path, new()
                {
                    ["paymentRefunded"] = true, ["paymentRefundAmount"] = order["total"], ["paymentRefundedAt"] = StoreValue.ServerTimestamp,
                    ["paymentRefundedBy"] = access.Identity!.Uid, ["paymentRefundReference"] = reference, ["paymentStatus"] = "Đã hoàn tiền"
                });
                transaction.Create("orderPaymentAudit/" + auditId, Audit(id, "payment-refunded", access.Identity!, order, reference));
                return order["total"];
            }, cancellationToken);
            return new(200, new() { ["ok"] = true, ["auditId"] = auditId, ["amountVnd"] = amount });
        }
        catch (InvalidFirebaseTokenException) { return Expired(); }
        catch (BackendException error) { return CommerceReply.Error(error.Code == "order-not-found" ? 404 : 409, error.Code, error.Message); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { return CommerceReply.Error(503, "payment-refund-unavailable", "Chưa thể ghi nhận hoàn tiền. Vui lòng thử lại."); }
    }
    public async Task<CommerceReply> AuditAsync(string id, string? authorization, CancellationToken cancellationToken)
    {
        id = id.Trim();
        if (!ValidId(id)) return CommerceReply.Error(400, "invalid-argument", "Mã đơn không hợp lệ.");
        try
        {
            var access = await identities.VerifyAsync(authorization, cancellationToken);
            if (access.Failure is { } failure) return Denied(failure);
            var records = await store.QueryAsync(new("orderPaymentAudit", [new("orderId", "==", id)], "createdAt", true, 25), cancellationToken);
            var names = new[] { "action", "actorUid", "actorEmail", "actorRole", "amountVnd", "paymentReference", "reconciliationReference", "previousPaymentStatus", "orderStatus" };
            var entries = records.Select(document =>
            {
                var data = new Dictionary<string, object> { ["id"] = document.Id };
                foreach (var name in names) data[name] = document.Data.GetValueOrDefault(name, default(JsonElement));
                data["createdAt"] = CommerceValues.Timestamp(document.Data.GetValueOrDefault("createdAt")); return data;
            }).ToList();
            return new(200, new() { ["entries"] = entries });
        }
        catch (InvalidFirebaseTokenException) { return Expired(); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { return CommerceReply.Error(503, "payment-audit-unavailable", "Chưa thể tải lịch sử đối soát. Vui lòng thử lại."); }
    }
    private static CommerceReply Expired() => CommerceReply.Error(401, "unauthenticated", "Phiên đăng nhập đã hết hạn. Hãy đăng nhập lại.");
}
