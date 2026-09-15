using JetReportDesigner.Api.Contracts;
using JetReportDesigner.Api.Jobs;
using JetReportDesigner.Storage.Jobs;
using Microsoft.AspNetCore.Mvc;

namespace JetReportDesigner.Api.Controllers;

/// <summary>Status and results for background report jobs — tenant-wide, like the reports
/// list itself (not scoped to who created each job).</summary>
[ApiController]
[Route("api/jobs")]
[Produces("application/json")]
public sealed class JobsController(IReportJobRepository jobs, RunningJobs runningJobs) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ReportJobResponse>>> List(CancellationToken cancellationToken)
    {
        var list = await jobs.ListAsync(cancellationToken);
        return Ok(list.Select(ReportJobResponse.From).ToList());
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
                : Problem(statusCode: StatusCodes.Status409Conflict, title: "This job just finished — there's nothing left to cancel.");
        }

        var job = await jobs.GetAsync(id, cancellationToken);
        return job is null
            ? NotFound()
            : Problem(statusCode: StatusCodes.Status409Conflict, title: $"This job is already {job.Status.ToLowerInvariant()}.");
    }

    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
    {
        var result = await jobs.GetResultAsync(id, cancellationToken);
        return result is null ? NotFound() : File(result.Content, result.ContentType, result.FileName);
    }
}
