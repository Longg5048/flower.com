namespace FlowerFlow.Domain.Commerce;

public sealed class Order
{
    public long Id { get; set; }
    public required string OrderCode { get; set; }
    public required string PaymentReference { get; set; }
    public required string IdempotencyKey { get; set; }
    public required string RequestHash { get; set; }
    public int? UserId { get; set; }
    public required string OrderType { get; set; }
    public required string CustomerName { get; set; }
    public required string Phone { get; set; }
    public required string Address { get; set; }
    public required string RecipientName { get; set; }
    public required string RecipientPhone { get; set; }
    public bool CustomerIsRecipient { get; set; }
    public int DeliveryZoneId { get; set; }
    public required string DeliveryProvince { get; set; }
    public required string DeliveryDistrict { get; set; }
    public DateOnly DeliveryDate { get; set; }
    public required string DeliveryTime { get; set; }
    public string? GreetingCard { get; set; }
    public bool AnonymousGreetingCard { get; set; }
    public bool GiftWrap { get; set; }
    public string? OrderNote { get; set; }
    public int? PromotionId { get; set; }
    public string? PromotionName { get; set; }
    public decimal? DiscountPercent { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Savings { get; set; }
    public decimal WrappingFee { get; set; }
    public decimal ShippingFee { get; set; }
    public decimal Tax { get; set; }
    public decimal Total { get; set; }
    public required string Status { get; set; }
    public string? CurrentLocation { get; set; }
    public string? EstimatedDelivery { get; set; }
    public required string PaymentMethod { get; set; }
    public required string PaymentStatus { get; set; }
    public required string InventoryState { get; set; }
    public DateTime? ReservationExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public byte[] RowVer { get; set; } = [];
    public List<OrderItem> Items { get; set; } = [];
    public List<OrderCustomBouquet> CustomBouquets { get; set; } = [];
    public List<OrderStatusHistory> StatusHistory { get; set; } = [];
    public List<Payment> Payments { get; set; } = [];
}
