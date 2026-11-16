using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using FlowerFlow.Application.Accounts;
using FlowerFlow.Application.Catalog;

namespace FlowerFlow.Infrastructure.Firebase;

public sealed record PasswordAuthOptions(string? PublicKey, string? ServerKey, string? ServiceAccountJson)
{
    public override string ToString() => "PasswordAuthOptions { credentials = redacted }";
}

public interface IFirebaseIdentityCredentials
{
    void EnsureConfigured();
    string ProjectId { get; }
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken);
}
public sealed class FirebaseIdentityCredentials(FirebaseConnection connection) : IFirebaseIdentityCredentials
{
    public void EnsureConfigured() => _ = connection.GetAuth();
    public string ProjectId => connection.GetProjectId();
    public Task<string> GetAccessTokenAsync(CancellationToken cancellationToken) => connection.GetIdentityAccessTokenAsync(cancellationToken);
}
public sealed class FirebasePasswordAuth(HttpClient http, IFirebaseIdentityCredentials connection, PasswordAuthOptions options, TimeProvider time) : IPasswordAuth
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private DateTimeOffset readyUntil;
    private const string BaseUrl = "https://identitytoolkit.googleapis.com/v1";
    private static InvalidOperationException Unavailable() => new("Password authentication is unavailable.");
    private void Configuration()
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FIREBASE_AUTH_EMULATOR_HOST"))
            || string.IsNullOrEmpty(options.PublicKey) || string.IsNullOrEmpty(options.ServerKey) || options.PublicKey == options.ServerKey)
            throw Unavailable();
    }
    private async Task<(bool Ok, int Status, JsonNode? Body)> PostAsync(string url, JsonObject body, CancellationToken cancellationToken, string? bearer = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
        if (bearer is not null) request.Headers.Authorization = new("Bearer", bearer);
        using var response = await http.SendAsync(request, cancellationToken);
        var data = await response.Content.ReadFromJsonAsync<JsonNode>(cancellationToken);
        return (response.IsSuccessStatusCode, (int)response.StatusCode, data);
    }
    public async Task AssertReadyAsync(CancellationToken cancellationToken)
    {
        connection.EnsureConfigured();
        Configuration();
        using var stream = typeof(FirebasePasswordAuth).Assembly.GetManifestResourceStream("FlowerFlow.FirebaseConfig.js")
            ?? throw Unavailable();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var source = await reader.ReadToEndAsync(cancellationToken);
        var configuredKey = Regex.Match(source, """apiKey\s*:\s*["']([^"']+)["']""").Groups[1].Value;
        if (configuredKey != options.PublicKey) throw Unavailable();
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (readyUntil > time.GetUtcNow()) return;
            foreach (var endpoint in new[] { BaseUrl + "/accounts:signInWithPassword", "https://www.googleapis.com/identitytoolkit/v3/relyingparty/verifyPassword" })
            {
                var result = await PostAsync(endpoint + "?key=" + Uri.EscapeDataString(options.PublicKey!), new JsonObject
                { ["email"] = "login-policy-probe@invalid.example", ["password"] = Guid.NewGuid().ToString(), ["returnSecureToken"] = true }, cancellationToken);
                var details = result.Body?["error"]?["details"] as JsonArray;
                if (result.Status != 403 || details is null || !details.OfType<JsonObject>().Any(detail =>
                    detail["reason"]?.GetValue<string>() == "API_KEY_SERVICE_BLOCKED"
                    && Regex.IsMatch(detail["metadata"]?["method"]?.GetValue<string>() ?? "", @"(?:SignInWithPassword|VerifyPassword|verifyPassword)$")))
                    throw Unavailable();
            }
            readyUntil = time.GetUtcNow().AddSeconds(60);
        }
        finally { gate.Release(); }
    }
    public async Task<PasswordAccount> LookupAsync(string email, CancellationToken cancellationToken)
    {
        Configuration();
        var token = await connection.GetAccessTokenAsync(cancellationToken);
        var result = await PostAsync(BaseUrl + "/projects/" + Uri.EscapeDataString(connection.ProjectId) + "/accounts:lookup",
            new JsonObject { ["email"] = new JsonArray(JsonValue.Create(email)) }, cancellationToken, token);
        if (!result.Ok || (result.Body?["users"] is not null && result.Body["users"] is not JsonArray)) throw Unavailable();
        if (result.Body?["users"] is JsonArray users && users.Count > 0)
        {
            var user = users[0]!;
            var uid = user["localId"]?.GetValue<string>();
            var version = FlowerPriceRules.Number(user["passwordUpdatedAt"]);
            if (string.IsNullOrEmpty(uid) || !double.IsFinite(version)) throw Unavailable();
            return new(uid, uid, version, user["disabled"] is JsonValue disabled && disabled.TryGetValue<bool>(out var blocked) && blocked);
        }
        if (string.IsNullOrEmpty(options.ServiceAccountJson)) throw Unavailable();
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(options.ServiceAccountJson), Encoding.UTF8.GetBytes(email));
        return new("unknown-" + Convert.ToHexStringLower(hash), null, 0);
    }
    public async Task<bool> VerifyAsync(string email, string password, PasswordAccount account, CancellationToken cancellationToken)
    {
        Configuration();
        var result = await PostAsync(BaseUrl + "/accounts:signInWithPassword?key=" + Uri.EscapeDataString(options.ServerKey!),
            new JsonObject { ["email"] = email, ["password"] = password, ["returnSecureToken"] = true }, cancellationToken);
        if (!result.Ok)
        {
            var code = (result.Body?["error"]?["message"]?.GetValue<string>() ?? "").Split(" : ")[0];
            if (code is "INVALID_LOGIN_CREDENTIALS" or "INVALID_PASSWORD" or "EMAIL_NOT_FOUND") return false;
            throw Unavailable();
        }
        if (account.Uid is null || account.Disabled || result.Body?["localId"]?.GetValue<string>() != account.Uid
            || !FlowerPriceRules.Truthy(result.Body?["idToken"])) throw Unavailable();
        return true;
    }
}
