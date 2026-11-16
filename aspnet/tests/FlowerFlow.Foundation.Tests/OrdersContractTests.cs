using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FlowerFlow.Domain.Models.Compatibility;
using Xunit;

namespace FlowerFlow.Foundation.Tests;

public sealed class OrdersContractTests
{
    [Fact]
    public async Task Orders_HTTP_and_persisted_state_match_JavaScript_checkout_tracking_expiration_reviews_and_reconciliation()
        => await RunAsync("orders-oracle.cjs", 34, "/api/orders");

    internal static async Task RunAsync(string script, int expectedCount, string defaultRoute)
    {
        var start = new ProcessStartInfo("node") { RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 };
        start.ArgumentList.Add(Path.Combine(ImageContractTests.Root(), "aspnet/tests/FlowerFlow.Foundation.Tests", script));
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync(); var errors = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try { await process.WaitForExitAsync(timeout.Token); } catch { process.Kill(true); throw; }
        Assert.True(process.ExitCode == 0, await errors);
        using var scenarios = JsonDocument.Parse(await output);
        Assert.Equal(expectedCount, scenarios.RootElement.GetArrayLength());
        foreach (var scenario in scenarios.RootElement.EnumerateArray())
        {
            var clock = new LoginContractTests.Clock { Now = DateTimeOffset.Parse("2026-11-16T03:00:00.123Z", System.Globalization.CultureInfo.InvariantCulture).ToUnixTimeMilliseconds() };
            var memory = new CommerceMemory(clock);
            foreach (var pair in scenario.GetProperty("seed").EnumerateObject()) memory.Records[pair.Name] = (Dictionary<string, object>)CommerceMemory.Decode(pair.Value)!;
            await using var host = new CommerceHost(memory, clock);
            using var client = host.CreateClient();
            foreach (var step in scenario.GetProperty("steps").EnumerateArray())
            {
                var operation = step.GetProperty("operation");
                if (operation.TryGetProperty("advance", out var advance)) clock.Now += advance.GetInt64();
                var query = new List<string>();
                if (operation.TryGetProperty("query", out var parameters))
                    foreach (var pair in parameters.EnumerateObject())
                        foreach (var value in pair.Value.ValueKind == JsonValueKind.Array ? pair.Value.EnumerateArray().ToArray() : new[] { pair.Value })
                            query.Add(Uri.EscapeDataString(pair.Name) + "=" + Uri.EscapeDataString(value.GetString()!));
                var route = operation.TryGetProperty("path", out var path) ? path.GetString()! : defaultRoute;
                using var request = new HttpRequestMessage(new HttpMethod(operation.TryGetProperty("method", out var method) ? method.GetString()! : "POST"), route + "?" + string.Join("&", query));
                if (operation.TryGetProperty("authorization", out var authorization)) request.Headers.TryAddWithoutValidation("Authorization", authorization.GetString());
                if (operation.TryGetProperty("body", out var body)) request.Content = new StringContent(body.GetRawText(), Encoding.UTF8, "application/json");
                var response = await client.SendAsync(request);
                var actual = JsonSerializer.SerializeToNode(new
                {
                    status = (int)response.StatusCode, body = JsonNode.Parse(await response.Content.ReadAsStringAsync()),
                    cache = response.Headers.NonValidated.TryGetValues("Cache-Control", out var cache) ? string.Join(", ", cache) : null,
                    allow = response.Content.Headers.TryGetValues("Allow", out var allow) ? string.Join(", ", allow) : null,
                    retry = response.Headers.TryGetValues("Retry-After", out var retry) ? string.Join(", ", retry) : null
                });
                var expected = JsonNode.Parse(step.GetProperty("expected").GetRawText());
                Assert.True(JsonNode.DeepEquals(expected, actual), $"{scenario.GetProperty("name")}: expected {expected}; actual {actual}");
                var states = JsonNode.Parse(JsJson.Stringify(memory.Records.ToDictionary(pair => pair.Key, pair => (object)pair.Value)));
                var expectedStates = JsonNode.Parse(step.GetProperty("states").GetRawText());
                Assert.True(JsonNode.DeepEquals(expectedStates, states), $"{scenario.GetProperty("name")}: persisted state mismatch. Expected {expectedStates}; actual {states}");
            }
        }
    }
}
