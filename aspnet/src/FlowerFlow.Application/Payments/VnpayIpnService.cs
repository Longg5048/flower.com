using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using FlowerFlow.Application.Commerce;
using FlowerFlow.Application.Orders;
using FlowerFlow.Domain.Models;
using FlowerFlow.Domain.Models.Compatibility;

namespace FlowerFlow.Application.Payments;

public sealed class VnpayIpnService(ICommerceStore store, ReservationsService reservations, IVnpayGateway gateway, TimeProvider time)
{
    private static CommerceReply Reply(string code, string message) => new(200, new() { ["RspCode"] = code, ["Message"] = message });
    public async Task<CommerceReply> ProcessAsync(JsonObject parameters, CancellationToken cancellationToken)
    {
        VnpaySecrets secrets;
        try { secrets = gateway.Secrets(); } catch (Exception) { return Reply("99", "Configuration error"); }
        var values = JsonSerializer.SerializeToElement(parameters);
        string Text(string key) => Js.StringOrEmpty(Js.Get(values, key));
        bool Is(string key, string expected) => Js.Get(values, key).ValueKind == JsonValueKind.String && Js.Get(values, key).GetString() == expected;
        if (!gateway.Verify(parameters, secrets.HashSecret) || Text("vnp_TmnCode") != secrets.TmnCode) return Reply("97", "Invalid signature");
        var txnRef = Text("vnp_TxnRef");
        if (!Regex.IsMatch(txnRef, @"\A[a-f0-9]{32}\z")) return Reply("01", "Order not found");
        try
        {
            await reservations.ExpireAsync(500, cancellationToken);
            return await store.RunAsync(async transaction =>
            {
                var attemptDocument = await transaction.ReadAsync("paymentTransactions/" + txnRef, cancellationToken);
                if (attemptDocument is null) return Reply("01", "Order not found");
                var attempt = attemptDocument.Data; var amount = CommerceValues.Number(attempt, "amount");
                if (attempt.GetValueOrDefault("orderId") is not string id || id.Length == 0 || !Js.SafeInteger(Js.Element(attempt.GetValueOrDefault("amount"))) || amount <= 0 || !Js.SafeInteger(amount * 100))
                    return Reply("01", "Order not found");
                var orderDocument = await transaction.ReadAsync("orders/" + id, cancellationToken);
                if (orderDocument is null || CommerceValues.Text(orderDocument.Data, "paymentMethod") != "vnpay") return Reply("01", "Order not found");
                var order = orderDocument.Data; var total = CommerceValues.Number(order, "total");
                var reported = Text("vnp_Amount"); var parsedAmount = Js.Number(Js.Element(reported));
                if (!Is("vnp_CurrCode", "VND") || !Regex.IsMatch(reported, @"\A[0-9]+\z") || !Js.SafeInteger(parsedAmount) || parsedAmount != amount * 100
                    || !Js.SafeInteger(Js.Element(order.GetValueOrDefault("total"))) || !Js.SafeInteger(total * 100) || parsedAmount != total * 100)
                    return Reply("04", "Invalid amount");
                if (CommerceValues.Text(attempt, "status") == "success") return Reply("00", "Confirm Success");
                var paid = Is("vnp_ResponseCode", "00") && Is("vnp_TransactionStatus", "00");
                var late = paid && (order.GetValueOrDefault("inventoryReserved") is not true || CommerceValues.Text(order, "status") == "Đã hủy");
                var update = new Dictionary<string, object>
                {
                    ["status"] = paid ? "success" : "failed", ["responseCode"] = Text("vnp_ResponseCode"), ["transactionStatus"] = Text("vnp_TransactionStatus"),
                    ["transactionNo"] = Js.Cut(Text("vnp_TransactionNo"), 100), ["processedAt"] = StoreValue.ServerTimestamp
                };
                if (late) update["latePayment"] = true;
                transaction.Update(attemptDocument.Path, update);
                if (paid && order.GetValueOrDefault("paymentVerified") is not true)
                {
                    var changes = new Dictionary<string, object>
                    {
                        ["paymentStatus"] = "da_thanh_toan", ["paymentVerified"] = true,
                        ["paidAt"] = DocumentTimestamp.FromMilliseconds(time.GetUtcNow().ToUnixTimeMilliseconds()).Iso,
                        ["vnpTransactionNo"] = Js.Cut(Text("vnp_TransactionNo"), 100), ["vnpTxnRef"] = txnRef
                    };
                    if (late) { changes["latePayment"] = true; changes["paymentAttention"] = "Thanh toán đến sau khi đơn đã hủy; cần kiểm tra và xử lý hoàn tiền hoặc khôi phục đơn."; }
                    transaction.Update(orderDocument.Path, changes);
                }
                else if (!paid && order.GetValueOrDefault("paymentVerified") is not true && CommerceValues.Text(order, "vnpTxnRef") == txnRef)
                    transaction.Update(orderDocument.Path, new() { ["paymentStatus"] = "that_bai", ["vnpResponseCode"] = Js.Cut(Text("vnp_ResponseCode"), 10) });
                else if (paid && order.GetValueOrDefault("paymentVerified") is true)
                {
                    transaction.Update(attemptDocument.Path, new() { ["latePayment"] = true });
                    transaction.Update(orderDocument.Path, new() { ["latePayment"] = true,
                        ["paymentAttention"] = "Có thêm giao dịch VNPay thành công cho đơn đã thanh toán; cần kiểm tra và xử lý hoàn tiền." });
                }
                return Reply("00", "Confirm Success");
            }, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { return Reply("99", "Unknown error"); }
    }
}
