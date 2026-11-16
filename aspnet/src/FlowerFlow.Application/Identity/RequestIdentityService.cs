using System.Text.RegularExpressions;

namespace FlowerFlow.Application.Identity;

/// <summary>Authorization rules from backend/shared/firebase-admin.js, without HTTP or SDK types.</summary>
public sealed class RequestIdentityService(IFirebaseTokenVerifier verifier, IAdminRoleReader roles)
{
    public async Task<IdentityResult> VerifyAsync(string? authorization, RequiredAccess access,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(access)) throw new ArgumentOutOfRangeException(nameof(access));
        var match = Regex.Match(authorization ?? "", @"^Bearer\s+(.+)$", RegexOptions.IgnoreCase);
        if (!match.Success)
            return Fail(401, "unauthenticated", "Cần mã xác thực Firebase để thực hiện thao tác này.");

        RequestIdentity identity;
        try
        {
            identity = await verifier.VerifyAsync(match.Groups[1].Value, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (FirebaseConfigurationException)
        {
            return Fail(503, "admin-api-not-configured", "API quản trị Firebase chưa được cấu hình.");
        }
        catch (Exception)
        {
            // The JS verifier returns 401 for all token verification failures, including provider failures.
            return Fail(401, "unauthenticated", "Mã xác thực Firebase không hợp lệ, đã hết hạn hoặc đã bị thu hồi.");
        }

        if (access == RequiredAccess.Customer) return new(identity, null);
        if (!identity.Claims.TryGetValue("admin", out var admin) || admin is not true)
            return Fail(403, "permission-denied", "Bạn cần quyền quản trị viên để thực hiện thao tác này.");

        AdminRole? role;
        try { role = await roles.ReadAsync(identity.Uid, cancellationToken); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception)
        {
            return Fail(503, "authorization-service-unavailable", "Chưa thể xác minh quyền quản trị.");
        }

        var ownerClaim = identity.Claims.TryGetValue("role", out var claimedRole) && claimedRole is "owner";
        if (role is null || (access == RequiredAccess.Owner && (!ownerClaim || role.Role != "owner")))
            return Fail(403, "permission-denied", "Quyền quản trị viên đã bị thu hồi.");
        return new(identity, null);
    }

    private static IdentityResult Fail(int status, string code, string message) => new(null, new(status, code, message));
}
