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
    ICurrentTenant currentTenant) : ControllerBase
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

        var created = await invites.CreateAsync(currentTenant.TenantId, request.Role, userId, ttl, cancellationToken);
        return Ok(new InviteResponse(created.Code, created.Role, created.ExpiresAtUtc));
    }

    /// <summary>Lists this tenant's pending (unused, unexpired) invites. Designer-only.</summary>
    [HttpGet("invites")]
    [Authorize(Policy = AuthPolicies.Designer)]
    public async Task<ActionResult<IReadOnlyList<PendingInviteResponse>>> ListInvites(CancellationToken cancellationToken)
    {
        var list = await invites.ListPendingAsync(currentTenant.TenantId, cancellationToken);
        return Ok(list.Select(i => new PendingInviteResponse(i.Code, i.Role, i.CreatedAtUtc, i.ExpiresAtUtc)).ToList());
    }

    /// <summary>Revokes a not-yet-used invite. Designer-only.</summary>
    [HttpDelete("invites/{code}")]
    [Authorize(Policy = AuthPolicies.Designer)]
    public async Task<IActionResult> RevokeInvite(string code, CancellationToken cancellationToken) =>
        await invites.RevokeAsync(currentTenant.TenantId, code, cancellationToken) ? NoContent() : NotFound();
}
