using System.Text.Json.Nodes;
using FlowerFlow.Application.Catalog;

namespace FlowerFlow.Application.Accounts;

public interface ILoginPolicyTransaction
{
    Task<JsonObject?> ReadAsync(CancellationToken cancellationToken);
    void Set(JsonObject state);
    void Delete();
}
public interface ILoginPolicyStore
{
    Task<LoginPolicyReply> RunAsync(string key, Func<ILoginPolicyTransaction, Task<LoginPolicyReply>> operation, CancellationToken cancellationToken);
}

public sealed class LoginProtectionService(ILoginPolicyStore store, TimeProvider time)
{
    private static readonly int[] LockMinutes = [15, 30, 60, 120, 240];
    private static JsonObject Empty(double version) => new()
        { ["failures"] = 0, ["lockedUntil"] = 0, ["resetRequired"] = false, ["passwordUpdatedAt"] = version };
    private static double Number(JsonObject state, string field) => FlowerPriceRules.Number(state[field]);
    private static LoginPolicyReply? Restriction(JsonObject state, long now)
    {
        if (FlowerPriceRules.Truthy(state["resetRequired"])) return new(403, "password-reset-required");
        var until = Number(state, "lockedUntil");
        return until > now ? new(429, "account-locked", (int)Math.Ceiling((until - now) / 1000)) : null;
    }
    public Task<LoginPolicyReply> AcquireAsync(string key, double passwordVersion, CancellationToken cancellationToken)
    {
        var lease = Guid.NewGuid().ToString();
        return store.RunAsync(key, async transaction =>
        {
            var record = await transaction.ReadAsync(cancellationToken) ?? Empty(passwordVersion);
            var now = time.GetUtcNow().ToUnixTimeMilliseconds();
            if (Number(record, "leaseUntil") > now) return new(409, "login-in-progress", 1);
            var state = passwordVersion > Number(record, "passwordUpdatedAt") ? Empty(passwordVersion) : record;
            if (Restriction(state, now) is { } blocked) return blocked;
            state["passwordUpdatedAt"] = Math.Max(passwordVersion, Number(state, "passwordUpdatedAt"));
            state["leaseId"] = lease; state["leaseUntil"] = now + 60_000; state["updatedAt"] = now;
            transaction.Set(state);
            return new(200, Attempt: new(key, lease));
        }, cancellationToken);
    }
    public Task<LoginPolicyReply> FinishAsync(LoginAttempt attempt, string outcome, CancellationToken cancellationToken) => store.RunAsync(attempt.Key, async transaction =>
    {
        var state = await transaction.ReadAsync(cancellationToken);
        var now = time.GetUtcNow().ToUnixTimeMilliseconds();
        if (state is null || state["leaseId"]?.GetValue<string>() != attempt.LeaseId || Number(state, "leaseUntil") <= now)
            return new(503, "auth-service-unavailable");
        if (outcome == "success") { transaction.Delete(); return new(200); }
        if (outcome == "invalid")
        {
            var failures = Number(state, "failures") + 1;
            var cycle = failures - 5;
            state["failures"] = failures;
            state["lockedUntil"] = cycle >= 0 && cycle < LockMinutes.Length ? now + LockMinutes[(int)cycle] * 60_000L : 0;
            state["resetRequired"] = cycle >= LockMinutes.Length;
        }
        state["leaseId"] = ""; state["leaseUntil"] = 0; state["updatedAt"] = now;
        transaction.Set(state);
        return Restriction(state, now) ?? new(401, "invalid-credential");
    }, cancellationToken);
}
