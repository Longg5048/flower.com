using System.Text.Json.Nodes;
using FlowerFlow.Application.Catalog;
using FlowerFlow.Application.Identity;

namespace FlowerFlow.Application.Admin;

public sealed record AuthUser(string Uid, string Email, string DisplayName, bool Disabled,
    IReadOnlyDictionary<string, object> Claims, IReadOnlyList<string> Providers, string CreationTime, string LastSignInTime);
public sealed record AuthUsersPage(IReadOnlyList<AuthUser> Users, string? NextPageToken);
public sealed class AuthUserNotFoundException() : Exception("Firebase user was not found.");
public interface IAccountsAdminGateway
{
    Task<AuthUsersPage> ListAsync(string? pageToken, CancellationToken cancellationToken);
    Task<AuthUser> GetAsync(string uid, CancellationToken cancellationToken);
    Task SetClaimsAsync(string uid, IReadOnlyDictionary<string, object> claims, CancellationToken cancellationToken);
    Task RevokeAsync(string uid, CancellationToken cancellationToken);
}
public sealed record AdminAuditDocument(string Id, JsonObject Fields, bool ServerTimestamp);
public interface IAdminStore
{
    void EnsureConfigured();
    Task SyncRoleAsync(AuthUser user, bool admin, CancellationToken cancellationToken);
    Task SyncRolesAsync(IReadOnlyList<AuthUser> users, CancellationToken cancellationToken);
    Task RecordAuditAsync(RequestIdentity actor, string action, string resourceType, string resourceId, JsonObject details, CancellationToken cancellationToken);
    Task<IReadOnlyList<AdminAuditDocument>> ReadAuditAsync(CancellationToken cancellationToken);
}
public sealed record AdminReply(JsonObject? Body, ProductFailure? Failure);

