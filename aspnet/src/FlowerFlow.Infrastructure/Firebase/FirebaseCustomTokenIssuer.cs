using FlowerFlow.Application.Accounts;

namespace FlowerFlow.Infrastructure.Firebase;

public sealed class FirebaseCustomTokenIssuer(FirebaseConnection connection) : ICustomTokenIssuer
{
    public Task<string> CreateAsync(string uid, CancellationToken cancellationToken) => connection.GetAuth().CreateCustomTokenAsync(uid, cancellationToken);
}
