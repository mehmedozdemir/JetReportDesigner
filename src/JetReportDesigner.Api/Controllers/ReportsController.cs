using FluentValidation;
using JetReportDesigner.Api.Contracts;
using JetReportDesigner.Core.Model;
using JetReportDesigner.Storage.Repositories;
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
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        await repository.DeleteAsync(id, cancellationToken) ? NoContent() : NotFound();

    private static Guid? ParseIfMatch(IEnumerable<string?> headerValues)
    {
        var raw = headerValues.FirstOrDefault()?.Trim().Trim('"');
        return Guid.TryParse(raw, out var token) ? token : null;
    }
}
