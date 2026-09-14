using JetReportDesigner.Api.Contracts;
using JetReportDesigner.Api.Infrastructure;
using JetReportDesigner.Storage.Schedules;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JetReportDesigner.Api.Controllers;

/// <summary>Manage existing schedules — tenant-wide, Designer-only.</summary>
[ApiController]
[Route("api/schedules")]
[Produces("application/json")]
[Authorize(Policy = AuthPolicies.Designer)]
public sealed class SchedulesController(IReportScheduleRepository schedules) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ReportScheduleResponse>>> List(CancellationToken cancellationToken)
    {
        var list = await schedules.ListAsync(cancellationToken);
        return Ok(list.Select(ReportScheduleResponse.From).ToList());
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ReportScheduleResponse>> Update(Guid id, [FromBody] ReportScheduleRequest request, CancellationToken cancellationToken)
    {
        var updated = await schedules.UpdateAsync(id, request.ToFields(), cancellationToken);
        return updated is null ? NotFound() : Ok(ReportScheduleResponse.From(updated));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        await schedules.DeleteAsync(id, cancellationToken) ? NoContent() : NotFound();
}
