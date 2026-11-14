namespace FlowerFlow.Domain.Commerce;

public sealed class Payment
{
    public long Id { get; set; }
    public long OrderId { get; set; }
    public Order? Order { get; set; }
    public required string Provider { get; set; }
    public required string TxnRef { get; set; }
    public int Attempt { get; set; } = 1;
    public decimal Amount { get; set; }
    public required string Status { get; set; }
    public string? ProviderTransactionNo { get; set; }
    public string? BankCode { get; set; }
    public string? ResponseCode { get; set; }
    public string? RawResponse { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public int? VerifiedBy { get; set; }
    public string? ReconciliationReference { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<Refund> Refunds { get; set; } = [];
}
