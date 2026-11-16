using System.Net;
using System.Net.Http.Headers;
using FlowerFlow.Application.Identity;
using FlowerFlow.Infrastructure.Firebase;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace FlowerFlow.Foundation.Tests;

public sealed class ApiHostTests
{
    [Fact]
    public async Task Design_requests_fail_closed_when_Firebase_is_not_configured()
    {
        await using var host = new Host(); using var client = host.CreateClient();
        var response = await client.GetAsync("/api/design-requests");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("admin-api-not-configured", JsonNode.Parse(await response.Content.ReadAsStringAsync())!["code"]!.GetValue<string>());
    }
    [Fact]
    public async Task Products_without_firebase_configuration_return_legacy_error_json()
    {
        await using var factory = new Host();
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/products");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Assert.Equal("admin-api-not-configured", json["code"]!.GetValue<string>());
        Assert.Equal(2, json.AsObject().Count);
    }

    [Fact]
    public async Task Host_starts_without_database_or_credentials()
    {
        await using var factory = new Host();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
        var readiness = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, readiness.StatusCode);
        Assert.Equal("not-ready", JsonNode.Parse(await readiness.Content.ReadAsStringAsync())!["status"]!.GetValue<string>());
        Assert.True(readiness.Headers.Contains("Content-Security-Policy"));
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData("revoked", HttpStatusCode.Unauthorized)]
    [InlineData("valid", HttpStatusCode.ServiceUnavailable)]
    public async Task Existing_account_route_uses_extracted_verifier_and_keeps_sql_guard(string? token, HttpStatusCode expected)
    {
        await using var factory = new Host();
        using var client = factory.CreateClient();
        if (token is not null) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.GetAsync("/api/account/me");
        Assert.Equal(expected, response.StatusCode);
    }

    private sealed class Host : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:FlowerFlow"] = "", ["Firebase:ProjectId"] = ""
            }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<FirebaseConnectionOptions>();
                services.AddSingleton(new FirebaseConnectionOptions(null, null));
                services.RemoveAll<IFirebaseTokenVerifier>();
                services.AddSingleton<IFirebaseTokenVerifier, Verifier>();
            });
        }
    }

    private sealed class Verifier : IFirebaseTokenVerifier
    {
        public bool IsConfigured => true;
        public Task<RequestIdentity> VerifyAsync(string token, CancellationToken cancellationToken) => token == "valid"
            ? Task.FromResult(new RequestIdentity("test-user", new Dictionary<string, object>()))
            : Task.FromException<RequestIdentity>(new InvalidFirebaseTokenException());
    }
}
