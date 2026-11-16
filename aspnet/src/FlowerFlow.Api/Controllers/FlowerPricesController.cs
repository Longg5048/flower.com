using System.Text.Json;
using System.Text.Json.Nodes;
using FlowerFlow.Application.Catalog;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace FlowerFlow.Api.Controllers;

[ApiController, Route("api/flower-prices"), FirestoreEndpoint]
public sealed class FlowerPricesController(FlowerPricesHttpEndpoint endpoint) : ControllerBase
{
    [Route("")]
    public Task<IActionResult> Handle(CancellationToken cancellationToken) => endpoint.HandleAsync(HttpContext, cancellationToken);
}

// Both route forms execute the same transport adapter and application use case.
public sealed class FlowerPricesHttpEndpoint(FlowerPricesService prices)
{
    public async Task<IActionResult> HandleAsync(HttpContext context, CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "private, no-store";
        if (context.Request.Method is not ("GET" or "PATCH"))
        {
            context.Response.Headers.Allow = "GET, PATCH";
            return Failure(new(405, "method-not-allowed", "API chỉ chấp nhận GET hoặc PATCH."));
        }
        var access = await prices.AuthorizeAsync(context.Request.Headers.Authorization, cancellationToken);
        if (access.Failure is { } denied) return Failure(denied);
        FlowerPriceResult result;
        if (context.Request.Method == "GET")
        {
            var ids = new List<string>();
            foreach (var pair in new QueryStringEnumerable(context.Request.QueryString.Value))
                if (pair.DecodeName().ToString() == "flowerId") ids.Add(pair.DecodeValue().ToString());
            result = ids.Count == 0 ? await prices.ListAsync(cancellationToken)
                : await prices.HistoryAsync(ids.Count == 1 ? ids[0] : null, cancellationToken);
        }
        else
        {
            JsonNode? body = null;
            try
            {
                if (context.Request.ContentLength is not 0)
                    body = await JsonNode.ParseAsync(context.Request.Body, cancellationToken: cancellationToken);
            }
            catch (JsonException)
            {
                // Invalid JSON is a transport error; do not expose parser diagnostics.
                return Failure(new(400, "invalid-argument", "Mã hoa hoặc giá bán không hợp lệ."));
            }
            result = await prices.UpdateAsync(access.Identity!, body, cancellationToken);
        }
        return result.Failure is { } failure ? Failure(failure) : new OkObjectResult(result.Body);
    }

    private static IActionResult Failure(ProductFailure failure) => new ObjectResult(new { code = failure.Code, error = failure.Message })
        { StatusCode = failure.Status };
}
