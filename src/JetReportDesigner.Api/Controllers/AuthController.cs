using System.Security.Claims;
using JetReportDesigner.Api.Contracts;
using JetReportDesigner.Api.Infrastructure;
using JetReportDesigner.Storage.Entities;
using JetReportDesigner.Storage.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace JetReportDesigner.Api.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public sealed class AuthController(
    UserManager<AppUser> users,
    JwtTokenService tokens,
    ITenantRepository tenants,
    ITenantInviteRepository invites,
    ICurrentTenant currentTenant) : ControllerBase
{
    /// <summary>Creates an account. Exactly one of <c>organizationName</c> (creates a brand-new
    /// tenant; the registering user becomes its <see cref="AppRole.Designer"/>) or
    /// <c>inviteCode</c> (joins an existing tenant with the invite's role) must be given.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        var hasOrgName = !string.IsNullOrWhiteSpace(request.OrganizationName);
        var hasInvite = !string.IsNullOrWhiteSpace(request.InviteCode);
        if (hasOrgName == hasInvite)
        {
            return ValidationProblem("Provide exactly one of organizationName (to create a new organization) or inviteCode (to join an existing one).");
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
                return ValidationProblem("This invite code is invalid, expired, or already used.");
            }

            tenantId = consumed.TenantId;
            role = consumed.Role;
        }
        else
        {
            tenantId = await tenants.CreateAsync(request.OrganizationName!.Trim(), cancellationToken);
            role = AppRole.Designer;
        }

        var user = new AppUser { Id = newUserId, UserName = request.Email, Email = request.Email, TenantId = tenantId };
        var result = await users.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(error.Code, error.Description);
            }

            return ValidationProblem(ModelState);
        }

        await users.AddToRoleAsync(user, role);

        return Ok(await BuildAuthResponse(user));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
    {
        var user = await users.FindByEmailAsync(request.Email);
        if (user is null || !await users.CheckPasswordAsync(user, request.Password))
        {
            return Unauthorized(new { title = "Invalid email or password." });
        }

        return Ok(await BuildAuthResponse(user));
    }

    [HttpGet("me")]
    public async Task<ActionResult<UserResponse>> Me()
    {
        var user = await users.GetUserAsync(User);
        if (user is null)
        {
            return Unauthorized();
        }

        return Ok(await ToUserResponse(user));
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
            return ValidationProblem($"Role must be '{AppRole.Designer}' or '{AppRole.Viewer}'.");
        }

        var user = await users.FindByIdAsync(id.ToString());
        if (user is null || user.TenantId != currentTenant.TenantId)
        {
            return NotFound();
        }

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");
        if (currentUserId == id.ToString() && request.Role == AppRole.Viewer)
        {
            return ValidationProblem("You cannot demote your own account.");
        }

        var currentRoles = await users.GetRolesAsync(user);
        await users.RemoveFromRolesAsync(user, currentRoles);
        await users.AddToRoleAsync(user, request.Role);

        return Ok(await ToUserResponse(user));
    }

    private async Task<AuthResponse> BuildAuthResponse(AppUser user)
    {
        var roles = await users.GetRolesAsync(user);
        var (token, expiresAtUtc) = tokens.CreateToken(user, roles);
        return new AuthResponse(token, expiresAtUtc, new UserResponse(user.Id, user.Email!, roles.ToList()));
    }

    private async Task<UserResponse> ToUserResponse(AppUser user)
    {
        var roles = await users.GetRolesAsync(user);
        return new UserResponse(user.Id, user.Email!, roles.ToList());
    }
}
