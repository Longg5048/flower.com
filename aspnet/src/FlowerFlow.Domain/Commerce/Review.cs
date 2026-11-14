namespace FlowerFlow.Domain.Commerce;

public sealed class Review
{
    public long Id { get; set; }
    public int AuthorUserId { get; set; }
    public long? OrderId { get; set; }
    public required string CustomerName { get; set; }
    public byte Rating { get; set; }
    public required string Comment { get; set; }
    public required string ModerationStatus { get; set; }
    public string? ModerationReason { get; set; }
    public int? ModeratedBy { get; set; }
    public DateTime? ModeratedAt { get; set; }
    public string? Reply { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
