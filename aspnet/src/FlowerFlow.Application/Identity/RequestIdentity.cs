namespace FlowerFlow.Application.Identity;

public sealed record RequestIdentity(string Uid, IReadOnlyDictionary<string, object> Claims);
public sealed record AdminRole(string? Role);

public interface IFirebaseTokenVerifier
{
    bool IsConfigured { get; }
    Task<RequestIdentity> VerifyAsync(string token, CancellationToken cancellationToken);
}

public interface IAdminRoleReader
{
    Task<AdminRole?> ReadAsync(string uid, CancellationToken cancellationToken);
}

// Adapter exceptions deliberately exclude provider messages, tokens and credentials.
public sealed class FirebaseConfigurationException() : Exception("Firebase Admin is not configured.");
public sealed class InvalidFirebaseTokenException() : Exception("Firebase token is invalid or revoked.");

public enum RequiredAccess { Customer, Admin, Owner }
public sealed record IdentityFailure(int Status, string Code, string Message);
public sealed record IdentityResult(RequestIdentity? Identity, IdentityFailure? Failure)
{
    public bool Succeeded => Identity is not null && Failure is null;
}
