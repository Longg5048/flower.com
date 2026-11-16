using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FlowerFlow.Application.Accounts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace FlowerFlow.Foundation.Tests;

public sealed class LoginContractTests
{
    [Fact]
    public async Task HTTP_login_and_policy_state_match_actual_JavaScript_across_lockout_and_outage_flows()
    {
        var start = new ProcessStartInfo("node") { RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 };
        start.ArgumentList.Add(Path.Combine(ImageContractTests.Root(), "aspnet/tests/FlowerFlow.Foundation.Tests/login-oracle.cjs"));
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync(); var errors = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try { await process.WaitForExitAsync(timeout.Token); } catch { process.Kill(true); throw; }
        Assert.True(process.ExitCode == 0, await errors);
        using var scenarios = JsonDocument.Parse(await output);
        Assert.Equal(6, scenarios.RootElement.GetArrayLength());
        foreach (var scenario in scenarios.RootElement.EnumerateArray())
        {
            var backend = new Backend(); var store = new PolicyMemory(); var time = new Clock();
            await using var host = new Host(backend, store, time);
            using var client = host.CreateClient();
            foreach (var step in scenario.GetProperty("steps").EnumerateArray())
            {
                backend.Operation = step.GetProperty("operation"); backend.Version = step.GetProperty("version").GetDouble();
                time.Now = step.GetProperty("now").GetInt64();
                using var request = new HttpRequestMessage(new HttpMethod(backend.Operation.TryGetProperty("method", out var method) ? method.GetString()! : "POST"), "/api/login");
                request.Content = new StringContent(backend.Operation.TryGetProperty("body", out var supplied) ? supplied.GetRawText()
                    : "{\"email\":\"customer@example.test\",\"password\":\"private-password\"}", Encoding.UTF8, "application/json");
                var response = await client.SendAsync(request);
                var states = new JsonObject();
                foreach (var pair in store.Records)
                {
                    var state = (JsonObject)pair.Value.DeepClone();
                    state["leaseId"] = state["leaseId"]!.GetValue<string>().Length > 0 ? "<lease>" : "";
                    states["loginProtection/" + pair.Key] = state;
                }
                var actual = JsonSerializer.SerializeToNode(new
                {
                    status = (int)response.StatusCode, body = JsonNode.Parse(await response.Content.ReadAsStringAsync()),
                    cache = response.Headers.NonValidated.TryGetValues("Cache-Control", out var cache) ? string.Join(", ", cache) : null,
                    allow = response.Content.Headers.TryGetValues("Allow", out var allow) ? string.Join(", ", allow) : null,
                    retry = response.Headers.TryGetValues("Retry-After", out var retry) ? string.Join(", ", retry) : null,
                    readyCalls = backend.ReadyCalls, lookups = backend.Lookups, verified = backend.Verified, minted = backend.Minted, states
                });
                var expected = JsonNode.Parse(step.GetProperty("expected").GetRawText());
                Assert.True(JsonNode.DeepEquals(expected, actual), $"{scenario.GetProperty("name")}: expected {expected}; actual {actual}");
            }
        }
    }
    internal sealed class Clock : TimeProvider
    {
        public long Now { get; set; } = 1_800_000_000_000;
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.FromUnixTimeMilliseconds(Now);
    }
    internal sealed class Backend : IPasswordAuth, ICustomTokenIssuer
    {
        public JsonElement Operation { get; set; } = JsonSerializer.SerializeToElement(new { });
        public double Version { get; set; } = 1000;
        public int ReadyCalls { get; private set; } public int Lookups { get; private set; }
        public int Verified { get; private set; } public int Minted { get; private set; }
        private bool Flag(string key) => Operation.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.True;
        public Task AssertReadyAsync(CancellationToken cancellationToken)
        {
            ReadyCalls++; if (Operation.TryGetProperty("ready", out var ready) && !ready.GetBoolean()) throw new Exception("provider-private");
            return Task.CompletedTask;
        }
        public Task<PasswordAccount> LookupAsync(string email, CancellationToken cancellationToken)
        {
            Lookups++; if (Flag("lookupError")) throw new Exception("provider-private");
            return Task.FromResult(new PasswordAccount(email, email, Version));
        }
        public Task<bool> VerifyAsync(string email, string password, PasswordAccount account, CancellationToken cancellationToken)
        {
            Verified++; if (Flag("outage")) throw new Exception("provider-private"); return Task.FromResult(Flag("valid"));
        }
        public Task<string> CreateAsync(string uid, CancellationToken cancellationToken)
        {
            Minted++; if (Flag("mintError")) throw new Exception("provider-private"); return Task.FromResult("session-token");
        }
    }
    internal sealed class Host(Backend backend, ILoginPolicyStore store, Clock clock) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IPasswordAuth>(); services.AddSingleton<IPasswordAuth>(backend);
                services.RemoveAll<ICustomTokenIssuer>(); services.AddSingleton<ICustomTokenIssuer>(backend);
                services.RemoveAll<ILoginPolicyStore>(); services.AddSingleton(store);
                services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(clock);
            });
        }
    }
    internal sealed class PolicyMemory : ILoginPolicyStore
    {
        public Dictionary<string, JsonObject> Records { get; } = new(StringComparer.Ordinal);
        private readonly SemaphoreSlim gate = new(1, 1);
        public async Task<LoginPolicyReply> RunAsync(string key, Func<ILoginPolicyTransaction, Task<LoginPolicyReply>> operation, CancellationToken cancellationToken)
        {
            await gate.WaitAsync(cancellationToken);
            try { return await operation(new MemoryTransaction(this, key)); }
            finally { gate.Release(); }
        }
        private sealed class MemoryTransaction(PolicyMemory store, string key) : ILoginPolicyTransaction
        {
            public Task<JsonObject?> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(store.Records.TryGetValue(key, out var state) ? (JsonObject)state.DeepClone() : null);
            public void Set(JsonObject state) => store.Records[key] = (JsonObject)state.DeepClone();
            public void Delete() => store.Records.Remove(key);
        }
    }
}
