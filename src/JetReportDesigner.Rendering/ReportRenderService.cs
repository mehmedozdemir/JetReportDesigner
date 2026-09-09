using System.Text;
using JetReportDesigner.Core.Binding;
using JetReportDesigner.Core.Model;
using JetReportDesigner.DataSources;
using JetReportDesigner.Rendering.Engines;
using JetReportDesigner.Rendering.Layout;

namespace JetReportDesigner.Rendering;

public enum RenderFormat
{
    Pdf,
    Html,
    Xlsx,
}

public sealed record RenderResult(byte[] Content, string ContentType, string FileName);

/// <summary>
/// Report + parameters + data → rendered output. Phase 1 handles free-layout
/// reports to PDF and HTML; banded rendering arrives in Phase 2.
/// </summary>
public sealed class ReportRenderService(
    ReportDataResolver dataResolver,
    IPdfRenderer pdfRenderer,
    IRenderImageResolver? imageResolver = null)
{
    private readonly FreeLayoutBuilder _freeLayout = new();
    private readonly BandedLayoutBuilder _bandedLayout = new();
    private readonly HtmlReportRenderer _htmlRenderer = new();
    private readonly IRenderImageResolver _imageResolver = imageResolver ?? NullImageResolver.Instance;

    public async Task<RenderResult> RenderAsync(
        ReportDefinition report,
        IReadOnlyDictionary<string, object?>? parameters,
        RenderFormat format,
        CancellationToken cancellationToken)
    {
        var resolvedParameters = ParameterValues.Resolve(report, parameters);
        var data = await dataResolver.ResolveAsync(report, resolvedParameters, cancellationToken);

        if (format == RenderFormat.Xlsx)
        {
            var name = string.IsNullOrWhiteSpace(report.Name) ? "report" : SanitizeFileName(report.Name);
            return new RenderResult(
                XlsxReportBuilder.Build(report, data, resolvedParameters),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"{name}.xlsx");
        }

        var document = report.LayoutMode switch
        {
            LayoutMode.Free => _freeLayout.Build(report, data, resolvedParameters),
            LayoutMode.Banded => _bandedLayout.Build(report, data, resolvedParameters),
            _ => throw new NotSupportedException($"Unknown layout mode '{report.LayoutMode}'."),
        };

        await ResolveImagesAsync(document, cancellationToken);

        var safeName = string.IsNullOrWhiteSpace(report.Name) ? "report" : SanitizeFileName(report.Name);

        return format switch
        {
            RenderFormat.Pdf => new RenderResult(pdfRenderer.Render(document), "application/pdf", $"{safeName}.pdf"),
            RenderFormat.Html => new RenderResult(
                Encoding.UTF8.GetBytes(_htmlRenderer.Render(document)),
                "text/html; charset=utf-8",
                $"{safeName}.html"),
            _ => throw new ArgumentOutOfRangeException(nameof(format)),
        };
    }

    /// <summary>
    /// Resolves every <see cref="ImagePrimitive.Source"/> to bytes (once per distinct
    /// source). A reference that cannot be resolved is left without bytes and the
    /// engines skip it, so a broken image never fails the export.
    /// </summary>
    private async Task ResolveImagesAsync(RenderDocument document, CancellationToken cancellationToken)
    {
        var images = document.Pages
            .SelectMany(p => p.Primitives)
            .OfType<ImagePrimitive>()
            .ToList();

        if (images.Count == 0)
        {
            return;
        }

        var cache = new Dictionary<string, ResolvedImage?>(StringComparer.Ordinal);
        foreach (var image in images)
        {
            if (!cache.TryGetValue(image.Source, out var resolved))
            {
                resolved = await _imageResolver.ResolveAsync(image.Source, cancellationToken);
                cache[image.Source] = resolved;
            }

            if (resolved is not null)
            {
                image.Bytes = resolved.Bytes;
                image.ContentType = resolved.ContentType;
            }
        }
    }

    private static string SanitizeFileName(string name)
    {
        var chars = name.Trim().Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c).ToArray();
        return new string(chars);
    }
}
