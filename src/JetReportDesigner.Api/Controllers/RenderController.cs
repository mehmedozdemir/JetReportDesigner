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
        [FromQuery] int page = 1,
        [FromQuery] int dpi = 150,
        [FromBody] RenderRequest? body = null,
        CancellationToken cancellationToken = default)
    {
        var record = await repository.GetAsync(id, cancellationToken);
        return record is null
            ? NotFound()
            : await Produce(record.Definition, body?.Parameters, body?.Data, format, download: true, page, dpi, cancellationToken);
    }

    /// <summary>Render a saved report addressed by its code (the stable name programs use) instead of its id.</summary>
    [HttpPost("api/reports/by-code/{code}/render")]
    public async Task<IActionResult> RenderByCode(
        string code,
        [FromQuery] string format = "pdf",
        [FromQuery] int page = 1,
        [FromQuery] int dpi = 150,
        [FromBody] RenderRequest? body = null,
        CancellationToken cancellationToken = default)
    {
        var wanted = code.Trim().ToLowerInvariant();
        var summary = (await repository.ListAsync(cancellationToken)).FirstOrDefault(r => r.Code == wanted);
        var record = summary is null ? null : await repository.GetAsync(summary.Id, cancellationToken);
        return record is null
            ? NotFound()
            : await Produce(record.Definition, body?.Parameters, body?.Data, format, download: true, page, dpi, cancellationToken);
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
            : await Produce(record.Definition, body?.Parameters, body?.Data, "html", download: false, 1, 150, cancellationToken);
    }

    /// <summary>Render an unsaved report definition (designer preview / export before first save).</summary>
    [HttpPost("api/render")]
    public async Task<IActionResult> RenderInline(
        [FromBody] InlineRenderRequest body,
        [FromQuery] string format = "html",
        [FromQuery] int page = 1,
        [FromQuery] int dpi = 150,
        CancellationToken cancellationToken = default)
    {
        await validator.ValidateAndThrowAsync(body.Definition, cancellationToken);
        var download = !format.Equals("html", StringComparison.OrdinalIgnoreCase);
        return await Produce(body.Definition, body.Parameters, body.Data, format, download, page, dpi, cancellationToken);
    }

    private async Task<IActionResult> Produce(
        ReportDefinition definition,
        Dictionary<string, object?>? parameters,
        Dictionary<string, System.Text.Json.JsonElement>? data,
        string format,
        bool download,
        int page,
        int dpi,
        CancellationToken cancellationToken)
    {
        var renderFormat = format.ToLowerInvariant() switch
        {
            "html" => RenderFormat.Html,
            "xlsx" or "excel" => RenderFormat.Xlsx,
            "png" => RenderFormat.Png,
            "jpg" or "jpeg" => RenderFormat.Jpeg,
            _ => RenderFormat.Pdf,
        };

        var options = new RenderOptions(
            data?.ToDictionary(kv => kv.Key, kv => kv.Value.GetRawText(), StringComparer.Ordinal),
            page,
            dpi);
        var result = await renderer.RenderAsync(definition, parameters, renderFormat, cancellationToken, options);

        if (result.PageCount is { } pageCount)
        {
            Response.Headers["X-Page-Count"] = pageCount.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return download
            ? File(result.Content, result.ContentType, result.FileName)
            : File(result.Content, result.ContentType);
    }
}
