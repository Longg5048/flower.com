using FlowerFlow.Application.Identity;

namespace FlowerFlow.Application.Orders;

public sealed class OrderAdminIdentity(IFirebaseTokenVerifier verifier, RequestIdentityService identities, IAdminRoleReader roles)
{
    public async Task<IdentityResult> VerifyAsync(string? authorization, CancellationToken cancellationToken)
    {
        if (!verifier.IsConfigured) throw new FirebaseConfigurationException();
        var result = await identities.VerifyAsync(authorization, RequiredAccess.Customer, cancellationToken);
        if (result.Failure is not null) return result;
        var actor = result.Identity!;
        if (!actor.Claims.TryGetValue("admin", out var admin) || admin is not true)
            return new(null, new(403, "forbidden", "Thao tác này chỉ dành cho quản trị viên."));
        if (await roles.ReadAsync(actor.Uid, cancellationToken) is null)
            return new(null, new(403, "forbidden", "Quyền quản trị viên đã bị thu hồi."));
        return result;
    }
}
