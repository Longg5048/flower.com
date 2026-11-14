namespace FlowerFlow.Domain.Commerce;

public sealed class CartItem
{
    public int UserId { get; set; }
    public required string LineKey { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public string? Options { get; set; }
    public DateTime UpdatedAt { get; set; }
}
