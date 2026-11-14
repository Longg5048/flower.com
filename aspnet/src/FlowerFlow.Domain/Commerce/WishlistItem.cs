namespace FlowerFlow.Domain.Commerce;

public sealed class WishlistItem
{
    public int UserId { get; set; }
    public int ProductId { get; set; }
    public DateTime AddedAt { get; set; }
}
