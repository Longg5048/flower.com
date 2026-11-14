namespace FlowerFlow.Domain.Commerce;

public sealed class Refund
{
    public long Id { get; set; }
    public long PaymentId { get; set; }
    public Payment? Payment { get; set; }
    public decimal Amount { get; set; }
    public required string Reference { get; set; }
    public string? Reason { get; set; }
    public int RefundedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}
