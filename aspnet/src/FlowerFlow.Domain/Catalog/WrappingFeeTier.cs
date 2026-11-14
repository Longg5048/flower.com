namespace FlowerFlow.Domain.Catalog;

public sealed class WrappingFeeTier
{
    public int Id { get; set; }
    public int MaxStems { get; set; }
    public decimal Fee { get; set; }
}
