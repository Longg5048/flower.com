namespace FlowerFlow.Domain.Catalog;

public sealed class ProductTag
{
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public required string TagType { get; set; }
    public required string Value { get; set; }
}
