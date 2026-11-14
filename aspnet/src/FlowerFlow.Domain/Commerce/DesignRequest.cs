namespace FlowerFlow.Domain.Commerce;

public sealed class DesignRequest
{
    public long Id { get; set; }
    public required string RequestCode { get; set; }
    public byte[] AccessTokenHash { get; set; } = [];
    public int? UserId { get; set; }
    public required string CustomerName { get; set; }
    public required string Phone { get; set; }
    public int? DeliveryZoneId { get; set; }
    public string? DeliveryProvince { get; set; }
    public string? DeliveryDistrict { get; set; }
    public DateOnly? DeliveryDate { get; set; }
    public required string Design { get; set; }
    public string? SpecialRequest { get; set; }
    public decimal EstimatedTotal { get; set; }
    public decimal? QuotedTotal { get; set; }
    public string? QuoteNote { get; set; }
    public required string Status { get; set; }
    public long? ConvertedOrderId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
