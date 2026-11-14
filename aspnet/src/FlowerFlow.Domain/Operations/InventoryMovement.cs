namespace FlowerFlow.Domain.Operations;

public sealed class InventoryMovement
{
    public long Id { get; set; }
    public int ProductId { get; set; }
    public int QuantityChange { get; set; }
    public int StockAfter { get; set; }
    public required string MovementType { get; set; }
    public long? OrderId { get; set; }
    public int? ReceiptItemId { get; set; }
    public string? Note { get; set; }
    public int? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}
