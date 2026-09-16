using JetReportDesigner.Api.Contracts;
using JetReportDesigner.Api.Localization;
using JetReportDesigner.Api.Jobs;
using JetReportDesigner.Storage.Jobs;
using Microsoft.AspNetCore.Mvc;

namespace JetReportDesigner.Api.Controllers;

/// <summary>Status and results for background report jobs — tenant-wide, like the reports
/// list itself (not scoped to who created each job).</summary>
[ApiController]
[Route("api/jobs")]
[Produces("application/json")]
public sealed class JobsController(IReportJobRepository jobs, RunningJobs runningJobs, IApiStrings strings) : ControllerBase
{
    private const int DefaultPageSize = 25;

    /// <summary>One page of the tenant's jobs. <paramref name="status"/> repeats for an OR
    /// filter (<c>?status=Queued&amp;status=Running</c>); omitting it means every status. The
    /// nav badge asks for those two with <c>take=1</c> and reads <c>total</c>, so a busy queue
    /// costs it one row rather than a page of them.</summary>
    [HttpGet]
    public async Task<ActionResult<ReportJobPageResponse>> List(
        CancellationToken cancellationToken,
        [FromQuery] string[]? status = null,
        [FromQuery] string? sort = null,
        [FromQuery] bool desc = true,
        [FromQuery] int skip = 0,
        [FromQuery] int take = DefaultPageSize)
    {
        var query = new ReportJobQuery(status ?? [], sort ?? ReportJobSort.Created, desc, skip, take);
        var page = await jobs.ListAsync(query, cancellationToken);
        return Ok(new ReportJobPageResponse(page.Items.Select(ReportJobResponse.From).ToList(), page.Total));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ReportJobResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        var job = await jobs.GetAsync(id, cancellationToken);
        return job is null ? NotFound() : Ok(ReportJobResponse.From(job));
    }

    /// <summary>Stops a job that hasn't finished — a queued one never starts, a rendering one is
    /// actually interrupted (see <see cref="RunningJobs"/>). Already-finished jobs are a 409:
    /// there's nothing to stop, and silently succeeding would misreport what happened.</summary>
    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var outcome = await jobs.RequestCancelAsync(id, cancellationToken);
        if (outcome == CancelOutcome.Cancelled)
        {
            return NoContent();
        }

        if (outcome == CancelOutcome.Running)
        {
            // The worker marks the row Cancelled once the render actually unwinds.
            return runningJobs.Cancel(id)
                ? Accepted()
                : Problem(statusCode: StatusCodes.Status409Conflict, title: strings["job.justFinished"]);
        }

        var job = await jobs.GetAsync(id, cancellationToken);
        return job is null
            ? NotFound()
            : Problem(statusCode: StatusCodes.Status409Conflict, title: strings.Format("job.alreadyFinished", strings[$"job.status.{job.Status.ToLowerInvariant()}"]));
    }

    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
    {
        var result = await jobs.GetResultAsync(id, cancellationToken);
        return result is null ? NotFound() : File(result.Content, result.ContentType, result.FileName);
    }
}
