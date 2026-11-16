using FlowerFlow.Application.Identity;
using FlowerFlow.Infrastructure.Firebase;
using Google.Cloud.Firestore;
using Grpc.Core;
using Xunit;

namespace FlowerFlow.Foundation.Tests;

public sealed class FirestoreEmulatorFactAttribute : FactAttribute
{
    public FirestoreEmulatorFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FLOWERFLOW_TEST_FIRESTORE_HOST")))
            Skip = "Set FLOWERFLOW_TEST_FIRESTORE_HOST to an explicitly started local Firestore emulator.";
    }
}

public sealed class FirestoreRoleTests
{
    [FirestoreEmulatorFact, Trait("Category", "Emulator")]
    public async Task Real_reader_observes_role_creation_change_and_revocation()
    {
        var host = Environment.GetEnvironmentVariable("FLOWERFLOW_TEST_FIRESTORE_HOST")!;
        if (!Uri.TryCreate("http://" + host, UriKind.Absolute, out var uri) || !uri.IsLoopback || uri.Port <= 0)
            throw new InvalidOperationException("Only an explicit loopback emulator endpoint is permitted.");
        var database = new FirestoreDbBuilder
        {
            ProjectId = "demo-flowerflow-foundation", Endpoint = host,
            ChannelCredentials = ChannelCredentials.Insecure,
            Settings = new Google.Cloud.Firestore.V1.FirestoreSettings
            {
                CallSettings = Google.Api.Gax.Grpc.CallSettings.FromHeader("Authorization", "Bearer owner")
            }
        }.Build();
        var reader = new FirestoreAdminRoleReader(() => database);
        var uid = "foundation-" + Guid.NewGuid().ToString("N");
        var document = database.Collection("adminRoles").Document(uid);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        try
        {
            Assert.Null(await reader.ReadAsync(uid, timeout.Token));
            await document.SetAsync(new Dictionary<string, object> { ["role"] = "admin" }, cancellationToken: timeout.Token);
            Assert.Equal(new AdminRole("admin"), await reader.ReadAsync(uid, timeout.Token));
            await document.SetAsync(new Dictionary<string, object> { ["role"] = "owner" }, cancellationToken: timeout.Token);
            Assert.Equal(new AdminRole("owner"), await reader.ReadAsync(uid, timeout.Token));
            await document.SetAsync(new Dictionary<string, object>(), cancellationToken: timeout.Token);
            Assert.Equal(new AdminRole(null), await reader.ReadAsync(uid, timeout.Token));
            await document.DeleteAsync(cancellationToken: timeout.Token);
            Assert.Null(await reader.ReadAsync(uid, timeout.Token));
        }
        finally { await document.DeleteAsync(cancellationToken: timeout.Token); }
    }
}
