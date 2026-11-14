namespace FlowerFlow.Domain.Operations;

public sealed class AuditLog
{
    public long Id { get; set; }
    public int? ActorUserId { get; set; }
    public required string Action { get; set; }
    public required string EntityType { get; set; }
    public required string EntityId { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public DateTime CreatedAt { get; set; }
}
