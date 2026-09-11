using System.Security.Claims;
using JetReportDesigner.Api.Contracts;
using JetReportDesigner.Api.Infrastructure;
using JetReportDesigner.Storage.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace JetReportDesigner.Api.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public sealed class AuthController(
    UserManager<AppUser> users,
    JwtTokenService tokens) : ControllerBase
{
    /// <summary>Creates an account. The very first user to register becomes a
    /// <see cref="AppRole.Designer"/> (bootstrap admin); everyone after that starts as a
    /// <see cref="AppRole.Viewer"/> and is promoted later by an existing Designer.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        var user = new AppUser { UserName = request.Email, Email = request.Email };
        var result = await users.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(error.Code, error.Description);
            }

            return ValidationProblem(ModelState);
        }

        var isFirstUser = users.Users.Count() == 1;
        var role = isFirstUser ? AppRole.Designer : AppRole.Viewer;
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

    /// <summary>Lists every user and their role. Designer-only — this is the user administration screen.</summary>
    [HttpGet("users")]
    [Authorize(Policy = AuthPolicies.Designer)]
    public async Task<ActionResult<IReadOnlyList<UserResponse>>> ListUsers(CancellationToken cancellationToken)
    {
        var list = new List<UserResponse>();
        foreach (var user in users.Users.OrderBy(u => u.Email).ToList())
        {
            list.Add(await ToUserResponse(user));
        }

        return Ok(list);
    }

    /// <summary>Promotes or demotes a user between Designer and Viewer. Designer-only.</summary>
    [HttpPut("users/{id:guid}/role")]
    [Authorize(Policy = AuthPolicies.Designer)]
    public async Task<ActionResult<UserResponse>> SetRole(Guid id, [FromBody] SetRoleRequest request)
    {
        if (request.Role is not (AppRole.Designer or AppRole.Viewer))
        {
            return ValidationProblem($"Role must be '{AppRole.Designer}' or '{AppRole.Viewer}'.");
        }

        var user = await users.FindByIdAsync(id.ToString());
        if (user is null)
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
