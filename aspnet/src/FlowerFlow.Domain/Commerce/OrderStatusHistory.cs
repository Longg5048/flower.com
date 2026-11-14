namespace FlowerFlow.Domain.Commerce;

public sealed class OrderStatusHistory
{
    public long Id { get; set; }
    public long OrderId { get; set; }
    public Order? Order { get; set; }
    public required string Status { get; set; }
    public string? Location { get; set; }
    public string? EstimatedDelivery { get; set; }
    public int? ChangedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}
