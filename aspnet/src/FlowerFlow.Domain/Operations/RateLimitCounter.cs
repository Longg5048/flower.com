namespace FlowerFlow.Domain.Operations;

public sealed class RateLimitCounter
{
    public required string Scope { get; set; }
    public byte[] KeyHash { get; set; } = [];
    public int Count { get; set; }
    public DateTime? LockedUntil { get; set; }
    public DateTime ExpiresAt { get; set; }
}
