namespace FlowerFlow.Domain.Operations;

public sealed class StockReceiptItem
{
    public int Id { get; set; }
    public int ReceiptId { get; set; }
    public StockReceipt? Receipt { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitCost { get; set; }
}
