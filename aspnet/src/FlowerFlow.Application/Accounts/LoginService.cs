using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using FlowerFlow.Application.Catalog;

namespace FlowerFlow.Application.Accounts;

public sealed record PasswordAccount(string Key, string? Uid, double PasswordUpdatedAt, bool Disabled = false);
public interface IPasswordAuth
{
    Task AssertReadyAsync(CancellationToken cancellationToken);
    Task<PasswordAccount> LookupAsync(string email, CancellationToken cancellationToken);
    Task<bool> VerifyAsync(string email, string password, PasswordAccount account, CancellationToken cancellationToken);
}
public interface ICustomTokenIssuer { Task<string> CreateAsync(string uid, CancellationToken cancellationToken); }
public sealed record LoginReply(int Status, JsonObject Body, int? RetryAfter = null);
public sealed record LoginAttempt(string Key, string LeaseId);
public sealed record LoginPolicyReply(int Status, string? Code = null, int? RetryAfter = null, LoginAttempt? Attempt = null);

public sealed class LoginService(IPasswordAuth passwords, LoginProtectionService protection, ICustomTokenIssuer tokens)
{
    public async Task<LoginReply> LoginAsync(JsonNode? body, CancellationToken cancellationToken)
    {
        var fields = body as JsonObject;
        var email = fields?["email"] is JsonValue value && value.TryGetValue<string>(out var text) ? FlowerPriceRules.Trim(text).ToLowerInvariant() : "";
        var password = fields?["password"] is JsonValue secret && secret.TryGetValue<string>(out var supplied) ? supplied : "";
        if (email.Length is < 1 or > 254 || !Regex.IsMatch(email, @"\A[^\s@]+@[^\s@]+\.[^\s@]+\z") || password.Length is < 1 or > 4096)
            return new(400, new JsonObject { ["code"] = "invalid-argument", ["error"] = "Vui lòng nhập email và mật khẩu hợp lệ." });
        LoginAttempt? attempt = null;
        try
        {
            await passwords.AssertReadyAsync(cancellationToken);
            var account = await passwords.LookupAsync(email, cancellationToken);
            var acquired = await protection.AcquireAsync(account.Key, account.PasswordUpdatedAt, cancellationToken);
            if (acquired.Code is not null) return Reply(acquired);
            attempt = acquired.Attempt!;
            var valid = await passwords.VerifyAsync(email, password, account, cancellationToken);
            var completed = await protection.FinishAsync(attempt, valid ? "success" : "invalid", cancellationToken);
            attempt = null;
            if (completed.Status != 200) return Reply(completed);
            if (account.Uid is null) throw new InvalidOperationException("Verified account has no UID.");
            return new(200, new JsonObject { ["customToken"] = await tokens.CreateAsync(account.Uid, cancellationToken) });
        }
        catch (Exception)
        {
            if (attempt is not null)
            {
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                try { await protection.FinishAsync(attempt, "unavailable", cleanup.Token); } catch (Exception) { }
            }
            cancellationToken.ThrowIfCancellationRequested();
            return Reply(new(503, "auth-service-unavailable"));
        }
    }
    private static LoginReply Reply(LoginPolicyReply result)
    {
        var message = result.Code switch
        {
            "invalid-credential" => "Email hoặc mật khẩu không chính xác.",
            "account-locked" => "Đăng nhập tạm thời bị khóa do nhiều lần thất bại.",
            "password-reset-required" => "Để bảo vệ tài khoản, vui lòng dùng Quên mật khẩu để đặt lại mật khẩu trước khi đăng nhập.",
            "login-in-progress" => "Một yêu cầu đăng nhập đang được xử lý. Vui lòng đợi rồi thử lại.",
            _ => "Chưa thể đăng nhập lúc này. Vui lòng thử lại sau."
        };
        var body = new JsonObject { ["code"] = result.Code, ["error"] = message };
        if (result.RetryAfter is > 0) body["retryAfter"] = result.RetryAfter;
        return new(result.Status, body, result.RetryAfter);
    }
}
