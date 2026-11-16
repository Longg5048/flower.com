using System.Text.Json;
using System.Text.Json.Nodes;
using FlowerFlow.Application.Commerce;
using FlowerFlow.Application.Payments;
using FlowerFlow.Domain.Models.Compatibility;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace FlowerFlow.Api.Controllers;

[ApiController, Route("api/payments"), FirestoreEndpoint]
public sealed class PaymentsController(PaymentsService payments) : ControllerBase
{
    [Route("")]
    public async Task<IActionResult> Handle(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        CommerceReply result;
        var query = PaymentQuery.Read(Request);
        if (Request.Method == "GET" && query["action"] is JsonValue value && value.TryGetValue<string>(out var action) && action == "status")
            result = await payments.StatusAsync(Js.StringOrEmpty(JsonSerializer.SerializeToElement(query["txnRef"])), cancellationToken);
        else
        {
            JsonElement body = default;
            if (Request.Method == "POST")
            {
                try { if (Request.ContentLength is not 0) { using var document = await JsonDocument.ParseAsync(Request.Body, cancellationToken: cancellationToken); body = document.RootElement.Clone(); } }
                catch (JsonException) { }
            }
            var operation = Js.Get(body, "action");
            result = Request.Method == "POST" && operation.ValueKind == JsonValueKind.String && operation.GetString() is "create" or "retry"
                ? await payments.CreateAsync(body, operation.GetString() == "retry", HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken)
                : CommerceReply.Error(405, "method-not-allowed", "API thanh toán chỉ chấp nhận GET hoặc POST.", allow: "GET, POST");
        }
        if (result.Allow is { } allow) Response.Headers.Allow = allow;
        return StatusCode(result.Status, result.Json);
    }
}

[ApiController, Route("api/vnpay-ipn"), FirestoreEndpoint]
public sealed class VnpayIpnController(VnpayIpnService ipn) : ControllerBase
{
    [Route("")]
    public async Task<IActionResult> Handle(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        if (Request.Method != "GET")
        {
            Response.Headers.Allow = "GET";
            return StatusCode(405, new { code = "method-not-allowed", error = "VNPay IPN chỉ chấp nhận GET." });
        }
        var result = await ipn.ProcessAsync(PaymentQuery.Read(Request), cancellationToken);
        return StatusCode(result.Status, result.Json);
    }
}

internal static class PaymentQuery
{
    public static JsonObject Read(HttpRequest request)
    {
        var parameters = new JsonObject();
        foreach (var pair in new QueryStringEnumerable(request.QueryString.Value))
        {
            var key = pair.DecodeName().ToString(); var value = pair.DecodeValue().ToString();
            if (!parameters.ContainsKey(key)) parameters[key] = value;
            else if (parameters[key] is JsonArray array) array.Add(value);
            else parameters[key] = new JsonArray(parameters[key]!.DeepClone(), JsonValue.Create(value));
        }
        return parameters;
    }
}
