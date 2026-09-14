using JetReportDesigner.Api.Contracts;
using JetReportDesigner.Rendering;
using JetReportDesigner.Storage.Repositories;
using JetReportDesigner.Storage.Sharing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JetReportDesigner.Api.Controllers;

/// <summary>
/// The public side of a share link — no login, no tenant context. Everything here is scoped
/// entirely by the unguessable token: it resolves to exactly one (tenant, report) pair and
/// never accepts a client-supplied report definition, unlike <see cref="RenderController"/>'s
/// inline render (which would let an anonymous caller probe a tenant's data connections).
/// </summary>
[ApiController]
[Route("api/share")]
[Produces("application/json")]
[AllowAnonymous]
public sealed class ShareController(IReportShareRepository shares, IReportRepository reports, ReportRenderService renderer) : ControllerBase
{
    [HttpGet("{token}")]
    public async Task<ActionResult<SharedReportResponse>> Get(string token, CancellationToken cancellationToken)
    {
        var record = await ResolveReportAsync(token, cancellationToken);
        return record is null ? NotFound() : Ok(new SharedReportResponse(record.Definition.Name));
    }

    /// <summary>HTML preview, for an &lt;iframe&gt; on the share page.</summary>
    [HttpGet("{token}/preview")]
    public async Task<IActionResult> Preview(string token, CancellationToken cancellationToken)
    {
        var record = await ResolveReportAsync(token, cancellationToken);
        if (record is null)
        {
            return NotFound();
        }

        var result = await renderer.RenderAsync(record.Definition, parameters: null, RenderFormat.Html, cancellationToken);
        return File(result.Content, result.ContentType);
    }

    /// <summary>Export. <c>format</c> = <c>pdf</c> (default) or <c>xlsx</c>.</summary>
    [HttpGet("{token}/render")]
    public async Task<IActionResult> Render(string token, [FromQuery] string format = "pdf", CancellationToken cancellationToken = default)
    {
        var record = await ResolveReportAsync(token, cancellationToken);
        if (record is null)
        {
            return NotFound();
        }

        var renderFormat = format.Equals("xlsx", StringComparison.OrdinalIgnoreCase) ? RenderFormat.Xlsx : RenderFormat.Pdf;
        var result = await renderer.RenderAsync(record.Definition, parameters: null, renderFormat, cancellationToken);
        return File(result.Content, result.ContentType, result.FileName);
    }

    private async Task<ReportRecord?> ResolveReportAsync(string token, CancellationToken cancellationToken)
    {
        var resolved = await shares.ResolveAsync(token, cancellationToken);
        return resolved is null ? null : await reports.GetForTenantAsync(resolved.TenantId, resolved.ReportId, cancellationToken);
    }
}
