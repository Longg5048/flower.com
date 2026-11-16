using Xunit;
namespace FlowerFlow.Foundation.Tests;
public sealed class DesignContractTests
{
    [Fact]
    public async Task Design_validation_colors_budget_images_and_capability_hash_match_JavaScript()
    {
        using var oracle = await OrderRulesContractTests.OracleAsync("design-validation-oracle.cjs");
        foreach (var test in oracle.RootElement.EnumerateArray())
        {
            var input = test.GetProperty("input"); object? actual = null;
            var failure = Record.Exception(() => actual = test.GetProperty("method").GetString() switch
            {
                "design" => FlowerFlow.Application.DesignRequests.DesignRequestValidationService.ParseDesign(input),
                "stems" => FlowerFlow.Application.DesignRequests.DesignRequestValidationService.NormalizeStems(input),
                "image" => FlowerFlow.Application.DesignRequests.DesignRequestValidationService.ValidImage(input),
                "token" => FlowerFlow.Application.DesignRequests.DesignRequestValidationService.ValidAccessToken(input.GetProperty("token"), input.GetProperty("hash")),
                _ => throw new InvalidOperationException()
            });
            if (test.TryGetProperty("exception", out _)) Assert.NotNull(failure);
            else
            {
                Assert.Null(failure);
                Assert.True(System.Text.Json.Nodes.JsonNode.DeepEquals(System.Text.Json.Nodes.JsonNode.Parse(test.GetProperty("expected").GetRawText()), System.Text.Json.JsonSerializer.SerializeToNode(actual)), test.ToString());
            }
        }
    }
    [Fact]
    public async Task Design_requests_HTTP_and_all_writes_match_JavaScript() => await OrdersContractTests.RunAsync("design-oracle.cjs", 24, "/api/design-requests");
}
