namespace FlowerFlow.Domain.Commerce;

public sealed class CustomerMessage
{
    public long Id { get; set; }
    public long? OrderId { get; set; }
    public required string CustomerName { get; set; }
    public required string Content { get; set; }
    public string? Reply { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
