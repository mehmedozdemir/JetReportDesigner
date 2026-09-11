using FluentValidation;
using JetReportDesigner.Api.Contracts;
using JetReportDesigner.Api.Infrastructure;
using JetReportDesigner.Core.Model;
using JetReportDesigner.Core.Validation;
using JetReportDesigner.Storage.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JetReportDesigner.Api.Controllers;

[ApiController]
[Route("api/reports")]
[Produces("application/json")]
public sealed class ReportsController(IReportRepository repository, IValidator<ReportDefinition> validator)
    : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ReportSummaryResponse>>> List(CancellationToken cancellationToken)
    {
        var reports = await repository.ListAsync(cancellationToken);
        return Ok(reports.Select(ReportSummaryResponse.From).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ReportResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        var record = await repository.GetAsync(id, cancellationToken);
        if (record is null)
        {
            return NotFound();
        }

        Response.Headers.ETag = $"\"{record.ConcurrencyToken}\"";
        return Ok(ReportResponse.From(record));
    }

    [HttpPost]
    [Authorize(Policy = AuthPolicies.Designer)]
    public async Task<ActionResult<ReportResponse>> Create(
        [FromBody] ReportDefinition definition,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(definition, cancellationToken);

        var created = await repository.CreateAsync(definition, cancellationToken);
        Response.Headers.ETag = $"\"{created.ConcurrencyToken}\"";
        return CreatedAtAction(nameof(Get), new { id = created.Id }, ReportResponse.From(created));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthPolicies.Designer)]
    public async Task<ActionResult<ReportResponse>> Update(
        Guid id,
        [FromBody] ReportDefinition definition,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(definition, cancellationToken);

        var expectedToken = ParseIfMatch(Request.Headers.IfMatch);
        var updated = await repository.UpdateAsync(id, definition, expectedToken, cancellationToken);
        if (updated is null)
        {
            return NotFound();
        }

        Response.Headers.ETag = $"\"{updated.ConcurrencyToken}\"";
        return Ok(ReportResponse.From(updated));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AuthPolicies.Designer)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        await repository.DeleteAsync(id, cancellationToken) ? NoContent() : NotFound();

    /// <summary>Design-time inspection: non-fatal problems (unknown bindings, empty tables, out-of-bounds elements).</summary>
    [HttpPost("validate")]
    public ActionResult<IReadOnlyList<ReportIssueResponse>> Validate([FromBody] ReportDefinition definition) =>
        Ok(ReportInspector.Inspect(definition).Select(ReportIssueResponse.From).ToList());

    [HttpGet("{id:guid}/versions")]
    public async Task<ActionResult<IReadOnlyList<ReportVersionResponse>>> Versions(Guid id, CancellationToken cancellationToken)
    {
        if (await repository.GetAsync(id, cancellationToken) is null)
        {
            return NotFound();
        }

        var versions = await repository.ListVersionsAsync(id, cancellationToken);
        return Ok(versions.Select(ReportVersionResponse.From).ToList());
    }

    [HttpGet("{id:guid}/versions/{version:int}")]
    public async Task<ActionResult<ReportVersionDetailResponse>> Version(Guid id, int version, CancellationToken cancellationToken)
    {
        var snapshot = await repository.GetVersionAsync(id, version, cancellationToken);
        return snapshot is null ? NotFound() : Ok(ReportVersionDetailResponse.From(snapshot));
    }

    [HttpPost("{id:guid}/versions/{version:int}/restore")]
    [Authorize(Policy = AuthPolicies.Designer)]
    public async Task<ActionResult<ReportResponse>> Restore(Guid id, int version, CancellationToken cancellationToken)
    {
        var restored = await repository.RestoreVersionAsync(id, version, cancellationToken);
        if (restored is null)
        {
            return NotFound();
        }

        Response.Headers.ETag = $"\"{restored.ConcurrencyToken}\"";
        return Ok(ReportResponse.From(restored));
    }

    private static Guid? ParseIfMatch(IEnumerable<string?> headerValues)
    {
        var raw = headerValues.FirstOrDefault()?.Trim().Trim('"');
        return Guid.TryParse(raw, out var token) ? token : null;
    }
}
