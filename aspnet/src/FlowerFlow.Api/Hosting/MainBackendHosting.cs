using System.Net;
using System.Text.Json;
using FlowerFlow.Application.Accounts;
using FlowerFlow.Application.Commerce;
using FlowerFlow.Application.Orders;
using FlowerFlow.Application.Payments;
using Microsoft.AspNetCore.HttpOverrides;

namespace FlowerFlow.Api.Hosting;

public static class MainBackendHosting
{
    public static void AddMainBackendHosting(this WebApplicationBuilder builder)
    {
        // Never accept forwarded headers from arbitrary clients. Configure the immediate proxy IPs.
        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1;
            options.KnownIPNetworks.Clear(); options.KnownProxies.Clear();
            foreach (var text in builder.Configuration.GetSection("Hosting:KnownProxies").Get<string[]>() ?? [])
                options.KnownProxies.Add(IPAddress.Parse(text));
        });
        builder.Services.AddHostedService<ReservationWorker>();
    }
    public static void UseMainBackendHosting(this WebApplication app)
    {
        // With no explicit proxy, do not invoke this middleware (empty lists otherwise trust everyone).
        if ((app.Configuration.GetSection("Hosting:KnownProxies").Get<string[]>() ?? []).Length > 0) app.UseForwardedHeaders();
        using var source = typeof(MainBackendHosting).Assembly.GetManifestResourceStream("FlowerFlow.SecurityHeaders.json")!;
        using var document = JsonDocument.Parse(source);
        var headers = document.RootElement.GetProperty("headers")[0].GetProperty("headers").EnumerateArray()
            .ToDictionary(h => h.GetProperty("key").GetString()!, h => h.GetProperty("value").GetString()!);
        app.Use(async (context, next) =>
        {
            foreach (var header in headers)
                if (header.Key != "Strict-Transport-Security" || context.Request.IsHttps) context.Response.Headers[header.Key] = header.Value;
            await next(context);
        });
        app.UseDefaultFiles();
        app.UseStaticFiles(new StaticFileOptions { OnPrepareResponse = context => context.Context.Response.Headers.CacheControl = "public, max-age=0, must-revalidate" });
    }
    public static void MapMainBackendReadiness(this WebApplication app) => app.MapGet("/health/ready", async (ICommerceStore store, IPasswordAuth passwords, IVnpayGateway gateway, OrderSecurityOptions security, CancellationToken ct) =>
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(12));
        try
        {
            if (string.IsNullOrWhiteSpace(security.Secret) || string.IsNullOrWhiteSpace(security.CronSecret)) throw new InvalidOperationException();
            gateway.Configuration();
            await store.ReadAsync("metadata/deliveryZones", timeout.Token);
            await passwords.AssertReadyAsync(timeout.Token);
            return Results.Ok(new { status = "ready" });
        }
        catch (Exception) { return Results.Json(new { status = "not-ready" }, statusCode: 503); }
    });
}

internal sealed class ReservationWorker(IServiceScopeFactory scopes, IConfiguration config, ILogger<ReservationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!config.GetValue<bool>("Reservations:WorkerEnabled")) return;
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<ReservationsService>().ExpireAsync(500, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception) { logger.LogWarning("Reservation expiry did not complete; the worker will retry."); }
            try { if (!await timer.WaitForNextTickAsync(stoppingToken)) break; }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
