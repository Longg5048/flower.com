namespace FlowerFlow.Domain.Catalog;

public sealed class DeliveryZone
{
    public int Id { get; set; }
    public required string Province { get; set; }
    public required string District { get; set; }
    public decimal Fee { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTime UpdatedAt { get; set; }
}
