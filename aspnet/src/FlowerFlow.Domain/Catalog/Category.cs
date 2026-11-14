namespace FlowerFlow.Domain.Catalog;

public sealed class Category
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string GroupKey { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public List<Product> Products { get; set; } = [];
}
