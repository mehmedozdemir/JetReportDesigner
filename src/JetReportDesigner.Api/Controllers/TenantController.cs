using JetReportDesigner.Api.Contracts;
using JetReportDesigner.Api.Infrastructure;
using JetReportDesigner.Storage.Entities;
using JetReportDesigner.Storage.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JetReportDesigner.Api.Controllers;

[ApiController]
[Route("api/tenant")]
[Produces("application/json")]
public sealed class TenantController(
    ITenantRepository tenants,
    ITenantInviteRepository invites,
    ICurrentTenant currentTenant,
    AccountMail mail,
    Microsoft.AspNetCore.Identity.UserManager<AppUser> users,
    JetReportDesigner.Api.Localization.IApiStrings strings) : ControllerBase
{
    private static readonly TimeSpan DefaultInviteTtl = TimeSpan.FromHours(72);
    private const int MaxInviteTtlHours = 24 * 30;

    /// <summary>The caller's own organization. Any authenticated role.</summary>
    [HttpGet]
    public async Task<ActionResult<TenantResponse>> Get(CancellationToken cancellationToken)
    {
        var tenant = await tenants.GetAsync(currentTenant.TenantId, cancellationToken);
        return tenant is null ? NotFound() : Ok(new TenantResponse(tenant.Id, tenant.Name, tenant.CreatedAtUtc));
    }

    /// <summary>Renames the organization. Designer-only.</summary>
    [HttpPut]
    [Authorize(Policy = AuthPolicies.Designer)]
    public async Task<ActionResult<TenantResponse>> Rename([FromBody] RenameTenantRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 200)
        {
            ModelState.AddModelError("name", strings["tenant.nameInvalid"]);
            return ValidationProblem(ModelState);
        }

        var renamed = await tenants.RenameAsync(currentTenant.TenantId, name, cancellationToken);
        return renamed is null ? NotFound() : Ok(new TenantResponse(renamed.Id, renamed.Name, renamed.CreatedAtUtc));
    }

    [HttpGet("capabilities")]
    public async Task<ActionResult<TenantCapabilitiesResponse>> Capabilities(CancellationToken cancellationToken) =>
        Ok(new TenantCapabilitiesResponse(await mail.IsConfiguredAsync(currentTenant.TenantId, cancellationToken)));

    /// <summary>Generates an invite code for someone to join this tenant. Designer-only.</summary>
    [HttpPost("invites")]
    [Authorize(Policy = AuthPolicies.Designer)]
    public async Task<ActionResult<InviteResponse>> CreateInvite([FromBody] CreateInviteRequest request, CancellationToken cancellationToken)
    {
        if (request.Role is not (AppRole.Designer or AppRole.Viewer))
        {
            return ValidationProblem($"Role must be '{AppRole.Designer}' or '{AppRole.Viewer}'.");
        }

        if (request.ExpiresInHours is { } hours && (hours < 1 || hours > MaxInviteTtlHours))
        {
            return ValidationProblem($"expiresInHours must be between 1 and {MaxInviteTtlHours}.");
        }

        var ttl = request.ExpiresInHours is { } h ? TimeSpan.FromHours(h) : DefaultInviteTtl;
        var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")!.Value);

        var email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        if (email is not null && (!email.Contains('@', StringComparison.Ordinal) || email.Length > 256))
        {
            ModelState.AddModelError("email", strings["tenant.inviteEmailInvalid"]);
            return ValidationProblem(ModelState);
        }

        if (email is not null && await users.FindByEmailAsync(email) is not null)
        {
            ModelState.AddModelError("email", strings["tenant.inviteEmailTaken"]);
            return ValidationProblem(ModelState);
        }

        var created = await invites.CreateAsync(currentTenant.TenantId, request.Role, userId, ttl, cancellationToken, email);
        var link = mail.InviteLink(Request, created.Code);

        var sent = false;
        if (email is not null)
        {
            var tenant = await tenants.GetAsync(currentTenant.TenantId, cancellationToken);
            var inviter = await users.FindByIdAsync(userId.ToString());
            sent = await mail.SendInviteAsync(
                currentTenant.TenantId, email, tenant?.Name ?? string.Empty,
                inviter?.DisplayName ?? inviter?.Email ?? string.Empty, link, created.Code, cancellationToken);
        }

        return Ok(new InviteResponse(created.Code, created.Role, created.ExpiresAtUtc, link, created.Email, sent));
    }

    /// <summary>Lists this tenant's pending (unused, unexpired) invites. Designer-only.</summary>
    [HttpGet("invites")]
    [Authorize(Policy = AuthPolicies.Designer)]
    public async Task<ActionResult<IReadOnlyList<PendingInviteResponse>>> ListInvites(CancellationToken cancellationToken)
    {
        var list = await invites.ListPendingAsync(currentTenant.TenantId, cancellationToken);
        return Ok(list.Select(i => new PendingInviteResponse(i.Code, i.Role, i.CreatedAtUtc, i.ExpiresAtUtc, mail.InviteLink(Request, i.Code), i.Email)).ToList());
    }

    /// <summary>Revokes a not-yet-used invite. Designer-only.</summary>
    [HttpDelete("invites/{code}")]
    [Authorize(Policy = AuthPolicies.Designer)]
    public async Task<IActionResult> RevokeInvite(string code, CancellationToken cancellationToken) =>
        await invites.RevokeAsync(currentTenant.TenantId, code, cancellationToken) ? NoContent() : NotFound();
}
