using System.Text.Json;
using FlowerFlow.Api.Authentication;
using FlowerFlow.Application.Identity;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace FlowerFlow.Foundation.Tests;

public sealed class IdentityHttpTests
{
    [Theory]
    [InlineData("", 401, "unauthenticated")]
    [InlineData("Bearer revoked", 401, "unauthenticated")]
    [InlineData("Bearer customer", 403, "permission-denied")]
    [InlineData("Bearer config", 503, "admin-api-not-configured")]
    [InlineData("Bearer outage", 503, "authorization-service-unavailable")]
    public async Task Transport_preserves_failure_status_json_and_no_store(string header, int status, string code)
    {
        var backend = new Backend();
        var service = new RequestIdentityHttpService(new(backend, backend));
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = header;
        using var output = new MemoryStream();
        context.Response.Body = output;
        Assert.Null(await service.RequireAsync(context, RequiredAccess.Admin));
        Assert.Equal(status, context.Response.StatusCode);
        Assert.Equal("no-store", context.Response.Headers.CacheControl.ToString());
        Assert.StartsWith("application/json", context.Response.ContentType);
        using var json = JsonDocument.Parse(output.ToArray());
        Assert.Equal(code, json.RootElement.GetProperty("code").GetString());
        Assert.True(json.RootElement.TryGetProperty("error", out _));
        Assert.Equal(2, json.RootElement.EnumerateObject().Count());
    }

    [Fact]
    public async Task Success_returns_identity_without_writing_response()
    {
        var backend = new Backend();
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Bearer admin";
        using var output = new MemoryStream();
        context.Response.Body = output;
        var identity = await new RequestIdentityHttpService(new(backend, backend)).RequireAsync(context, RequiredAccess.Admin);
        Assert.Equal("admin", identity?.Uid);
        Assert.Equal(0, output.Length);
        Assert.Equal(200, context.Response.StatusCode);
    }

    private sealed class Backend : IFirebaseTokenVerifier, IAdminRoleReader
    {
        public bool IsConfigured => true;
        public Task<RequestIdentity> VerifyAsync(string token, CancellationToken cancellationToken) => token switch
        {
            "revoked" => Task.FromException<RequestIdentity>(new InvalidFirebaseTokenException()),
            "config" => Task.FromException<RequestIdentity>(new FirebaseConfigurationException()),
            _ => Task.FromResult(new RequestIdentity(token, new Dictionary<string, object> { ["admin"] = token != "customer" }))
        };
        public Task<AdminRole?> ReadAsync(string uid, CancellationToken cancellationToken) => uid == "outage"
            ? Task.FromException<AdminRole?>(new Exception("sensitive-provider-detail"))
            : Task.FromResult<AdminRole?>(new("admin"));
    }
}
