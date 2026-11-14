using FlowerFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FlowerFlow.Api.Controllers;

[ApiController]
[Route("api/products")]
public sealed class ProductsController(FlowerFlowDbContext db, IConfiguration configuration) : ControllerBase
{
    // Shape intentionally matches the existing Vercel endpoint's { products: [...] } response.
    [HttpGet]
    [ProducesResponseType(typeof(ProductListResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ProductListResponse>> List([FromQuery] string? manage = null, CancellationToken cancellationToken = default)
    {
        // The legacy Vercel route uses manage=1; accept that value and the usual boolean spelling.
        var manageMode = manage == "1" || bool.TryParse(manage, out var parsedManage) && parsedManage;

        if (string.IsNullOrWhiteSpace(configuration.GetConnectionString("FlowerFlow")))
            return Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Catalog database is not configured",
                detail: "Set ConnectionStrings__FlowerFlow to a local development SQL Server connection string.");

        if (manageMode && !User.IsInRole("admin") && !User.IsInRole("owner"))
            return User.Identity?.IsAuthenticated == true ? Forbid() : Challenge();

        var products = await db.Products.AsNoTracking()
            .Where(product => manageMode || (product.Status != "hidden" && product.Status != "archived"))
            .OrderBy(product => product.Name)
            .Select(product => new CatalogProductDto(
                product.Code, product.Code, product.Name, product.Description, product.Meaning,
                product.Category == null ? null : product.Category.Name,
                product.SaleType, product.Unit, product.CurrentPrice, product.Stock,
                product.SameDayDelivery, product.Status,
                product.Tags.Select(tag => new CatalogTagDto(tag.TagType, tag.Value)).ToList(),
                product.Images.OrderBy(image => image.SortOrder).Select(image => image.Url).ToList()))
            .ToListAsync(cancellationToken);

        Response.Headers.CacheControl = manageMode
            ? "private, no-store"
            : "public, max-age=0, s-maxage=0, must-revalidate";
        return Ok(new ProductListResponse(products));
    }
}

public sealed record ProductListResponse(IReadOnlyList<CatalogProductDto> Products);
public sealed record CatalogTagDto(string Type, string Value);
public sealed record CatalogProductDto(
    string Id, string Code, string Name, string? Description, string? Meaning,
    string? Category, string SaleType, string Unit, decimal Price, int Quantity,
    bool SameDayDelivery, string Status, IReadOnlyList<CatalogTagDto> Tags, IReadOnlyList<string?> Images);
