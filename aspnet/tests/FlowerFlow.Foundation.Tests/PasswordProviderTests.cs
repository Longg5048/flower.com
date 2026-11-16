using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using FlowerFlow.Application.Accounts;
using FlowerFlow.Infrastructure.Firebase;
using Xunit;

namespace FlowerFlow.Foundation.Tests;

public sealed class PasswordProviderTests
{
    private static string PublicKey() => Regex.Match(File.ReadAllText(Path.Combine(ImageContractTests.Root(), "flower.com/js/core/firebase-config.js")),
        """apiKey\s*:\s*["']([^"']+)["']""").Groups[1].Value;
    private static FirebasePasswordAuth Provider(Handler handler, LoginContractTests.Clock? time = null, string? publicKey = null) =>
        new(new HttpClient(handler), new Credentials(), new(publicKey ?? PublicKey(), "server-test-key", "test-hmac-key"), time ?? new());
    private const string Blocked = "{\"error\":{\"details\":[{\"reason\":\"API_KEY_SERVICE_BLOCKED\",\"metadata\":{\"method\":\"google.cloud.identitytoolkit.v1.AuthenticationService.SignInWithPassword\"}}]}}";

    [Fact]
    public async Task Both_public_password_endpoints_must_be_blocked_and_success_is_cached_for_one_minute()
    {
        var handler = new Handler([(403, Blocked), (403, Blocked), (403, Blocked), (403, Blocked)]);
        var clock = new LoginContractTests.Clock();
        var provider = Provider(handler, clock);
        await provider.AssertReadyAsync(CancellationToken.None);
        await provider.AssertReadyAsync(CancellationToken.None);
        Assert.Equal(2, handler.Calls.Count);
        Assert.EndsWith("/accounts:signInWithPassword", handler.Calls[0].Path);
        Assert.EndsWith("/relyingparty/verifyPassword", handler.Calls[1].Path);
        clock.Now += 60_001;
        await provider.AssertReadyAsync(CancellationToken.None);
        Assert.Equal(4, handler.Calls.Count);
    }
    [Theory]
    [InlineData(400, "{\"error\":{\"message\":\"INVALID_PASSWORD\"}}")]
    [InlineData(403, "{\"error\":{\"details\":[{\"reason\":\"API_KEY_HTTP_REFERRER_BLOCKED\"}]}}")]
    [InlineData(429, "{}")]
    [InlineData(403, "{\"error\":{\"details\":[{\"reason\":\"API_KEY_SERVICE_BLOCKED\",\"metadata\":{\"method\":\"OtherMethod\"}}]}}")]
    public async Task Non_method_restrictions_do_not_enable_password_login(int status, string body)
    {
        var handler = new Handler([(status, body)]);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Provider(handler).AssertReadyAsync(CancellationToken.None));
        Assert.Single(handler.Calls);
    }
    [Fact]
    public async Task Public_key_must_match_frontend_and_failed_probe_is_not_cached()
    {
        var handler = new Handler([(429, "{}"), (403, Blocked), (403, Blocked)]);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Provider(handler, publicKey: "wrong-public-key").AssertReadyAsync(CancellationToken.None));
        Assert.Empty(handler.Calls);
        var provider = Provider(handler);
        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.AssertReadyAsync(CancellationToken.None));
        await provider.AssertReadyAsync(CancellationToken.None);
        Assert.Equal(3, handler.Calls.Count);
    }
    [Fact]
    public async Task Account_lookup_uses_server_OAuth_and_unknown_account_uses_stable_HMAC_key()
    {
        var handler = new Handler([(200, "{\"users\":[{\"localId\":\"uid-a\",\"passwordUpdatedAt\":\"12345\",\"disabled\":true}]}"), (200, "{}")]);
        var provider = Provider(handler);
        var found = await provider.LookupAsync("customer@example.test", CancellationToken.None);
        Assert.Equal(new PasswordAccount("uid-a", "uid-a", 12345, true), found);
        Assert.Equal("Bearer test-access-token", handler.Calls[0].Authorization);
        var missing = await provider.LookupAsync("missing@example.test", CancellationToken.None);
        Assert.Null(missing.Uid);
        Assert.Equal("unknown-" + Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes("test-hmac-key"), Encoding.UTF8.GetBytes("missing@example.test"))), missing.Key);
    }
    [Theory]
    [InlineData("INVALID_LOGIN_CREDENTIALS", false)]
    [InlineData("INVALID_PASSWORD", false)]
    [InlineData("EMAIL_NOT_FOUND", false)]
    [InlineData("USER_DISABLED", true)]
    [InlineData("TOO_MANY_ATTEMPTS_TRY_LATER", true)]
    public async Task Only_invalid_password_codes_count_as_wrong_password(string code, bool unavailable)
    {
        var provider = Provider(new Handler([(400, "{\"error\":{\"message\":\"" + code + "\"}}") ]));
        var operation = () => provider.VerifyAsync("customer@example.test", "private-password", new("uid-a", "uid-a", 123), CancellationToken.None);
        if (unavailable) await Assert.ThrowsAsync<InvalidOperationException>(operation);
        else Assert.False(await operation());
    }
    [Theory]
    [InlineData("uid-a", false, true)]
    [InlineData("other", false, false)]
    [InlineData("uid-a", true, false)]
    public async Task Successful_provider_reply_must_match_account_and_not_return_upstream_tokens(string uid, bool disabled, bool allowed)
    {
        var provider = Provider(new Handler([(200, "{\"localId\":\"" + uid + "\",\"idToken\":\"upstream-secret\",\"refreshToken\":\"refresh-secret\"}")]));
        var operation = () => provider.VerifyAsync("customer@example.test", "private-password", new("uid-a", "uid-a", 123, disabled), CancellationToken.None);
        if (allowed) Assert.True(await operation()); else await Assert.ThrowsAsync<InvalidOperationException>(operation);
    }
    private sealed class Credentials : IFirebaseIdentityCredentials
    {
        public void EnsureConfigured() { }
        public string ProjectId => "demo-password-provider";
        public Task<string> GetAccessTokenAsync(CancellationToken cancellationToken) => Task.FromResult("test-access-token");
    }
    private sealed class Handler((int Status, string Body)[] replies) : HttpMessageHandler
    {
        public List<(string Path, string? Authorization)> Calls { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls.Add((request.RequestUri!.AbsolutePath, request.Headers.Authorization?.ToString()));
            var reply = replies[Calls.Count - 1];
            return Task.FromResult(new HttpResponseMessage((HttpStatusCode)reply.Status)
                { Content = new StringContent(reply.Body, Encoding.UTF8, "application/json") });
        }
    }
}
