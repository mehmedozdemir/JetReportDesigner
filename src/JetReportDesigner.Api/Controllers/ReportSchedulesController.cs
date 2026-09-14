using System.Security.Claims;
using JetReportDesigner.Api.Contracts;
using JetReportDesigner.Api.Infrastructure;
using JetReportDesigner.Storage.Schedules;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JetReportDesigner.Api.Controllers;

/// <summary>Creates a recurring schedule for a report. Designer-only, like every other
/// tenant-wide automation setting (connections, the mail account).</summary>
[ApiController]
[Route("api/reports/{reportId:guid}/schedules")]
[Produces("application/json")]
[Authorize(Policy = AuthPolicies.Designer)]
public sealed class ReportSchedulesController(IReportScheduleRepository schedules) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ReportScheduleResponse>> Create(Guid reportId, [FromBody] ReportScheduleRequest request, CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")!.Value);
        var created = await schedules.CreateAsync(reportId, request.ToFields(), userId, cancellationToken);
        return created is null ? NotFound() : Ok(ReportScheduleResponse.From(created));
    }
}
