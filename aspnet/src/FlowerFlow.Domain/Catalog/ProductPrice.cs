namespace FlowerFlow.Domain.Catalog;

public sealed class ProductPrice
{
    public long Id { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public decimal Price { get; set; }
    public decimal? PreviousPrice { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public int? ChangedBy { get; set; }
    public string? Reason { get; set; }
}
