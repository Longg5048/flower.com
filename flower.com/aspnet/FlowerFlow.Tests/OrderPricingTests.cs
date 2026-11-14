using FlowerFlow.Api.Domain;
using Xunit;

namespace FlowerFlow.Tests;

public sealed class OrderPricingTests
{
    private static readonly DateTimeOffset Now = new(2025, 3, 20, 5, 30, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(450000, 450000)]
    [InlineData(73.99, 1920000)]
    [InlineData(49.99, 1297000)]
    [InlineData(0, 0)]
    [InlineData(-5, 0)]
    public void ToVndPrice_matches_legacy_conversion(decimal input, long expected)
    {
        Assert.Equal(expected, OrderPricing.ToVndPrice(input));
    }

    [Fact]
    public void PriceCart_rounds_discount_once_and_limits_it_to_subtotal()
    {
        var promotion = new Promotion("p15", 15.5m, true, ["1"], AllProducts: true);

        var totals = OrderPricing.PriceCart([new PricedItem("1", 1_297_000, 1)], promotion);

        Assert.Equal(new CartTotals(1_297_000, 201_035, 0, 1_095_965), totals);
    }

    [Fact]
    public void PriceCart_applies_product_scoped_promotions_only_to_matching_items()
    {
        var promotion = new Promotion("p30", 30, true, ["1"]);

        var totals = OrderPricing.PriceCart(
            [new PricedItem("1", 100_000, 1), new PricedItem("2", 100_000, 1)],
            promotion);

        Assert.Equal(new CartTotals(200_000, 30_000, 0, 170_000), totals);
    }

    [Fact]
    public void PriceCart_caps_a_full_discount_at_the_subtotal()
    {
        var promotion = new Promotion("p100", 100, true, [], AllProducts: true);

        var totals = OrderPricing.PriceCart([new PricedItem("1", 100_000, 3)], promotion);

        Assert.Equal(new CartTotals(300_000, 300_000, 0, 0), totals);
    }

    [Fact]
    public void FindPromotion_uses_the_Vietnamese_calendar_date()
    {
        var promotion = new Promotion("p20", 20, true, [], AllProducts: true, StartDate: "2025-03-20", EndDate: "");

        Assert.Same(promotion, OrderPricing.FindPromotion([promotion], "p20", Now));
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 101)]
    public void FindPromotion_rejects_inactive_or_out_of_range_promotions(bool active, decimal percent)
    {
        var promotion = new Promotion("p", percent, active, []);

        var error = Assert.Throws<OrderPricingException>(() =>
            OrderPricing.FindPromotion([promotion], "p", Now));

        Assert.Equal("invalid-promotion", error.Code);
    }

    [Theory]
    [InlineData("2025-03-21", null)]
    [InlineData(null, "2025-03-19")]
    public void FindPromotion_rejects_promotions_outside_the_Vietnamese_date_window(string? startDate, string? endDate)
    {
        var promotion = new Promotion("p", 20, true, [], StartDate: startDate, EndDate: endDate);

        var error = Assert.Throws<OrderPricingException>(() =>
            OrderPricing.FindPromotion([promotion], "p", Now));

        Assert.Equal("invalid-promotion", error.Code);
    }

    [Fact]
    public void FindPromotion_rejects_malformed_dates()
    {
        var promotion = new Promotion("p", 20, true, [], EndDate: "2025-02-30");

        var error = Assert.Throws<OrderPricingException>(() =>
            OrderPricing.FindPromotion([promotion], "p", Now));

        Assert.Equal("invalid-promotion", error.Code);
    }
}
