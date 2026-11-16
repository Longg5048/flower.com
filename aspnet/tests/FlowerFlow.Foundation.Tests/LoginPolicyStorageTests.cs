using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using FlowerFlow.Application.Accounts;
using FlowerFlow.Infrastructure.Firebase;
using Google.Cloud.Firestore;
using Xunit;

namespace FlowerFlow.Foundation.Tests;

public sealed class LoginPolicyStorageTests
{
    [FirestoreEmulatorFact, Trait("Category", "Emulator")]
    public async Task Concurrent_leases_expiry_stale_completion_and_password_version_are_serialized_by_Firestore()
    {
        await using var fixture = await Fixture.CreateAsync();
        var time = new LoginContractTests.Clock();
        var policy = new LoginProtectionService(fixture.Store, time);
        var otherInstance = new LoginProtectionService(new FirestoreLoginPolicyStore(() => fixture.Database), time);
        var attempts = await Task.WhenAll(Enumerable.Range(0, 12).Select(index => (index % 2 == 0 ? policy : otherInstance).AcquireAsync("uid-a", 1000, fixture.Token)));
        var active = Assert.Single(attempts, attempt => attempt.Attempt is not null).Attempt!;
        Assert.Equal(11, attempts.Count(attempt => attempt.Code == "login-in-progress"));
        time.Now += 60_000;
        var replacement = (await policy.AcquireAsync("uid-a", 1000, fixture.Token)).Attempt!;
        Assert.NotEqual(active.LeaseId, replacement.LeaseId);
        Assert.Equal(503, (await policy.FinishAsync(active, "invalid", fixture.Token)).Status);
        Assert.Equal(200, (await policy.FinishAsync(replacement, "success", fixture.Token)).Status);
        Assert.False((await fixture.Database.Collection("loginProtection").Document("uid-a").GetSnapshotAsync(fixture.Token)).Exists);
        await fixture.Database.Collection("loginProtection").Document("uid-a").SetAsync(new Dictionary<string, object>
            { ["failures"] = 10, ["lockedUntil"] = 0, ["resetRequired"] = true, ["passwordUpdatedAt"] = 1000 }, cancellationToken: fixture.Token);
        Assert.Equal("password-reset-required", (await policy.AcquireAsync("uid-a", 1000, fixture.Token)).Code);
        var afterChange = await policy.AcquireAsync("uid-a", 1001, fixture.Token);
        Assert.NotNull(afterChange.Attempt);
        Assert.Equal(0, (await fixture.Database.Collection("loginProtection").Document("uid-a").GetSnapshotAsync(fixture.Token)).GetValue<double>("failures"));
        Assert.Equal(200, (await policy.FinishAsync(afterChange.Attempt!, "success", fixture.Token)).Status);
    }
    [FirestoreEmulatorFact, Trait("Category", "Emulator")]
    public async Task HTTP_password_checks_stop_at_lockout_and_success_requires_policy_clearance()
    {
        await using var fixture = await Fixture.CreateAsync();
        var backend = new LoginContractTests.Backend(); var clock = new LoginContractTests.Clock();
        var observed = new ObservedStore(fixture.Store);
        await using var host = new LoginContractTests.Host(backend, observed, clock);
        using var client = host.CreateClient();
        var credentials = new { email = " CUSTOMER@EXAMPLE.TEST ", password = "private-password" };
        var replies = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => client.PostAsJsonAsync("/api/login", credentials, fixture.Token)));
        Assert.True(replies.All(reply => new[] { 401, 409, 429 }.Contains((int)reply.StatusCode)),
            string.Join("\n", observed.Failures.Select(error => error.ToString())));
        for (var attempt = 0; backend.Verified < 5 && attempt < 10; attempt++) await client.PostAsJsonAsync("/api/login", credentials, fixture.Token);
        Assert.Equal(5, backend.Verified);
        backend.Operation = JsonSerializer.SerializeToElement(new { valid = true });
        var blocked = await client.PostAsJsonAsync("/api/login", credentials, fixture.Token);
        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
        Assert.Equal(5, backend.Verified); Assert.Equal(0, backend.Minted);
        clock.Now += 15 * 60_000;
        var success = await client.PostAsJsonAsync("/api/login", credentials, fixture.Token);
        Assert.Equal(HttpStatusCode.OK, success.StatusCode);
        Assert.Equal("session-token", JsonNode.Parse(await success.Content.ReadAsStringAsync(fixture.Token))!["customToken"]!.GetValue<string>());
        Assert.Equal(1, backend.Minted);
        Assert.Empty((await fixture.Database.Collection("loginProtection").GetSnapshotAsync(fixture.Token)).Documents);
    }
    private sealed class ObservedStore(ILoginPolicyStore inner) : ILoginPolicyStore
    {
        public System.Collections.Concurrent.ConcurrentQueue<Exception> Failures { get; } = new();
        public async Task<LoginPolicyReply> RunAsync(string key, Func<ILoginPolicyTransaction, Task<LoginPolicyReply>> operation, CancellationToken cancellationToken)
        {
            try { return await inner.RunAsync(key, operation, cancellationToken); }
            catch (Exception error) { Failures.Enqueue(error); throw; }
        }
    }
    private sealed class Fixture : IAsyncDisposable
    {
        public required FirestoreDb Database { get; init; }
        public required FirestoreLoginPolicyStore Store { get; init; }
        private readonly CancellationTokenSource timeout = new(TimeSpan.FromSeconds(50));
        public CancellationToken Token => timeout.Token;
        public static Task<Fixture> CreateAsync()
        {
            var endpoint = Environment.GetEnvironmentVariable("FLOWERFLOW_TEST_FIRESTORE_HOST")!;
            if (!Uri.TryCreate("http://" + endpoint, UriKind.Absolute, out var uri) || !uri.IsLoopback)
                throw new InvalidOperationException("Only loopback emulator endpoints are permitted.");
            var db = new FirestoreDbBuilder
            {
                ProjectId = "demo-login-" + Guid.NewGuid().ToString("N")[..12], Endpoint = endpoint,
                ChannelCredentials = Grpc.Core.ChannelCredentials.Insecure,
                Settings = new Google.Cloud.Firestore.V1.FirestoreSettings
                { CallSettings = Google.Api.Gax.Grpc.CallSettings.FromHeader("Authorization", "Bearer owner") }
            }.Build();
            return Task.FromResult(new Fixture { Database = db, Store = new(() => db) });
        }
        public async ValueTask DisposeAsync()
        {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            foreach (var document in (await Database.Collection("loginProtection").GetSnapshotAsync(cleanup.Token)).Documents)
                await document.Reference.DeleteAsync(cancellationToken: cleanup.Token);
            timeout.Dispose();
        }
    }
}
