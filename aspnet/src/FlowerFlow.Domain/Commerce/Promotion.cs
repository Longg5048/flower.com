namespace FlowerFlow.Domain.Commerce;

public sealed class Promotion
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public decimal DiscountPercent { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public bool AppliesToAll { get; set; }
    public bool IsActive { get; set; } = true;
    public int? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<PromotionProduct> Products { get; set; } = [];
}
