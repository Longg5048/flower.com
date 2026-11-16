using System.Globalization;
using FirebaseAdmin.Auth;
using FlowerFlow.Application.Admin;

namespace FlowerFlow.Infrastructure.Firebase;

public sealed class FirebaseAccountsAdminGateway(FirebaseConnection connection) : IAccountsAdminGateway
{
    private static AuthUser Map(UserRecord user) => new(user.Uid, user.Email ?? "", user.DisplayName ?? "", user.Disabled,
        user.CustomClaims ?? new Dictionary<string, object>(), user.ProviderData.Select(provider => provider.ProviderId).ToArray(),
        user.UserMetaData.CreationTimestamp?.ToUniversalTime().ToString("r", CultureInfo.InvariantCulture) ?? "",
        user.UserMetaData.LastSignInTimestamp?.ToUniversalTime().ToString("r", CultureInfo.InvariantCulture) ?? "");
    public async Task<AuthUsersPage> ListAsync(string? pageToken, CancellationToken cancellationToken)
    {
        var page = await connection.GetAuth().ListUsersAsync(new ListUsersOptions { PageSize = 100, PageToken = pageToken }).ReadPageAsync(100, cancellationToken);
        return new(page.Select(user => Map(user)).ToArray(), page.NextPageToken);
    }
    public async Task<AuthUser> GetAsync(string uid, CancellationToken cancellationToken)
    {
        try { return Map(await connection.GetAuth().GetUserAsync(uid, cancellationToken)); }
        catch (FirebaseAuthException error) when (error.AuthErrorCode == AuthErrorCode.UserNotFound) { throw new AuthUserNotFoundException(); }
    }
    public async Task SetClaimsAsync(string uid, IReadOnlyDictionary<string, object> claims, CancellationToken cancellationToken)
    {
        try { await connection.GetAuth().SetCustomUserClaimsAsync(uid, claims, cancellationToken); }
        catch (FirebaseAuthException error) when (error.AuthErrorCode == AuthErrorCode.UserNotFound) { throw new AuthUserNotFoundException(); }
    }
    public Task RevokeAsync(string uid, CancellationToken cancellationToken) => connection.GetAuth().RevokeRefreshTokensAsync(uid, cancellationToken);
}
