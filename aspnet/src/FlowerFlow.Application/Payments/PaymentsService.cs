using System.Text.Json;
using System.Text.RegularExpressions;
using FlowerFlow.Application.Commerce;
using FlowerFlow.Application.Orders;
using FlowerFlow.Domain.Models;
using FlowerFlow.Domain.Models.Compatibility;

namespace FlowerFlow.Application.Payments;

public sealed record PaymentAttempt(string TxnRef, double Amount, DocumentTimestamp CreatedAt, DocumentTimestamp ExpiresAt);
public sealed class PaymentsService(ICommerceStore store, ReservationsService reservations, IVnpayGateway gateway, ICommerceRandom random, TimeProvider time)
{
    public async Task<PaymentAttempt> CreateOrReuseAsync(string id, string reference, string previousTxnRef, CancellationToken cancellationToken)
    {
        await reservations.ExpireAsync(500, cancellationToken);
        return await store.RunAsync(async transaction =>
        {
            var document = await transaction.ReadAsync("orders/" + id, cancellationToken);
            if (document is null) throw new BackendException("order-not-found", "Không tìm thấy đơn hàng.");
            var order = document.Data;
            if (previousTxnRef.Length > 0)
            {
                var previous = await transaction.ReadAsync("paymentTransactions/" + previousTxnRef, cancellationToken);
                if (previous is null || CommerceValues.Text(previous.Data, "orderId") != id) throw new BackendException("order-not-found", "Không tìm thấy giao dịch thanh toán.");
            }
            else if (CommerceValues.Text(order, "paymentReference") != reference) throw new BackendException("order-not-found", "Không tìm thấy giao dịch thanh toán.");
            if (CommerceValues.Text(order, "paymentMethod") != "vnpay") throw new BackendException("payment-not-available", "Đơn hàng này không sử dụng thanh toán VNPay.");
            if (order.GetValueOrDefault("paymentVerified") is true) throw new BackendException("payment-already-complete", "Đơn hàng đã được xác nhận thanh toán.");
            var now = DocumentTimestamp.FromMilliseconds(time.GetUtcNow().ToUnixTimeMilliseconds());
            var currentRef = CommerceValues.Text(order, "vnpTxnRef");
            if (currentRef.Length > 0)
            {
                var current = await transaction.ReadAsync("paymentTransactions/" + currentRef, cancellationToken);
                if (current is not null && CommerceValues.Text(current.Data, "status") == "pending"
                    && current.Data.GetValueOrDefault("expiresAt") is DocumentTimestamp expires && expires.Milliseconds > now.Milliseconds
                    && order.GetValueOrDefault("inventoryReserved") is true && CommerceValues.Milliseconds(order.GetValueOrDefault("reservationExpiresAt")) > now.Milliseconds)
                {
                    if (!Js.SafeInteger(Js.Element(current.Data.GetValueOrDefault("amount")))) throw new VnpayException("invalid-payment", "Thông tin giao dịch VNPay không hợp lệ.");
                    return new(currentRef, CommerceValues.Number(current.Data, "amount"), current.Data.GetValueOrDefault("createdAt") as DocumentTimestamp ?? now, expires);
                }
            }
            var items = PricingService.AggregateItems(Js.Element(order.GetValueOrDefault("items")));
            var amount = CommerceValues.Number(order, "total");
            if (items.Count == 0 || !Js.SafeInteger(Js.Element(order.GetValueOrDefault("total"))) || !Js.SafeInteger(amount * 100) || amount <= 0)
                throw new BackendException("invalid-payment", "Đơn hàng không có số tiền thanh toán hợp lệ.");
            var reserved = order.GetValueOrDefault("inventoryReserved") is true;
            if (!reserved && !(CommerceValues.Text(order, "status") == "Đã hủy" && CommerceValues.Text(order, "paymentStatus") == "that_bai"
                && Js.Truthy(Js.Element(order.GetValueOrDefault("reservationExpiredAt")))))
                throw new BackendException("payment-not-available", "Đơn hàng không còn ở trạng thái có thể thanh toán.");
            var products = reserved ? [] : await transaction.ReadManyAsync(items.Select(item => "products/" + (string)item["id"]).ToArray(), cancellationToken);
            for (var index = 0; index < products.Count; index++)
            {
                var available = products[index] is { } product ? CommerceValues.Number(product.Data, "quantity") : 0;
                if (!Js.Integer(available) || available < Convert.ToDouble(items[index]["quantity"], System.Globalization.CultureInfo.InvariantCulture))
                    throw new BackendException("insufficient-stock", "Một số sản phẩm đã hết hàng. Hãy quay lại giỏ hàng để cập nhật đơn.");
            }
            var txnRef = random.Hex(16);
            var expiresAt = reserved && order.GetValueOrDefault("reservationExpiresAt") is DocumentTimestamp reservation && reservation.Milliseconds > now.Milliseconds
                ? reservation : DocumentTimestamp.FromMilliseconds(now.Milliseconds + 900_000);
            var attempt = Math.Max(0, CommerceValues.NumberOrZero(order, "vnpPaymentAttempt")) + 1;
            if (!reserved)
                for (var index = 0; index < products.Count; index++) transaction.Update(products[index]!.Path,
                    new() { ["quantity"] = CommerceValues.Number(products[index]!.Data, "quantity") - Convert.ToDouble(items[index]["quantity"], System.Globalization.CultureInfo.InvariantCulture) });
            transaction.Create("paymentTransactions/" + txnRef, new()
                { ["orderId"] = id, ["amount"] = amount, ["status"] = "pending", ["attempt"] = attempt, ["createdAt"] = now, ["expiresAt"] = expiresAt });
            var changes = new Dictionary<string, object>
            { ["paymentStatus"] = "chua_thanh_toan", ["vnpTxnRef"] = txnRef, ["vnpPaymentAttempt"] = attempt, ["vnpPaymentCreatedAt"] = StoreValue.ServerTimestamp };
            if (!reserved)
            {
                changes["status"] = "Chờ xác nhận"; changes["currentLocation"] = "Đơn hàng đang chờ thanh toán lại";
                changes["inventoryReserved"] = true; changes["inventoryState"] = "reserved"; changes["reservationExpiresAt"] = expiresAt; changes["reservationExpiredAt"] = StoreValue.Delete;
                changes["trackingHistory"] = new List<object>(CommerceValues.Array(order, "trackingHistory"))
                { new Dictionary<string, object> { ["status"] = "Chờ xác nhận", ["location"] = "Đơn hàng đang chờ thanh toán lại", ["estimatedDelivery"] = "", ["updatedAt"] = now.Iso } };
            }
            transaction.Update(document.Path, changes);
            return new PaymentAttempt(txnRef, amount, now, expiresAt);
        }, cancellationToken);
    }
    public async Task<CommerceReply> CreateAsync(JsonElement body, bool retry, string? clientIp, CancellationToken cancellationToken)
    {
        try
        {
            var configuration = gateway.Configuration();
            var previous = retry ? Js.StringOrEmpty(Js.Get(body, "txnRef")) : "";
            if (retry && !Regex.IsMatch(previous, @"\A[a-f0-9]{32}\z")) throw new BackendException("invalid-payment", "Mã giao dịch thanh toán không hợp lệ.");
            var id = retry ? "" : Js.StringOrEmpty(Js.Get(body, "orderId")).Trim();
            var reference = retry ? "" : Js.StringOrEmpty(Js.Get(body, "paymentReference")).Trim();
            if (!retry && (id.Length is < 1 or > 128 || id.Contains('/') || reference.Length is < 1 or > 64))
                throw new BackendException("invalid-payment", "Thông tin đơn hàng không hợp lệ.");
            if (retry)
            {
                var document = await store.ReadAsync("paymentTransactions/" + previous, cancellationToken);
                if (document is null) throw new BackendException("order-not-found", "Không tìm thấy giao dịch thanh toán.");
                id = CommerceValues.Text(document.Data, "orderId");
            }
            var attempt = await CreateOrReuseAsync(id, reference, previous, cancellationToken);
            var url = gateway.PaymentUrl(attempt.TxnRef, attempt.Amount, clientIp ?? "127.0.0.1", attempt.CreatedAt, attempt.ExpiresAt, configuration);
            return new(200, new() { ["paymentUrl"] = url, ["expiresAt"] = attempt.ExpiresAt.Iso });
        }
        catch (VnpayException error) { return error.Code == "vnpay-not-configured" ? ConfigurationFailure() : CommerceReply.Error(400, error.Code, error.Message); }
        catch (BackendException error) { return CommerceReply.Error(error.Code == "order-not-found" ? 404 : error.Code == "invalid-payment" ? 400 : 409, error.Code, error.Message); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { return CommerceReply.Error(503, "payment-service-unavailable", "Chưa thể tạo liên kết thanh toán. Vui lòng thử lại sau."); }
    }
    public async Task<CommerceReply> StatusAsync(string txnRef, CancellationToken cancellationToken)
    {
        if (!Regex.IsMatch(txnRef, @"\A[a-f0-9]{32}\z")) return CommerceReply.Error(400, "invalid-payment", "Mã giao dịch thanh toán không hợp lệ.");
        try
        {
            var document = await store.ReadAsync("paymentTransactions/" + txnRef, cancellationToken);
            if (document is null) return CommerceReply.Error(404, "payment-not-found", "Không tìm thấy giao dịch thanh toán.");
            var payment = document.Data; var status = CommerceValues.Text(payment, "status");
            var expired = status == "pending" && payment.GetValueOrDefault("expiresAt") is DocumentTimestamp expiration && expiration.Milliseconds <= time.GetUtcNow().ToUnixTimeMilliseconds();
            return new(200, new() { ["status"] = expired ? "expired" : payment.GetValueOrDefault("status", default(JsonElement)),
                ["orderId"] = payment.GetValueOrDefault("orderId", default(JsonElement)), ["retryAllowed"] = status != "success", ["latePayment"] = payment.GetValueOrDefault("latePayment") is true });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { return CommerceReply.Error(503, "payment-service-unavailable", "Chưa thể kiểm tra trạng thái thanh toán."); }
    }
    private static CommerceReply ConfigurationFailure() => CommerceReply.Error(503, "vnpay-not-configured", "Thanh toán VNPay chưa được cấu hình. Vui lòng chọn thanh toán khi nhận hàng hoặc thử lại sau.");
}
