namespace FlowerFlow.Domain.Commerce;

public sealed class OrderCustomBouquet
{
    public long Id { get; set; }
    public long OrderId { get; set; }
    public Order? Order { get; set; }
    public required string BouquetKey { get; set; }
    public int StemCount { get; set; }
    public decimal WrappingFee { get; set; }
    public List<OrderItem> Items { get; set; } = [];
}
