using FlowerFlow.Application.Identity;

namespace FlowerFlow.Infrastructure.Firebase;

public sealed class FirebaseIdentityAdapter(FirebaseConnection connection) : IFirebaseTokenVerifier
{
    public bool IsConfigured
    {
        get
        {
            try { connection.GetAuth(); return true; }
            catch (FirebaseConfigurationException) { return false; }
        }
    }

    public async Task<RequestIdentity> VerifyAsync(string token, CancellationToken cancellationToken)
    {
        try
        {
            var decoded = await connection.GetAuth().VerifyIdTokenAsync(token, checkRevoked: true,
                cancellationToken: cancellationToken);
            return new(decoded.Uid, decoded.Claims);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (FirebaseConfigurationException) { throw; }
        catch (Exception) { throw new InvalidFirebaseTokenException(); }
    }
}

public sealed class FirestoreAdminRoleReader(Func<Google.Cloud.Firestore.FirestoreDb> database) : IAdminRoleReader
{
    public async Task<AdminRole?> ReadAsync(string uid, CancellationToken cancellationToken)
    {
        // Read every privileged request; do not cache a role that may have been revoked.
        var snapshot = await database().Collection("adminRoles").Document(uid)
            .GetSnapshotAsync(cancellationToken);
        if (!snapshot.Exists) return null;
        var fields = snapshot.ToDictionary();
        return new(fields.TryGetValue("role", out var role) ? role as string : null);
    }
}
