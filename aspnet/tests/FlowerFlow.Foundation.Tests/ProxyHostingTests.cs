using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FlowerFlow.Application.Orders;
using Xunit;
namespace FlowerFlow.Foundation.Tests;
public sealed class ProxyHostingTests
{
    [Theory]
    [InlineData(false, "203.0.113.9")]
    [InlineData(true, "198.51.100.7")]
    public async Task Only_explicit_trusted_proxy_can_supply_rate_limit_client_IP(bool trusted, string expectedIp)
    {
        var clock = new LoginContractTests.Clock(); var memory = new CommerceMemory(clock);
        using var seed = JsonDocument.Parse("""{"products/ready":{"price":100000,"quantity":10},"metadata/deliveryZones":{"zones":[{"id":"zone-a","province":"Hà Nội","district":"Ba Đình","fee":10000,"enabled":true}]}}""");
        foreach (var pair in seed.RootElement.EnumerateObject()) memory.Records[pair.Name] = (Dictionary<string, object>)CommerceMemory.Decode(pair.Value)!;
        await using var host = new CommerceHost(memory, clock, trusted ? ["203.0.113.9"] : []); using var client = host.CreateClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Forwarded-For", "198.51.100.7");
        var response = await client.PostAsJsonAsync("/api/orders", new { action = "lookup", id = "missing", phone = "0901234567" });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.True(memory.Records.ContainsKey("orderLookupLimits/" + TrackingService.Hmac("test-commerce-secret", expectedIp)));
        Assert.Single(memory.Records.Keys, key => key.StartsWith("orderLookupLimits/"));
    }
}
