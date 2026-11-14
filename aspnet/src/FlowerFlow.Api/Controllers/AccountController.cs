using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using FlowerFlow.Domain.Identity;
using FlowerFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FlowerFlow.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/account")]
public sealed class AccountController(FlowerFlowDbContext db, IConfiguration configuration) : ControllerBase
{
    [HttpGet("me")]
    [ProducesResponseType(typeof(AccountProfileDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AccountProfileDto>> Me(CancellationToken cancellationToken)
    {
        if (!HasDatabase()) return DatabaseNotConfigured();
        var uid = User.FindFirstValue("firebase_uid");
        if (uid is null) return Unauthorized();

        var account = await db.Users.AsNoTracking()
            .Where(user => user.FirebaseUid == uid && user.IsActive)
            .Select(user => new AccountProfileDto(user.FirebaseUid, user.Email, user.DisplayName, user.Phone, user.Role))
            .SingleOrDefaultAsync(cancellationToken);

        return account is null
            ? NotFound(new ProblemDetails { Status = StatusCodes.Status404NotFound, Title = "Account profile not found" })
            : Ok(account);
    }

    [HttpPut("profile")]
    [ProducesResponseType(typeof(AccountProfileDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AccountProfileDto>> UpdateProfile(
        UpdateAccountProfileRequest request,
        CancellationToken cancellationToken)
    {
        if (!HasDatabase()) return DatabaseNotConfigured();
        var uid = User.FindFirstValue("firebase_uid");
        if (uid is null) return Unauthorized();

        var account = await db.Users.SingleOrDefaultAsync(user => user.FirebaseUid == uid, cancellationToken);
        if (account is { IsActive: false }) return Forbid();

        var tokenEmail = User.FindFirstValue(ClaimTypes.Email);
        if (account is null)
        {
            account = new User
            {
                FirebaseUid = uid,
                Email = tokenEmail,
                DisplayName = request.DisplayName.Trim(),
                Phone = NormalizePhone(request.Phone),
                Role = "customer",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.Users.Add(account);
        }
        else
        {
            // Keep role and activation under server-side control; profile input cannot alter either.
            account.Email = tokenEmail ?? account.Email;
            account.DisplayName = request.DisplayName.Trim();
            account.Phone = NormalizePhone(request.Phone);
            account.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
        return Ok(new AccountProfileDto(account.FirebaseUid, account.Email, account.DisplayName, account.Phone, account.Role));
    }

    private bool HasDatabase() => !string.IsNullOrWhiteSpace(configuration.GetConnectionString("FlowerFlow"));

    private ObjectResult DatabaseNotConfigured() => Problem(
        statusCode: StatusCodes.Status503ServiceUnavailable,
        title: "Account database is not configured",
        detail: "Set ConnectionStrings__FlowerFlow to a development SQL Server connection string.");

    private static string? NormalizePhone(string? phone) => string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
}

public sealed record UpdateAccountProfileRequest(
    [property: Required, StringLength(120, MinimumLength = 1)] string DisplayName,
    [property: StringLength(20)] string? Phone);

public sealed record AccountProfileDto(string Uid, string? Email, string? DisplayName, string? Phone, string Role);
