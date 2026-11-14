namespace FlowerFlow.Domain.Catalog;

public sealed class ProductImage
{
    public long Id { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public string? Url { get; set; }
    public byte[]? Data { get; set; }
    public string? ContentType { get; set; }
    public int SortOrder { get; set; }
    public int? UploadedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}
