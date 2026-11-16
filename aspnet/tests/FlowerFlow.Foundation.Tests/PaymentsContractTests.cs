using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FlowerFlow.Infrastructure.Vnpay;
using Xunit;

namespace FlowerFlow.Foundation.Tests;

public sealed class PaymentsContractTests
{
    [Fact]
    public Task Payment_attempts_IPN_replay_expiry_retry_and_late_payment_match_JavaScript_and_persisted_state() =>
        OrdersContractTests.RunAsync("payments-oracle.cjs", 25, "/api/payments");
    [Fact]
    public async Task Canonical_query_signature_and_payment_URL_encoding_match_JavaScript()
    {
        var start = new ProcessStartInfo("node") { RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 };
        start.ArgumentList.Add(Path.Combine(ImageContractTests.Root(), "aspnet/tests/FlowerFlow.Foundation.Tests/vnpay-oracle.cjs"));
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync(); var errors = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try { await process.WaitForExitAsync(timeout.Token); } catch { process.Kill(true); throw; }
        Assert.True(process.ExitCode == 0, await errors);
        using var oracle = JsonDocument.Parse(await output);
        foreach (var input in oracle.RootElement.GetProperty("queries").EnumerateArray())
        {
            var parameters = JsonNode.Parse(input.GetProperty("input").GetRawText())!.AsObject();
            Assert.Equal(input.GetProperty("expected").GetString(), VnpayGateway.CanonicalQuery(parameters));
            Assert.Equal(input.GetProperty("valid").GetBoolean(), VnpayGateway.VerifySignature(parameters, "test-secret"));
        }
        var config = VnpayGateway.GetConfig(key => key switch
            { "VNPAY_TMN_CODE" => "TEST", "VNPAY_HASH_SECRET" => "test-secret", "PUBLIC_APP_URL" => "https://example.invalid/path", _ => null });
        foreach (var input in oracle.RootElement.GetProperty("urls").EnumerateArray())
            Assert.Equal(input.GetProperty("expected").GetString(), VnpayGateway.CreatePaymentUrl("FFabc123", 123456, input.GetProperty("ipAddress").GetString(),
                DateTimeOffset.Parse(oracle.RootElement.GetProperty("now").GetString()!, CultureInfo.InvariantCulture),
                DateTimeOffset.Parse(oracle.RootElement.GetProperty("expires").GetString()!, CultureInfo.InvariantCulture), config));
    }
}
