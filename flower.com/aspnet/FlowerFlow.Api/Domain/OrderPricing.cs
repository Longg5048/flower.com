using System.Globalization;

namespace FlowerFlow.Api.Domain;

public sealed record Promotion(
    string Id,
    decimal DiscountPercent,
    bool Active,
    IReadOnlyCollection<string> ProductIds,
    bool AllProducts = false,
    string? StartDate = null,
    string? EndDate = null);

public sealed record PricedItem(string Id, long Price, int Quantity);

public sealed record CartTotals(long Subtotal, long Savings, long Tax, long Total);

public sealed class OrderPricingException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public static class OrderPricing
{
    private const decimal LegacyUsdToVnd = 25949.123093m;
    private static readonly TimeZoneInfo VietnamTimeZone = ResolveVietnamTimeZone();

    public static long ToVndPrice(decimal price)
    {
        if (price < 0)
        {
            return 0;
        }

        if (price > 0 && price < 1000)
        {
            return checked((long)(Math.Round(price * LegacyUsdToVnd / 1000, 0, MidpointRounding.AwayFromZero) * 1000));
        }

        return checked((long)Math.Round(price, 0, MidpointRounding.AwayFromZero));
    }

    public static Promotion? FindPromotion(
        IEnumerable<Promotion> promotions,
        string? promotionId,
        DateTimeOffset now)
    {
        if (string.IsNullOrEmpty(promotionId))
        {
            return null;
        }

        var promotion = promotions.FirstOrDefault(item =>
            string.Equals(item.Id, promotionId, StringComparison.Ordinal) && item.Active);

        if (promotion is null)
        {
            throw new OrderPricingException(
                "invalid-promotion",
                "Ưu đãi đã hết hạn hoặc không hợp lệ. Hãy tải lại trang thanh toán.");
        }

        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, VietnamTimeZone).DateTime);
        var startDate = ParsePromotionDate(promotion.StartDate);
        var endDate = ParsePromotionDate(promotion.EndDate);

        if (promotion.DiscountPercent <= 0
            || promotion.DiscountPercent > 100
            || (!string.IsNullOrEmpty(promotion.StartDate) && startDate is null)
            || (!string.IsNullOrEmpty(promotion.EndDate) && endDate is null)
            || (startDate is not null && endDate is not null && startDate > endDate)
            || (startDate is not null && today < startDate)
            || (endDate is not null && today > endDate))
        {
            throw new OrderPricingException("invalid-promotion", "Ưu đãi hiện không hợp lệ.");
        }

        return promotion;
    }

    public static CartTotals PriceCart(IReadOnlyCollection<PricedItem> items, Promotion? promotion)
    {
        var subtotal = items.Sum(item => checked(item.Price * item.Quantity));
        var eligibleIds = promotion?.ProductIds.ToHashSet(StringComparer.Ordinal)
            ?? new HashSet<string>(StringComparer.Ordinal);
        var discount = promotion is null
            ? 0m
            : items.Sum(item =>
                promotion.AllProducts || eligibleIds.Contains(item.Id)
                    ? (decimal)item.Price * item.Quantity * promotion.DiscountPercent / 100
                    : 0m);
        var savings = Math.Min(subtotal, checked((long)Math.Round(discount, 0, MidpointRounding.AwayFromZero)));

        return new CartTotals(subtotal, savings, 0, subtotal - savings);
    }

    private static DateOnly? ParsePromotionDate(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        return DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;
    }

    private static TimeZoneInfo ResolveVietnamTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }
    }
}
