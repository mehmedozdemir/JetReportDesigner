using System.Security.Claims;
using JetReportDesigner.Api.Contracts;
using JetReportDesigner.Api.Localization;
using JetReportDesigner.Api.Infrastructure;
using JetReportDesigner.Storage.Entities;
using JetReportDesigner.Storage.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace JetReportDesigner.Api.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public sealed class AuthController(
    UserManager<AppUser> users,
    JwtTokenService tokens,
    ITenantRepository tenants,
    ITenantInviteRepository invites,
    ICurrentTenant currentTenant,
    AccountMail mail,
    TimeProvider clock,
    IApiStrings strings,
    ILogger<AuthController> logger) : ControllerBase
{
    private const int MaxDisplayName = 100;

    /// <summary>Creates an account. Exactly one of <c>organizationName</c> (creates a brand-new
    /// tenant; the registering user becomes its <see cref="AppRole.Designer"/>) or
    /// <c>inviteCode</c> (joins an existing tenant with the invite's role) must be given.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting(AuthPolicies.AuthRateLimit)]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        var hasOrgName = !string.IsNullOrWhiteSpace(request.OrganizationName);
        var hasInvite = !string.IsNullOrWhiteSpace(request.InviteCode);
        if (hasOrgName == hasInvite)
        {
            return ValidationProblem(strings["auth.registerChoice"]);
        }

        if (request.DisplayName?.Trim().Length > MaxDisplayName)
        {
            ModelState.AddModelError("displayName", strings["auth.nameTooLong"]);
            return ValidationProblem(ModelState);
        }

        // Check the password and email before touching invites or tenants, so a typo doesn't burn an
        // invite code or leave an empty organization behind.
        var probe = new AppUser { UserName = request.Email, Email = request.Email };
        var problems = new List<IdentityError>();
        foreach (var validator in users.PasswordValidators)
        {
            var check = await validator.ValidateAsync(users, probe, request.Password ?? string.Empty);
            problems.AddRange(check.Errors);
        }

        foreach (var validator in users.UserValidators)
        {
            var check = await validator.ValidateAsync(users, probe);
            problems.AddRange(check.Errors);
        }

        if (problems.Count > 0)
        {
            return IdentityProblem(problems);
        }

        Guid tenantId;
        string role;
        var newUserId = Guid.NewGuid();

        if (hasInvite)
        {
            // Consumed up front (not after the user is created) so a code can't be redeemed
            // twice by two concurrent registrations racing each other.
            var consumed = await invites.ConsumeAsync(request.InviteCode!, newUserId, cancellationToken);
            if (consumed is null)
            {
                ModelState.AddModelError("inviteCode", strings["auth.inviteInvalid"]);
                return ValidationProblem(ModelState);
            }

            tenantId = consumed.TenantId;
            role = consumed.Role;
        }
        else
        {
            tenantId = await tenants.CreateAsync(request.OrganizationName!.Trim(), cancellationToken);
            role = AppRole.Designer;
        }

        var user = new AppUser
        {
            Id = newUserId,
            UserName = request.Email,
            Email = request.Email,
            TenantId = tenantId,
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? null : request.DisplayName.Trim(),
            LastLoginAtUtc = clock.GetUtcNow().UtcDateTime,
        };
        var result = await users.CreateAsync(user, request.Password!);
        if (!result.Succeeded)
        {
            return IdentityProblem(result.Errors);
        }

        await users.AddToRoleAsync(user, role);

        return Ok(await BuildAuthResponse(user));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(AuthPolicies.AuthRateLimit)]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
    {
        var user = await users.FindByEmailAsync(request.Email ?? string.Empty);
        if (user is null)
        {
            return Unauthorized(new { title = strings["auth.invalidCredentials"] });
        }

        if (await users.IsLockedOutAsync(user))
        {
            return LockedOut(user);
        }

        if (!await users.CheckPasswordAsync(user, request.Password ?? string.Empty))
        {
            await users.AccessFailedAsync(user);
            if (await users.IsLockedOutAsync(user))
            {
                logger.LogWarning("Account {UserId} locked after repeated failed sign-ins", user.Id);
                return LockedOut(user);
            }

            return Unauthorized(new { title = strings["auth.invalidCredentials"] });
        }

        await users.ResetAccessFailedCountAsync(user);
        user.LastLoginAtUtc = clock.GetUtcNow().UtcDateTime;
        await users.UpdateAsync(user);
        return Ok(await BuildAuthResponse(user));
    }

    [HttpGet("me")]
    public async Task<ActionResult<UserResponse>> Me()
    {
        var user = await users.GetUserAsync(User);
        return user is null ? Unauthorized() : Ok(await ToUserResponse(user));
    }

    /// <summary>Changes the signed-in person's own name.</summary>
    [HttpPut("me")]
    public async Task<ActionResult<UserResponse>> UpdateMe([FromBody] UpdateProfileRequest request)
    {
        var user = await users.GetUserAsync(User);
        if (user is null)
        {
            return Unauthorized();
        }

        var name = request.DisplayName?.Trim();
        if (name?.Length > MaxDisplayName)
        {
            ModelState.AddModelError("displayName", strings["auth.nameTooLong"]);
            return ValidationProblem(ModelState);
        }

        user.DisplayName = string.IsNullOrEmpty(name) ? null : name;
        await users.UpdateAsync(user);
        return Ok(await ToUserResponse(user));
    }

    [HttpPost("me/password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var user = await users.GetUserAsync(User);
        if (user is null)
        {
            return Unauthorized();
        }

        if (!await users.CheckPasswordAsync(user, request.CurrentPassword ?? string.Empty))
        {
            ModelState.AddModelError("currentPassword", strings["auth.currentPasswordWrong"]);
            return ValidationProblem(ModelState);
        }

        var result = await users.ChangePasswordAsync(user, request.CurrentPassword!, request.NewPassword ?? string.Empty);
        return result.Succeeded ? NoContent() : IdentityProblem(result.Errors);
    }

    /// <summary>
    /// Emails a reset link when the account exists and its organization has a mail account. Always
    /// answers the same way, so it cannot be used to find out which addresses have accounts.
    /// </summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting(AuthPolicies.AuthRateLimit)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken ct)
    {
        var user = string.IsNullOrWhiteSpace(request.Email) ? null : await users.FindByEmailAsync(request.Email.Trim());
        if (user is not null)
        {
            var token = await users.GeneratePasswordResetTokenAsync(user);
            var sent = await mail.SendResetAsync(user.TenantId, user.Email!, mail.ResetLink(Request, user.Email!, token), ct);
            logger.LogInformation("Password reset requested for {UserId}; email sent: {Sent}", user.Id, sent);
        }

        return Accepted();
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    [EnableRateLimiting(AuthPolicies.AuthRateLimit)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        var user = string.IsNullOrWhiteSpace(request.Email) ? null : await users.FindByEmailAsync(request.Email.Trim());
        if (user is null)
        {
            return ValidationProblem(strings["auth.resetLinkInvalid"]);
        }

        var result = await users.ResetPasswordAsync(user, request.Token ?? string.Empty, request.NewPassword ?? string.Empty);
        if (!result.Succeeded)
        {
            return result.Errors.Any(e => e.Code == "InvalidToken")
                ? ValidationProblem(strings["auth.resetLinkInvalid"])
                : IdentityProblem(result.Errors);
        }

        // A reset also lifts a lockout — that is usually why the person is resetting.
        await users.SetLockoutEndDateAsync(user, null);
        await users.ResetAccessFailedCountAsync(user);
        return NoContent();
    }

    /// <summary>The organization and role an invite code leads to, for the join page. Changes nothing.</summary>
    [HttpGet("invites/{code}")]
    [AllowAnonymous]
    [EnableRateLimiting(AuthPolicies.AuthRateLimit)]
    public async Task<ActionResult<InvitePreviewResponse>> PreviewInvite(string code, CancellationToken ct)
    {
        var invite = await invites.FindValidAsync(code, ct);
        var tenant = invite is null ? null : await tenants.GetAsync(invite.TenantId, ct);
        return invite is null || tenant is null
            ? NotFound(new { title = strings["auth.inviteInvalid"] })
            : Ok(new InvitePreviewResponse(tenant.Name, invite.Role, invite.Email, invite.ExpiresAtUtc));
    }

    /// <summary>Lists every user in the caller's tenant and their role. Designer-only — this is
    /// the team administration screen.</summary>
    [HttpGet("users")]
    [Authorize(Policy = AuthPolicies.Designer)]
    public async Task<ActionResult<IReadOnlyList<UserResponse>>> ListUsers(CancellationToken cancellationToken)
    {
        var list = new List<UserResponse>();
        foreach (var user in users.Users.Where(u => u.TenantId == currentTenant.TenantId).OrderBy(u => u.Email).ToList())
        {
            list.Add(await ToUserResponse(user));
        }

        return Ok(list);
    }

    /// <summary>Promotes or demotes a user (in the caller's own tenant) between Designer and
    /// Viewer. Designer-only.</summary>
    [HttpPut("users/{id:guid}/role")]
    [Authorize(Policy = AuthPolicies.Designer)]
    public async Task<ActionResult<UserResponse>> SetRole(Guid id, [FromBody] SetRoleRequest request)
    {
        if (request.Role is not (AppRole.Designer or AppRole.Viewer))
        {
            return ValidationProblem(strings.Format("auth.roleInvalid", AppRole.Designer, AppRole.Viewer));
        }

        var user = await users.FindByIdAsync(id.ToString());
        if (user is null || user.TenantId != currentTenant.TenantId)
        {
            return NotFound();
        }

        if (CurrentUserId() == id.ToString() && request.Role == AppRole.Viewer)
        {
            return ValidationProblem(strings["auth.cannotDemoteSelf"]);
        }

        var currentRoles = await users.GetRolesAsync(user);
        await users.RemoveFromRolesAsync(user, currentRoles);
        await users.AddToRoleAsync(user, request.Role);

        return Ok(await ToUserResponse(user));
    }

    /// <summary>
    /// A password-reset link for a teammate, for a Designer to pass on — the way back in when the
    /// organization has no mail account, and the quickest way to unlock a locked-out colleague.
    /// </summary>
    [HttpPost("users/{id:guid}/reset-link")]
    [Authorize(Policy = AuthPolicies.Designer)]
    public async Task<ActionResult<ResetLinkResponse>> CreateResetLink(Guid id)
    {
        var user = await users.FindByIdAsync(id.ToString());
        if (user is null || user.TenantId != currentTenant.TenantId)
        {
            return NotFound();
        }

        var token = await users.GeneratePasswordResetTokenAsync(user);
        logger.LogInformation("Reset link for {UserId} created by {Caller}", user.Id, CurrentUserId());
        return Ok(new ResetLinkResponse(mail.ResetLink(Request, user.Email!, token), clock.GetUtcNow().UtcDateTime.AddHours(2)));
    }

    /// <summary>Removes someone from the organization — the account goes, since a user belongs to
    /// exactly one tenant here. Their reports, jobs and schedules stay: those carry a plain
    /// denormalised author id/email, not a foreign key, so nothing the team still needs is lost.
    /// Designer-only, and refuses to remove your own account — which is also what keeps an
    /// organization from losing its last Designer.</summary>
    [HttpDelete("users/{id:guid}")]
    [Authorize(Policy = AuthPolicies.Designer)]
    public async Task<IActionResult> RemoveUser(Guid id)
    {
        var user = await users.FindByIdAsync(id.ToString());
        if (user is null || user.TenantId != currentTenant.TenantId)
        {
            return NotFound();
        }

        if (CurrentUserId() == id.ToString())
        {
            return ValidationProblem(strings["auth.cannotRemoveSelf"]);
        }

        var result = await users.DeleteAsync(user);
        if (!result.Succeeded)
        {
            return Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: string.Join(" ", result.Errors.Select(e => e.Description)));
        }

        return NoContent();
    }

    private string? CurrentUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");

    private ObjectResult LockedOut(AppUser user)
    {
        var minutes = user.LockoutEnd is { } end
            ? Math.Max(1, (int)Math.Ceiling((end - clock.GetUtcNow()).TotalMinutes))
            : 15;
        return StatusCode(StatusCodes.Status423Locked, new { title = strings.Format("auth.lockedOut", minutes), lockedOut = true });
    }

    /// <summary>Identity's errors as a validation problem keyed by the field they belong to, in the caller's language.</summary>
    private ActionResult IdentityProblem(IEnumerable<IdentityError> errors)
    {
        foreach (var error in errors.DistinctBy(e => e.Code))
        {
            var field = error.Code switch
            {
                _ when error.Code.StartsWith("Password", StringComparison.Ordinal) => "password",
                "DuplicateEmail" or "DuplicateUserName" or "InvalidEmail" or "InvalidUserName" => "email",
                _ => string.Empty,
            };
            var key = $"identity.{error.Code}";
            var text = strings[key];
            ModelState.AddModelError(field, text == key ? error.Description : text);
        }

        return ValidationProblem(ModelState);
    }

    private async Task<AuthResponse> BuildAuthResponse(AppUser user)
    {
        var roles = await users.GetRolesAsync(user);
        var (token, expiresAtUtc) = tokens.CreateToken(user, roles);
        return new AuthResponse(token, expiresAtUtc, await ToUserResponse(user));
    }

    private async Task<UserResponse> ToUserResponse(AppUser user)
    {
        var roles = await users.GetRolesAsync(user);
        var locked = user.LockoutEnd is { } end && end > clock.GetUtcNow();
        return new UserResponse(
            user.Id,
            user.Email!,
            roles.ToList(),
            user.DisplayName,
            user.LastLoginAtUtc is { } last ? DateTime.SpecifyKind(last, DateTimeKind.Utc) : null,
            locked);
    }
}
