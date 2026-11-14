namespace FlowerFlow.Domain.Commerce;

public sealed class DesignRequestHistory
{
    public long Id { get; set; }
    public long RequestId { get; set; }
    public required string Status { get; set; }
    public string? Note { get; set; }
    public int? ChangedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}
