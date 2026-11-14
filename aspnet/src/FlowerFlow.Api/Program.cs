using FlowerFlow.Api.Authentication;
using FlowerFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddAuthorization();
builder.Services.AddSingleton<FirebaseTokenVerifier>();
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
app.Run();

// Exposes the entry point for future ASP.NET integration tests.
public partial class Program;
