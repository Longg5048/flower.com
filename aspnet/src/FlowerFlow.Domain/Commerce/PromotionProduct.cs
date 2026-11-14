namespace FlowerFlow.Domain.Commerce;

public sealed class PromotionProduct
{
    public int PromotionId { get; set; }
    public Promotion? Promotion { get; set; }
    public int ProductId { get; set; }
}