public sealed class AdminService(IAccountsAdminGateway accounts, IAdminStore store, IFirebaseTokenVerifier verifier, RequestIdentityService identities)
{
    public async Task<IdentityResult> AuthorizeAsync(string? authorization, bool owner, bool audit, CancellationToken cancellationToken)
    {
        try
        {
            if (!verifier.IsConfigured) throw new FirebaseConfigurationException();
            store.EnsureConfigured();
        }
        catch (Exception) { return new(null, new(503, "admin-api-not-configured", audit ? "API nhật ký quản trị chưa được cấu hình." : "API quản trị Firebase chưa được cấu hình.")); }
        return await identities.VerifyAsync(authorization, owner ? RequiredAccess.Owner : RequiredAccess.Admin, cancellationToken);
    }
    public static string Role(AuthUser user) => user.Claims.TryGetValue("role", out var role) && role is "owner" ? "owner"
        : user.Claims.TryGetValue("admin", out var admin) && admin is true ? "admin" : "customer";
    private static bool Page(JsonNode? body, out string? token)
    {
        token = null;
        var value = (body as JsonObject)?["pageToken"];
        if (value is null) return true;
        if (value is not JsonValue scalar || !scalar.TryGetValue<string>(out var text) || text.Length > 2048) return false;
        token = text.Length > 0 ? text : null;
        return true;
    }
    public async Task<AdminReply> ListAsync(JsonNode? body, bool owner, CancellationToken cancellationToken)
    {
        if (!Page(body, out var token)) return Error(400, "invalid-argument", "Mã phân trang không hợp lệ.");
        try
        {
            var page = await accounts.ListAsync(token, cancellationToken);
            if (owner && page.Users.Count > 0)
            {
                try { await store.SyncRolesAsync(page.Users, cancellationToken); }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                catch (Exception) { /* Preserve best-effort role backfill of the source. */ }
            }
            return new(new JsonObject
            {
                ["users"] = new JsonArray(page.Users.Select(user =>
                {
                    var fields = new JsonObject { ["uid"] = user.Uid, ["email"] = user.Email, ["displayName"] = user.DisplayName,
                        ["disabled"] = user.Disabled, ["role"] = Role(user) };
                    if (!owner)
                    {
                        fields["providers"] = new JsonArray(user.Providers.Select(value => (JsonNode?)JsonValue.Create(value)).ToArray());
                        fields["creationTime"] = user.CreationTime; fields["lastSignInTime"] = user.LastSignInTime;
                    }
                    return (JsonNode?)fields;
                }).ToArray()), ["nextPageToken"] = string.IsNullOrEmpty(page.NextPageToken) ? null : page.NextPageToken
            }, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { return Error(500, "user-list-failed", "Chưa thể tải danh sách tài khoản Firebase."); }
    }
    public async Task<AdminReply> OwnerAsync(RequestIdentity actor, JsonNode? body, CancellationToken cancellationToken)
    {
        var fields = body as JsonObject;
        var action = fields?["action"] is JsonValue value && value.TryGetValue<string>(out var text) ? text : "";
        if (action == "list") return await ListAsync(body, true, cancellationToken);
        if (action is not ("grant-admin" or "remove-admin")) return Error(400, "invalid-argument", "Thao tác quản lý chủ cửa hàng không được hỗ trợ.");
        var uid = fields?["uid"] is JsonValue identity && identity.TryGetValue<string>(out var supplied) ? supplied : "";
        if (FlowerPriceRules.Trim(uid).Length == 0 || uid.Length > 128) return Error(400, "invalid-argument", "Cần mã UID tài khoản hợp lệ.");
        if (uid == actor.Uid) return Error(400, "invalid-argument", "Chủ cửa hàng không thể tự thay đổi quyền của mình.");
        try
        {
            var user = await accounts.GetAsync(uid, cancellationToken);
            var previous = Role(user);
            if (previous == "owner") return Error(403, "permission-denied", "Không thể thay đổi quyền của tài khoản chủ cửa hàng tại đây.");
            var claims = new Dictionary<string, object>(user.Claims);
            if (action == "grant-admin") { claims["admin"] = true; claims["role"] = "admin"; }
            else
            {
                if (previous != "admin") return Error(400, "invalid-argument", "Tài khoản này hiện không có quyền quản trị viên.");
                claims.Remove("admin"); claims.Remove("role");
            }
            await accounts.SetClaimsAsync(user.Uid, claims, cancellationToken);
            await store.SyncRoleAsync(user, action == "grant-admin", cancellationToken);
            try { await accounts.RevokeAsync(user.Uid, cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception) { return Error(500, "role-updated-session-revoke-failed", "Đã đổi quyền nhưng chưa thể thu hồi phiên đăng nhập cũ. Hãy thu hồi phiên này trong Firebase Console."); }
            try { await store.RecordAuditAsync(actor, "owner-user." + action, "user-role", user.Uid,
                new JsonObject { ["previousRole"] = previous, ["nextRole"] = action == "grant-admin" ? "admin" : "customer" }, cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception) { return Error(500, "role-updated-audit-failed", "Đã đổi quyền và thu hồi phiên đăng nhập, nhưng chưa ghi được nhật ký kiểm tra."); }
            return new(new JsonObject { ["uid"] = user.Uid, ["role"] = action == "grant-admin" ? "admin" : "customer",
                ["message"] = "Role updated. The account must sign in again." }, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (AuthUserNotFoundException) { return Error(404, "user-not-found", "Không tìm thấy tài khoản Firebase."); }
        catch (Exception) { return Error(500, "role-update-failed", "Chưa thể cập nhật quyền tài khoản."); }
    }
    public async Task<AdminReply> AuditAsync(CancellationToken cancellationToken)
    {
        try
        {
            var entries = (await store.ReadAuditAsync(cancellationToken)).Select(document =>
            {
                var fields = document.Fields;
                return (JsonNode?)new JsonObject
                {
                    ["id"] = document.Id, ["actorUid"] = FlowerPriceRules.OrText(fields["actorUid"]), ["actorEmail"] = FlowerPriceRules.OrText(fields["actorEmail"]),
                    ["action"] = FlowerPriceRules.OrText(fields["action"]), ["resourceType"] = FlowerPriceRules.OrText(fields["resourceType"]),
                    ["resourceId"] = FlowerPriceRules.OrText(fields["resourceId"]),
                    ["details"] = fields["details"] is JsonObject or JsonArray ? fields["details"]!.DeepClone() : new JsonObject(),
                    ["createdAt"] = document.ServerTimestamp ? FlowerPriceRules.TimestampText(fields["createdAt"]) : ""
                };
            }).ToArray();
            return new(new JsonObject { ["entries"] = new JsonArray(entries) }, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception) { return Error(503, "admin-audit-unavailable", "Chưa thể tải nhật ký thao tác quản trị."); }
    }
    private static AdminReply Error(int status, string code, string message) => new(null, new(status, code, message));
}
