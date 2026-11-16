using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using FlowerFlow.Application.Commerce;
using FlowerFlow.Application.Orders;
using FlowerFlow.Domain.Models.Compatibility;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace FlowerFlow.Api.Controllers;

[ApiController, Route("api/orders"), FirestoreEndpoint]
public sealed class OrdersController(CheckoutService checkout, TrackingService tracking, ReviewsService reviews,
    PaymentAdminService payments, ReservationsService reservations, OrderAdminIdentity admin, OrderSecurityOptions security) : ControllerBase
{
    [Route("")]
    public async Task<IActionResult> Handle(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        CommerceReply result;
        var context = new OrderRequestContext(Request.Headers.Authorization, HttpContext.Connection.RemoteIpAddress?.ToString());
        if (Request.Method == "POST")
        {
            JsonElement body = default;
            try
            {
                if (Request.ContentLength is not 0)
                { using var document = await JsonDocument.ParseAsync(Request.Body, cancellationToken: cancellationToken); body = document.RootElement.Clone(); }
            }
            catch (JsonException) { }
            result = Js.Get(body, "action").ValueKind == JsonValueKind.String ? Js.Get(body, "action").GetString() switch
            {
                "quote" => await checkout.QuoteAsync(body, cancellationToken),
                "lookup" => await tracking.LookupAsync(body, context, cancellationToken),
                "review" => await reviews.SubmitAsync(body, context.Authorization, cancellationToken),
                "review-get" => await reviews.OwnAsync(body, context.Authorization, cancellationToken),
                "verify-qr-payment" => await payments.ReconcileAsync(body, context.Authorization, false, cancellationToken),
                "record-late-qr-payment" => await payments.ReconcileAsync(body, context.Authorization, true, cancellationToken),
                "record-payment-refund" => await payments.RefundAsync(body, context.Authorization, cancellationToken),
                _ => await checkout.CreateAsync(body, context, cancellationToken)
            } : await checkout.CreateAsync(body, context, cancellationToken);
        }
        else if (Request.Method != "GET") result = CommerceReply.Error(405, "method-not-allowed", "API đơn hàng chỉ chấp nhận phương thức GET hoặc POST.", allow: "GET, POST");
        else result = Query("action") switch
        {
            "mine" => await tracking.MineAsync(context.Authorization, cancellationToken),
            "payment-audit" => await payments.AuditAsync(Query("orderId") ?? "", context.Authorization, cancellationToken),
            "expire" => await ExpireAsync(context.Authorization, cancellationToken),
            _ => CommerceReply.Error(405, "method-not-allowed", "Tra cứu đơn hàng cần dùng phương thức POST.")
        };
        if (result.RetryAfter is { } retry) Response.Headers.RetryAfter = retry.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (result.Allow is { } allow) Response.Headers.Allow = allow;
        return StatusCode(result.Status, result.Json);
    }
    private async Task<CommerceReply> ExpireAsync(string? authorization, CancellationToken cancellationToken)
    {
        var match = Regex.Match(authorization ?? "", @"^Bearer\s+(.+)$", RegexOptions.IgnoreCase);
        var token = match.Success ? match.Groups[1].Value : "";
        var secret = security.CronSecret;
        var authorized = !string.IsNullOrEmpty(secret) && token.Length == secret.Length
            && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(token), Encoding.UTF8.GetBytes(secret));
        if (!authorized && token.Length > 0)
        {
            var identity = await admin.VerifyAsync(authorization, cancellationToken);
            if (identity.Failure is { } denied) return CommerceReply.Error(denied.Status, denied.Code, denied.Message);
            authorized = true;
        }
        if (!authorized) return CommerceReply.Error(401, "unauthenticated", "Quyền dọn đơn hết hạn không hợp lệ.");
        try { return new(200, new() { ["released"] = await reservations.ExpireAsync(500, cancellationToken) }); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { return CommerceReply.Error(503, "order-service-unavailable", "Chưa thể giải phóng tồn kho đơn hết hạn."); }
    }
    private string? Query(string key)
    {
        var values = new List<string>();
        foreach (var pair in new QueryStringEnumerable(Request.QueryString.Value)) if (pair.DecodeName().ToString() == key) values.Add(pair.DecodeValue().ToString());
        return values.Count == 1 ? values[0] : null;
    }
}
