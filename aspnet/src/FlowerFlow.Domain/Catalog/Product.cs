namespace FlowerFlow.Domain.Catalog;

public sealed class Product
{
    public int Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public string? Meaning { get; set; }
    public int? CategoryId { get; set; }
    public Category? Category { get; set; }
    public required string SaleType { get; set; }
    public required string Unit { get; set; }
    public decimal CurrentPrice { get; set; }
    public long? CurrentPriceId { get; set; }
    public ProductPrice? CurrentPriceRecord { get; set; }
    public int Stock { get; set; }
    public bool SameDayDelivery { get; set; }
    public required string Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public byte[] RowVer { get; set; } = [];
    public List<ProductTag> Tags { get; set; } = [];
    public List<ProductImage> Images { get; set; } = [];
}
