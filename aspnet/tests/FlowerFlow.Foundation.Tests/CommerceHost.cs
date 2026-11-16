using System.Net;
using FlowerFlow.Application.Commerce;
using FlowerFlow.Application.Identity;
using FlowerFlow.Application.Payments;
using FlowerFlow.Domain.Models;
using FlowerFlow.Domain.Models.Compatibility;
using FlowerFlow.Infrastructure.Vnpay;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Configuration;

namespace FlowerFlow.Foundation.Tests;

internal sealed class CommerceHost(ICommerceStore store, LoginContractTests.Clock clock, string[]? proxies = null) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.UseWebRoot(Path.Combine(AppContext.BaseDirectory, "wwwroot"));
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection((proxies ?? []).Select((ip, index) => KeyValuePair.Create<string, string?>("Hosting:KnownProxies:" + index, ip))));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ICommerceStore>(); services.AddSingleton(store);
            services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(clock);
            services.RemoveAll<OrderSecurityOptions>(); services.AddSingleton(new OrderSecurityOptions("test-commerce-secret", "test-cron"));
            services.RemoveAll<IFirebaseTokenVerifier>(); services.AddSingleton<IFirebaseTokenVerifier, TokenVerifier>();
            services.RemoveAll<IAdminRoleReader>(); services.AddSingleton<IAdminRoleReader>(new Roles(store));
            services.AddSingleton<IStartupFilter, ClientIp>();
            services.RemoveAll<IVnpayGateway>(); services.AddSingleton<IVnpayGateway, Gateway>();
            services.RemoveAll<ICommerceRandom>(); services.AddSingleton<ICommerceRandom, Random>();
        });
    }
    private sealed class Random : ICommerceRandom
    {
        private int count;
        public string Hex(int bytes) => Convert.ToHexStringLower(Enumerable.Repeat((byte)Interlocked.Increment(ref count), bytes).ToArray());
    }
    private sealed class Gateway : IVnpayGateway
    {
        private static string? Setting(string key) => key switch
        {
            "VNPAY_TMN_CODE" => "test-tmn", "VNPAY_HASH_SECRET" => "test-vnpay-hash", "PUBLIC_APP_URL" => "https://flower.example.test", _ => null
        };
        public VnpayConfig Configuration() => VnpayGateway.GetConfig(Setting);
        public VnpaySecrets Secrets() => VnpayGateway.GetSecrets(Setting);
        public bool Verify(JsonObject parameters, string secret) => VnpayGateway.VerifySignature(parameters, secret);
        public string PaymentUrl(string txnRef, double amount, string? ip, DocumentTimestamp createdAt, DocumentTimestamp expiresAt, VnpayConfig config) =>
            VnpayGateway.CreatePaymentUrl(txnRef, amount, ip, DateTimeOffset.FromUnixTimeMilliseconds((long)createdAt.Milliseconds), DateTimeOffset.FromUnixTimeMilliseconds((long)expiresAt.Milliseconds), config);
    }
    private sealed class ClientIp : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        { app.Use((context, following) => { context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.9"); return following(); }); next(app); };
    }
    private sealed class TokenVerifier : IFirebaseTokenVerifier
    {
        public bool IsConfigured => true;
        public Task<RequestIdentity> VerifyAsync(string token, CancellationToken cancellationToken) => token switch
        {
            "customer" => Task.FromResult(new RequestIdentity("customer", new Dictionary<string, object> { ["email"] = "customer@example.test" })),
            "admin" => Task.FromResult(new RequestIdentity("admin", new Dictionary<string, object> { ["admin"] = true, ["email"] = "admin@example.test" })),
            "owner" => Task.FromResult(new RequestIdentity("owner", new Dictionary<string, object> { ["admin"] = true, ["role"] = "owner", ["email"] = "owner@example.test" })),
            _ => Task.FromException<RequestIdentity>(new InvalidFirebaseTokenException())
        };
    }
    private sealed class Roles(ICommerceStore store) : IAdminRoleReader
    {
        public async Task<AdminRole?> ReadAsync(string uid, CancellationToken cancellationToken)
        {
            var document = await store.ReadAsync("adminRoles/" + uid, cancellationToken);
            return document is null ? null : new(document.Data.GetValueOrDefault("role") as string);
        }
    }
}
