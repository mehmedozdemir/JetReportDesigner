using JetReportDesigner.Api.Contracts;
using JetReportDesigner.Storage.Jobs;
using Microsoft.AspNetCore.Mvc;

namespace JetReportDesigner.Api.Controllers;

/// <summary>Status and results for background report jobs — tenant-wide, like the reports
/// list itself (not scoped to who created each job).</summary>
[ApiController]
[Route("api/jobs")]
[Produces("application/json")]
public sealed class JobsController(IReportJobRepository jobs) : ControllerBase
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

    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
    {
        var result = await jobs.GetResultAsync(id, cancellationToken);
        return result is null ? NotFound() : File(result.Content, result.ContentType, result.FileName);
    }
}
