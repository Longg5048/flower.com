using System.Text.RegularExpressions;
using FlowerFlow.Application.Identity;

namespace FlowerFlow.Application.Orders;

public sealed class CustomerIdentityService(IFirebaseTokenVerifier verifier)
{
    public async Task<RequestIdentity?> ReadAsync(string? authorization, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(authorization)) return null;
        var bearer = Regex.Match(authorization, @"\ABearer\s+(\S+)\z", RegexOptions.IgnoreCase);
        if (!bearer.Success) throw new InvalidFirebaseTokenException();
        return await verifier.VerifyAsync(bearer.Groups[1].Value, cancellationToken);
    }
}
