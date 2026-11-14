namespace FlowerFlow.Domain.Commerce;

public sealed class DesignRequestStem
{
    public long RequestId { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}
