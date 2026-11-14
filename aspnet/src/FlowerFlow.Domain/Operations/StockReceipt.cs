namespace FlowerFlow.Domain.Operations;

public sealed class StockReceipt
{
    public int Id { get; set; }
    public int? SupplierId { get; set; }
    public DateTime ReceivedAt { get; set; }
    public int CreatedBy { get; set; }
    public string? Note { get; set; }
    public List<StockReceiptItem> Items { get; set; } = [];
}
