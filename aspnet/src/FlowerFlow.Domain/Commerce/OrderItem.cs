namespace FlowerFlow.Domain.Commerce;

public sealed class OrderItem
{
    public long Id { get; set; }
    public long OrderId { get; set; }
    public Order? Order { get; set; }
    public int ProductId { get; set; }
    public long? ProductPriceId { get; set; }
    public long? CustomBouquetId { get; set; }
    public required string ProductCode { get; set; }
    public required string ProductName { get; set; }
    public string? ImageUrl { get; set; }
    public required string SaleType { get; set; }
    public required string Unit { get; set; }
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal LineTotal { get; private set; }
    public string? Size { get; set; }
    public string? Color { get; set; }
    public string? CardMessage { get; set; }
    public string? PersonalNote { get; set; }
}
