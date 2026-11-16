using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using FlowerFlow.Application.Identity;
using Xunit;

namespace FlowerFlow.Foundation.Tests;

public sealed class IdentityContractTests
{
    [Fact]
    public async Task Authorization_matches_actual_JavaScript_for_all_scenarios()
    {
        var root = FindRoot();
        var start = new ProcessStartInfo("node")
        {
            RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8, StandardErrorEncoding = System.Text.Encoding.UTF8
        };
        start.ArgumentList.Add(Path.Combine(root, "aspnet/tests/FlowerFlow.Foundation.Tests/firebase-admin-oracle.cjs"));
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch { process.Kill(entireProcessTree: true); throw; }
        Assert.True(process.ExitCode == 0, await errors);
        using var scenarios = JsonDocument.Parse(await output);
        Assert.Equal(24, scenarios.RootElement.GetArrayLength());
        foreach (var input in scenarios.RootElement.EnumerateArray())
        {
            var fake = new Backend(input);
            var service = new RequestIdentityService(fake, fake);
            var access = input.GetProperty("method").GetString() switch
            {
                "admin" => RequiredAccess.Admin, "owner" => RequiredAccess.Owner, _ => RequiredAccess.Customer
            };
            var result = await service.VerifyAsync(input.GetProperty("authorization").GetString(), access);
            var actual = JsonSerializer.SerializeToNode(new
            {
                uid = result.Identity?.Uid, status = result.Failure?.Status ?? 200,
                body = result.Failure is { } failure ? new { code = failure.Code, error = failure.Message } : null,
                verifiedToken = fake.VerifiedToken, roleReads = fake.RoleReads
            });
            var expected = JsonNode.Parse(input.GetProperty("expected").GetRawText())!.AsObject();
            // The fake cannot prove SDK revocation checks; the production adapter explicitly enables them.
            expected.Remove("checkRevoked");
            Assert.True(JsonNode.DeepEquals(expected, actual), $"{input.GetProperty("name")}: expected {expected}; actual {actual}");
            Assert.DoesNotContain("sensitive-provider-detail", actual!.ToJsonString());
        }
    }

    [Fact]
    public async Task Missing_configuration_returns_sanitized_503()
    {
        var backend = new Backend { TokenException = new FirebaseConfigurationException() };
        var result = await new RequestIdentityService(backend, backend).VerifyAsync("Bearer token", RequiredAccess.Customer);
        Assert.Equal(new IdentityFailure(503, "admin-api-not-configured", "API quản trị Firebase chưa được cấu hình."), result.Failure);
        Assert.Equal(0, backend.RoleReads);
    }

    [Fact]
    public async Task Role_is_read_again_after_revocation()
    {
        var backend = new Backend { Identity = new("user-a", new Dictionary<string, object> { ["admin"] = true }), Role = new("admin") };
        var service = new RequestIdentityService(backend, backend);
        Assert.True((await service.VerifyAsync("Bearer token", RequiredAccess.Admin)).Succeeded);
        backend.Role = null;
        Assert.Equal(403, (await service.VerifyAsync("Bearer token", RequiredAccess.Admin)).Failure?.Status);
        Assert.Equal(2, backend.RoleReads);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Request_cancellation_propagates(bool duringRoleRead)
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var backend = new Backend { Identity = new("user-a", new Dictionary<string, object> { ["admin"] = true }) };
        if (duringRoleRead) backend.RoleException = new OperationCanceledException(cancelled.Token);
        else backend.TokenException = new OperationCanceledException(cancelled.Token);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new RequestIdentityService(backend, backend)
            .VerifyAsync("Bearer token", RequiredAccess.Admin, cancelled.Token));
    }

    [Fact]
    public void Application_has_no_transport_or_provider_dependency()
    {
        foreach (var reference in typeof(RequestIdentityService).Assembly.GetReferencedAssemblies())
            Assert.DoesNotContain(new[] { "FlowerFlow.Infrastructure", "FirebaseAdmin", "Google.Cloud.Firestore", "Microsoft.AspNetCore.Http.Abstractions", "Microsoft.EntityFrameworkCore" },
                name => reference.Name == name);
    }

    private static string FindRoot()
    {
        var path = new DirectoryInfo(AppContext.BaseDirectory);
        while (path is not null && !File.Exists(Path.Combine(path.FullName, "flower.com/backend/shared/firebase-admin.js"))) path = path.Parent;
        return path?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    private sealed class Backend : IFirebaseTokenVerifier, IAdminRoleReader
    {
        public Backend() { }
        public Backend(JsonElement input)
        {
            Identity = new("user-a", input.GetProperty("claims").EnumerateObject().ToDictionary(p => p.Name,
                p => p.Value.ValueKind switch { JsonValueKind.True => (object)true, JsonValueKind.False => false,
                    JsonValueKind.Number => p.Value.GetInt32(), _ => p.Value.GetString()! }));
            if (input.TryGetProperty("roleExists", out var exists) && exists.GetBoolean())
                Role = new(input.TryGetProperty("role", out var role) ? role.GetString() : null);
            if (input.TryGetProperty("tokenError", out var tokenError) && tokenError.GetBoolean()) TokenException = new InvalidFirebaseTokenException();
            if (input.TryGetProperty("roleError", out var roleError) && roleError.GetBoolean()) RoleException = new Exception("sensitive-provider-detail");
        }
        public RequestIdentity Identity { get; set; } = new("user-a", new Dictionary<string, object>());
        public AdminRole? Role { get; set; }
        public Exception? TokenException { get; set; }
        public Exception? RoleException { get; set; }
        public string? VerifiedToken { get; private set; }
        public int RoleReads { get; private set; }
        public bool IsConfigured => TokenException is not FirebaseConfigurationException;
        public Task<RequestIdentity> VerifyAsync(string token, CancellationToken cancellationToken)
        {
            VerifiedToken = token;
            return TokenException is { } error ? Task.FromException<RequestIdentity>(error) : Task.FromResult(Identity);
        }
        public Task<AdminRole?> ReadAsync(string uid, CancellationToken cancellationToken)
        {
            Assert.Equal("user-a", uid);
            RoleReads++;
            return RoleException is { } error ? Task.FromException<AdminRole?>(error) : Task.FromResult(Role);
        }
    }
}
