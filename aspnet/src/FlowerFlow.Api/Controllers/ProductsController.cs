using FlowerFlow.Application.Catalog;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace FlowerFlow.Api.Controllers;

[AttributeUsage(AttributeTargets.Class)]
public sealed class FirestoreEndpointAttribute : Attribute;

[ApiController, Route("api/products"), FirestoreEndpoint]
public sealed class ProductsController(ProductsService products, FlowerPricesHttpEndpoint prices) : ControllerBase
{
    [Route("")]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var route = QueryValues("route");
        if (route.Length > 0)
        {
            Response.Headers.CacheControl = "no-store";
            if (route.Length == 1 && route[0] == "flower-prices")
                return await prices.HandleAsync(HttpContext, cancellationToken);
            return NotFound(new { code = "api-not-found", error = "Không tìm thấy API." });
        }
        if (Request.Method != "GET")
        {
            Response.Headers.Allow = "GET";
            return StatusCode(405, new { code = "method-not-allowed", error = "API này chỉ chấp nhận GET." });
        }
        var values = QueryValues("manage");
        var manage = values.Length == 1 && values[0] == "1";
        var result = await products.ListAsync(manage, Request.Headers.Authorization, cancellationToken);
        if (result.Failure is { } failure)
            return StatusCode(failure.Status, new { code = failure.Code, error = failure.Message });
        Response.Headers.CacheControl = manage ? "private, no-store" : "public, max-age=0, s-maxage=0, must-revalidate";
        return Ok(new { products = result.Products });
    }

    private string[] QueryValues(string name)
    {
        // ASP.NET's Query collection merges case variants; JavaScript query keys are case-sensitive.
        var values = new List<string>();
        foreach (var pair in new QueryStringEnumerable(Request.QueryString.Value))
            if (pair.DecodeName().ToString() == name) values.Add(pair.DecodeValue().ToString());
        return values.ToArray();
    }
}
