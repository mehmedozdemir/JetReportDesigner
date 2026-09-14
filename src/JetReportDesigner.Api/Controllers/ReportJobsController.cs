using System.Security.Claims;
using JetReportDesigner.Api.Contracts;
using JetReportDesigner.Storage.Jobs;
using Microsoft.AspNetCore.Mvc;

namespace JetReportDesigner.Api.Controllers;

/// <summary>Queues a background render for a saved report — "export this without making me
/// wait". Any signed-in role: exporting is already available to Viewers via the synchronous
/// render endpoints, so the background version matches.</summary>
[ApiController]
[Route("api/reports/{reportId:guid}/jobs")]
[Produces("application/json")]
public sealed class ReportJobsController(IReportJobRepository jobs) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ReportJobResponse>> Enqueue(Guid reportId, [FromBody] EnqueueReportJobRequest request, CancellationToken cancellationToken)
    {
        var format = request.Format.Equals("xlsx", StringComparison.OrdinalIgnoreCase) ? "xlsx" : "pdf";
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")!.Value);

        var created = await jobs.EnqueueAsync(reportId, format, userId, cancellationToken);
        return created is null ? NotFound() : Ok(ReportJobResponse.From(created));
    }
}
