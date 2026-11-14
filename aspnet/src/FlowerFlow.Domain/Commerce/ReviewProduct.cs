namespace FlowerFlow.Domain.Commerce;

public sealed class ReviewProduct
{
    public long ReviewId { get; set; }
    public int ProductId { get; set; }
    public Review? Review { get; set; }
}
