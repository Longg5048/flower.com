using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FlowerFlow.Application.Orders;
using FlowerFlow.Domain.Models.Compatibility;
using Xunit;

namespace FlowerFlow.Foundation.Tests;

public sealed class OrderRulesContractTests
{
    internal static async Task<JsonDocument> OracleAsync(string script)
    {
        var start = new ProcessStartInfo("node") { RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 };
        start.ArgumentList.Add(Path.Combine(ImageContractTests.Root(), "aspnet/tests/FlowerFlow.Foundation.Tests", script));
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync(); var errors = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try { await process.WaitForExitAsync(timeout.Token); } catch { process.Kill(true); throw; }
        Assert.True(process.ExitCode == 0, await errors);
        return JsonDocument.Parse(await output);
    }
    [Fact]
    public async Task Validation_and_delivery_schedule_match_current_JavaScript()
    {
        using var oracle = await OracleAsync("order-validation-oracle.cjs");
        foreach (var input in oracle.RootElement.GetProperty("results").EnumerateArray())
        {
            var body = input.GetProperty("body");
            var result = input.GetProperty("type").GetString() == "checkout" ? OrderValidationService.ValidateCheckout(body) : OrderValidationService.ValidateQuote(body);
            var expected = JsonNode.Parse(input.GetProperty("expected").GetRawText());
            Assert.True(JsonNode.DeepEquals(expected, JsonSerializer.SerializeToNode(result)), $"Validation mismatch: {input}");
        }
        foreach (var input in oracle.RootElement.GetProperty("schedules").EnumerateArray())
            Assert.Equal(input.GetProperty("expected").GetString(), OrderValidationService.DeliveryScheduleError(input.GetProperty("date").GetString()!,
                input.GetProperty("time").GetString()!, DateTimeOffset.Parse(input.GetProperty("now").GetString()!, CultureInfo.InvariantCulture)));
    }
    [Fact]
    public async Task Pricing_discounts_legacy_prices_and_bouquet_fees_match_current_JavaScript()
    {
        using var oracle = await OracleAsync("order-pricing-oracle.cjs");
        foreach (var test in oracle.RootElement.EnumerateArray())
        {
            var input = test.GetProperty("input"); object? actual = null; BackendException? failure = null;
            List<Dictionary<string, object>> Items() => input.GetProperty("items").EnumerateArray().Select(item => (Dictionary<string, object>)Js.Object(item)!).ToList();
            try
            {
                actual = test.GetProperty("method").GetString() switch
                {
                    "price" => PricingService.GetProductPrice(input),
                    "cart" => PricingService.PriceCart(Items(), Js.Object(input.GetProperty("promotion")) as Dictionary<string, object>),
                    "promotion" => PricingService.FindPromotion(input.GetProperty("promotions"), input.GetProperty("id").GetString(), DateTimeOffset.Parse("2026-11-15T12:00:00Z", CultureInfo.InvariantCulture)),
                    "aggregate" => PricingService.AggregateItems(input.GetProperty("items")),
                    "bouquets" => PricingService.CalculateCustomBouquets(Items(), input.GetProperty("tiers")),
                    _ => throw new InvalidOperationException("Unknown pricing oracle method.")
                };
            }
            catch (BackendException error) { failure = error; }
            if (test.TryGetProperty("code", out var code))
            {
                Assert.NotNull(failure); Assert.Equal(code.GetString(), failure.Code); Assert.Equal(test.GetProperty("message").GetString(), failure.Message);
            }
            else
            {
                Assert.Null(failure);
                Assert.True(JsonNode.DeepEquals(JsonNode.Parse(test.GetProperty("expected").GetRawText()), JsonSerializer.SerializeToNode(actual)), $"Pricing mismatch: {test}");
            }
        }
    }
}
