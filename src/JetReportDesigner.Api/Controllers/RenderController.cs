using FluentValidation;
using JetReportDesigner.Api.Contracts;
using JetReportDesigner.Core.Model;
using JetReportDesigner.Rendering;
using JetReportDesigner.Storage.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace JetReportDesigner.Api.Controllers;

[ApiController]
public sealed class RenderController(
    IReportRepository repository,
    ReportRenderService renderer,
    IValidator<ReportDefinition> validator)
    : ControllerBase
{
    /// <summary>Render a saved report. <c>format</c> = <c>pdf</c> (default) or <c>html</c>.</summary>
    [HttpPost("api/reports/{id:guid}/render")]
    public async Task<IActionResult> RenderSaved(
        Guid id,
        [FromQuery] string format = "pdf",
        [FromBody] RenderRequest? body = null,
        CancellationToken cancellationToken = default)
    {
        var record = await repository.GetAsync(id, cancellationToken);
        return record is null
            ? NotFound()
            : await Produce(record.Definition, body?.Parameters, format, download: true, cancellationToken);
    }

    /// <summary>HTML preview of a saved report, for the designer's preview tab.</summary>
    [HttpPost("api/reports/{id:guid}/preview")]
    public async Task<IActionResult> Preview(
        Guid id,
        [FromBody] RenderRequest? body,
        CancellationToken cancellationToken)
    {
        var record = await repository.GetAsync(id, cancellationToken);
        return record is null
            ? NotFound()
            : await Produce(record.Definition, body?.Parameters, "html", download: false, cancellationToken);
    }

    /// <summary>Render an unsaved report definition (designer preview / export before first save).</summary>
    [HttpPost("api/render")]
    public async Task<IActionResult> RenderInline(
        [FromBody] InlineRenderRequest body,
        [FromQuery] string format = "html",
        CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(body.Definition, cancellationToken);
        var download = format.Equals("pdf", StringComparison.OrdinalIgnoreCase);
        return await Produce(body.Definition, body.Parameters, format, download, cancellationToken);
    }

    private async Task<IActionResult> Produce(
        ReportDefinition definition,
        Dictionary<string, object?>? parameters,
        string format,
        bool download,
        CancellationToken cancellationToken)
    {
        var renderFormat = format.Equals("html", StringComparison.OrdinalIgnoreCase)
            ? RenderFormat.Html
            : RenderFormat.Pdf;

        var result = await renderer.RenderAsync(definition, parameters, renderFormat, cancellationToken);

        return download
            ? File(result.Content, result.ContentType, result.FileName)
            : File(result.Content, result.ContentType);
    }
}
