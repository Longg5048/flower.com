using FlowerFlow.Application.Identity;

namespace FlowerFlow.Api.Authentication;

/// <summary>HTTP boundary for the Firestore-backed routes migrated in subsequent stages.</summary>
public sealed class RequestIdentityHttpService(RequestIdentityService identities)
{
    public async Task<RequestIdentity?> RequireAsync(HttpContext context, RequiredAccess access)
    {
        var result = await identities.VerifyAsync(context.Request.Headers.Authorization.ToString(), access, context.RequestAborted);
        if (result.Failure is { } failure)
        {
            context.Response.StatusCode = failure.Status;
            context.Response.Headers.CacheControl = "no-store";
            await context.Response.WriteAsJsonAsync(new { code = failure.Code, error = failure.Message }, context.RequestAborted);
            return null;
        }
        return result.Identity;
    }
}
