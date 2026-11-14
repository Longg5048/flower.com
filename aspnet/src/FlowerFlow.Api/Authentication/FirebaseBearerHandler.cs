using System.Security.Claims;
using System.Text.Encodings.Web;
using FlowerFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FlowerFlow.Api.Authentication;

public sealed class FirebaseBearerHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    FirebaseTokenVerifier verifier,
    FlowerFlowDbContext db,
    IConfiguration configuration)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authorization = Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authorization))
            return AuthenticateResult.NoResult();

        if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return AuthenticateResult.Fail("Authorization header must use the Bearer scheme.");

        if (!verifier.IsConfigured)
            return AuthenticateResult.Fail("Firebase token verification is not configured.");

        var rawToken = authorization["Bearer ".Length..].Trim();
        if (rawToken.Length == 0)
            return AuthenticateResult.Fail("Bearer token is empty.");

        try
        {
            var token = await verifier.VerifyAsync(rawToken, Context.RequestAborted);
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, token.Uid),
                new("firebase_uid", token.Uid)
            };
            if (token.Claims.TryGetValue("email", out var email) && email is string emailValue)
                claims.Add(new Claim(ClaimTypes.Email, emailValue));
            if (token.Claims.TryGetValue("name", out var name) && name is string nameValue)
                claims.Add(new Claim(ClaimTypes.Name, nameValue));

            if (!string.IsNullOrWhiteSpace(configuration.GetConnectionString("FlowerFlow")))
            {
                var user = await db.Users.AsNoTracking()
                    .SingleOrDefaultAsync(candidate => candidate.FirebaseUid == token.Uid, Context.RequestAborted);
                if (user is { IsActive: false })
                    return AuthenticateResult.Fail("This FlowerFlow account is inactive.");

                var hasAdminClaim = token.Claims.TryGetValue("admin", out var adminClaim) && adminClaim is true;
                var claimedRole = token.Claims.TryGetValue("role", out var roleClaim) ? roleClaim as string : null;
                if (user is { IsActive: true, Role: "admin" } && hasAdminClaim && claimedRole == "admin")
                    claims.Add(new Claim(ClaimTypes.Role, "admin"));
                if (user is { IsActive: true, Role: "owner" } && hasAdminClaim && claimedRole == "owner")
                    claims.Add(new Claim(ClaimTypes.Role, "owner"));
            }

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
        }
        catch (OperationCanceledException) when (Context.RequestAborted.IsCancellationRequested)
        {
            return AuthenticateResult.Fail("Request was cancelled.");
        }
        catch (Exception exception)
        {
            Logger.LogWarning("Firebase bearer token authentication failed ({ExceptionType}).", exception.GetType().Name);
            return AuthenticateResult.Fail("Firebase bearer token is invalid or could not be verified.");
        }
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        if (!verifier.IsConfigured)
        {
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await Response.WriteAsJsonAsync(new
            {
                type = "https://httpstatuses.com/503",
                title = "Firebase authentication is not configured",
                status = StatusCodes.Status503ServiceUnavailable
            });
            return;
        }

        await base.HandleChallengeAsync(properties);
    }
}
