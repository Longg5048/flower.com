using FlowerFlow.Api.Authentication;
using FlowerFlow.Api.Controllers;
using FlowerFlow.Api.Hosting;
using FlowerFlow.Application.Catalog;
using FlowerFlow.Application.Identity;
using FlowerFlow.Application.ShopSettings;
using FlowerFlow.Application.Accounts;
using FlowerFlow.Application.Admin;
using FlowerFlow.Application.Commerce;
using FlowerFlow.Application.Orders;
using FlowerFlow.Application.Payments;
using FlowerFlow.Application.DesignRequests;
using FlowerFlow.Infrastructure.Vnpay;
using FlowerFlow.Infrastructure.Firebase;
using FlowerFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.AddMainBackendHosting();
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddAuthorization();
builder.Services.AddSingleton(new FirebaseConnectionOptions(
    builder.Configuration["Firebase:ProjectId"] ?? Environment.GetEnvironmentVariable("FIREBASE_PROJECT_ID"),
    Environment.GetEnvironmentVariable("FIREBASE_SERVICE_ACCOUNT_JSON"),
    AllowApplicationDefaultCredentials: true));
builder.Services.AddSingleton<FirebaseConnection>();
builder.Services.AddSingleton<FirebaseIdentityAdapter>();
builder.Services.AddSingleton<IFirebaseTokenVerifier>(services => services.GetRequiredService<FirebaseIdentityAdapter>());
builder.Services.AddSingleton<IAdminRoleReader>(services =>
    new FirestoreAdminRoleReader(services.GetRequiredService<FirebaseConnection>().GetDatabase));
builder.Services.AddSingleton<RequestIdentityService>();
builder.Services.AddSingleton<RequestIdentityHttpService>();
builder.Services.AddSingleton<IProductReader>(services =>
    new FirestoreProductReader(services.GetRequiredService<FirebaseConnection>().GetDatabase));
builder.Services.AddSingleton<IProductCatalogDefaults, EmbeddedProductCatalog>();
builder.Services.AddSingleton<ProductSerializer>();
builder.Services.AddSingleton<ProductsService>();
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddSingleton<IFlowerPriceStore>(services =>
    new FirestoreFlowerPriceStore(services.GetRequiredService<FirebaseConnection>().GetDatabase));
builder.Services.AddSingleton<FlowerPricesService>();
builder.Services.AddSingleton<FlowerPricesHttpEndpoint>();
builder.Services.AddSingleton<IImageStore>(services =>
    new FirestoreImageStore(services.GetRequiredService<FirebaseConnection>().GetDatabase));
builder.Services.AddSingleton<ProductImagesService>();
builder.Services.AddSingleton<ReviewImagesService>();
builder.Services.AddSingleton<ISettingsStore>(services => new FirestoreSettingsStore(services.GetRequiredService<FirebaseConnection>().GetDatabase));
builder.Services.AddSingleton<SettingsService>();
builder.Services.AddSingleton<ILoginPolicyStore>(services => new FirestoreLoginPolicyStore(services.GetRequiredService<FirebaseConnection>().GetDatabase));
builder.Services.AddSingleton<LoginProtectionService>();
builder.Services.AddSingleton<ICustomTokenIssuer, FirebaseCustomTokenIssuer>();
builder.Services.AddSingleton<IFirebaseIdentityCredentials, FirebaseIdentityCredentials>();
builder.Services.AddSingleton(new PasswordAuthOptions(Environment.GetEnvironmentVariable("FIREBASE_AUTH_PUBLIC_API_KEY"),
    Environment.GetEnvironmentVariable("FIREBASE_AUTH_SERVER_API_KEY"), Environment.GetEnvironmentVariable("FIREBASE_SERVICE_ACCOUNT_JSON")));
builder.Services.AddHttpClient<IPasswordAuth, FirebasePasswordAuth>(client => client.Timeout = TimeSpan.FromSeconds(10))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false }).RemoveAllLoggers();
builder.Services.AddSingleton<LoginService>();
builder.Services.AddSingleton<IAccountsAdminGateway, FirebaseAccountsAdminGateway>();
builder.Services.AddSingleton<IAdminStore>(services => new FirestoreAdminStore(services.GetRequiredService<FirebaseConnection>().GetDatabase));
builder.Services.AddSingleton<AdminService>();
builder.Services.AddSingleton<ICommerceStore>(services => new FirestoreCommerceStore(services.GetRequiredService<FirebaseConnection>().GetDatabase));
builder.Services.AddSingleton(new OrderSecurityOptions(Environment.GetEnvironmentVariable("FIREBASE_SERVICE_ACCOUNT_JSON"), Environment.GetEnvironmentVariable("CRON_SECRET")));
builder.Services.AddSingleton<CustomerIdentityService>();
builder.Services.AddSingleton<ReservationsService>();
builder.Services.AddSingleton<TrackingService>();
builder.Services.AddSingleton<CheckoutService>();
builder.Services.AddSingleton<ReviewsService>();
builder.Services.AddSingleton<OrderAdminIdentity>();
builder.Services.AddSingleton<PaymentAdminService>();
builder.Services.AddSingleton<ICommerceRandom, CommerceRandom>();
builder.Services.AddSingleton<IVnpayGateway, VnpayGatewayAdapter>();
builder.Services.AddSingleton<PaymentsService>();
builder.Services.AddSingleton<VnpayIpnService>();
builder.Services.AddSingleton<DesignRequestsService>();
builder.Services.AddAuthentication("FirebaseBearer")
    .AddScheme<AuthenticationSchemeOptions, FirebaseBearerHandler>("FirebaseBearer", _ => { });

var connectionString = builder.Configuration.GetConnectionString("FlowerFlow");
builder.Services.AddDbContext<FlowerFlowDbContext>(options =>
{
    if (!string.IsNullOrWhiteSpace(connectionString))
        options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure());
});

var app = builder.Build();
app.UseExceptionHandler();
app.UseMainBackendHosting();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "FlowerFlow API v1"));
}

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "flowerflow-api",
    databaseConfigured = !string.IsNullOrWhiteSpace(connectionString)
}));

app.MapGet("/health/database", async (FlowerFlowDbContext db, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(connectionString))
        return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "Database connection is not configured");

    try
    {
        var canConnect = await db.Database.CanConnectAsync(cancellationToken);
        return canConnect
            ? Results.Ok(new { status = "ok", database = "connected" })
            : Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "SQL Server is not reachable");
    }
    catch (Exception)
    {
        return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "SQL Server is not reachable");
    }
});

app.MapControllers();
app.MapMainBackendReadiness();
app.Run();

// Exposes the entry point for future ASP.NET integration tests.
public partial class Program;
