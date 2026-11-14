var builder = WebApplication.CreateBuilder(args);
var webRootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot");
var portValue = Environment.GetEnvironmentVariable("PORT");

if (!Directory.Exists(webRootPath))
{
    throw new DirectoryNotFoundException($"Published frontend assets were not found at '{webRootPath}'.");
}

builder.WebHost.UseWebRoot(webRootPath);

if (!string.IsNullOrWhiteSpace(portValue))
{
    if (!int.TryParse(portValue, out var port) || port is < 1 or > 65535)
    {
        throw new InvalidOperationException("PORT must be a valid TCP port.");
    }

    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

app.Run();

public partial class Program { }
