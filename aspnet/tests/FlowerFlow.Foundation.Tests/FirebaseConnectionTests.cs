using System.Security.Cryptography;
using System.Text.Json;
using FlowerFlow.Application.Identity;
using FlowerFlow.Infrastructure.Firebase;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace FlowerFlow.Foundation.Tests;

public sealed class FirebaseConnectionTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{secret-invalid-json")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"type\":\"authorized_user\",\"project_id\":\"demo-test\"}")]
    public void Invalid_configuration_is_lazy_and_does_not_leak_secrets(string? json)
    {
        using var connection = new FirebaseConnection(new(null, json));
        var adapter = new FirebaseIdentityAdapter(connection);
        Assert.False(adapter.IsConfigured);
        var error = Assert.Throws<FirebaseConfigurationException>(() => connection.GetDatabase());
        Assert.Null(error.InnerException);
        Assert.Equal("Firebase Admin is not configured.", error.Message);
    }

    [Fact]
    public void Multiple_connections_use_separate_apps_and_dispose_independently()
    {
        var options = TestOptions();
        using var first = new FirebaseConnection(options);
        using var second = new FirebaseConnection(options);
        var firstAuth = first.GetAuth();
        var secondAuth = second.GetAuth();
        Assert.NotSame(firstAuth, secondAuth);
        Assert.Same(firstAuth, first.GetAuth());
        first.Dispose();
        first.Dispose();
        Assert.Throws<ObjectDisposedException>(() => first.GetAuth());
        Assert.Same(secondAuth, second.GetAuth());
    }

    [Fact]
    public async Task Real_sdk_rejects_malformed_token_without_network_access()
    {
        using var connection = new FirebaseConnection(TestOptions());
        var adapter = new FirebaseIdentityAdapter(connection);
        await Assert.ThrowsAsync<InvalidFirebaseTokenException>(() => adapter.VerifyAsync("not-a-jwt", CancellationToken.None));
    }

    [Fact]
    public void Production_connection_refuses_implicit_auth_emulator()
    {
        var previous = Environment.GetEnvironmentVariable("FIREBASE_AUTH_EMULATOR_HOST");
        try
        {
            Environment.SetEnvironmentVariable("FIREBASE_AUTH_EMULATOR_HOST", "127.0.0.1:9099");
            using var connection = new FirebaseConnection(TestOptions());
            Assert.Throws<FirebaseConfigurationException>(() => connection.GetAuth());
            Assert.False(new FirebaseIdentityAdapter(connection).IsConfigured);
        }
        finally { Environment.SetEnvironmentVariable("FIREBASE_AUTH_EMULATOR_HOST", previous); }
    }

    // Generated in memory only; no real credential, network operation or database access.
    private static FirebaseConnectionOptions TestOptions()
    {
        using var key = RSA.Create(2048);
        return new(null, JsonSerializer.Serialize(new
        {
            type = "service_account", project_id = "demo-flowerflow-foundation", private_key_id = "test",
            private_key = key.ExportPkcs8PrivateKeyPem(), client_email = "test@demo-flowerflow-foundation.iam.gserviceaccount.com",
            client_id = "123", token_uri = "https://oauth2.googleapis.com/token"
        }));
    }
}
