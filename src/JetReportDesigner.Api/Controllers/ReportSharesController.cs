using System.Security.Claims;
using JetReportDesigner.Api.Contracts;
using JetReportDesigner.Api.Infrastructure;
using JetReportDesigner.Storage.Sharing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JetReportDesigner.Api.Controllers;

/// <summary>Manages public share links for a report — create/list/revoke. Designer-only;
/// the links themselves are consumed anonymously via <see cref="ShareController"/>.</summary>
[ApiController]
[Route("api/reports/{reportId:guid}/shares")]
[Produces("application/json")]
[Authorize(Policy = AuthPolicies.Designer)]
public sealed class ReportSharesController(IReportShareRepository shares) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ShareResponse>>> List(Guid reportId, CancellationToken cancellationToken)
    {
        var list = await shares.ListForReportAsync(reportId, cancellationToken);
        return Ok(list.Select(ShareResponse.From).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<ShareResponse>> Create(Guid reportId, CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")!.Value);
        var email = User.FindFirst(ClaimTypes.Email)?.Value ?? User.FindFirst("email")?.Value;

        var created = await shares.CreateAsync(reportId, userId, email, cancellationToken);
        return created is null ? NotFound() : Ok(ShareResponse.From(created));
    }

    [HttpDelete("{token}")]
    public async Task<IActionResult> Revoke(Guid reportId, string token, CancellationToken cancellationToken) =>
        await shares.RevokeAsync(reportId, token, cancellationToken) ? NoContent() : NotFound();
}
