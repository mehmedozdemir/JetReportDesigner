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
    IRenderImageResolver? imageResolver = null,
    ISubreportResolver? subreportResolver = null)
{
    /// <summary>Parent + this many nested levels of subreports; deeper ones are reported as an error box.</summary>
    private const int MaxSubreportDepth = 4;

    private readonly FreeLayoutBuilder _freeLayout = new();
    private readonly BandedLayoutBuilder _bandedLayout = new();
    private readonly HtmlReportRenderer _htmlRenderer = new();
    private readonly IRenderImageResolver _imageResolver = imageResolver ?? NullImageResolver.Instance;
    private readonly ISubreportResolver _subreportResolver = subreportResolver ?? NullSubreportResolver.Instance;

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

        var ancestry = report.Id == Guid.Empty
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase) { report.Id.ToString() };
        await ResolveSubreportsAsync(document, ancestry, depth: 0, cancellationToken);

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

    /// <summary>
    /// Replaces every <see cref="SubreportPrimitive"/> with the referenced report's own
    /// content: resolved, laid out, recursively resolved (images, its own subreports),
    /// then scaled to the placeholder's width and clipped to its height (first page
    /// only — pagination inside a subreport is out of scope for V1). A reference that
    /// cannot be resolved, or would create a cycle or exceed <see cref="MaxSubreportDepth"/>,
    /// becomes a small error box instead of failing the whole export.
    /// </summary>
    private async Task ResolveSubreportsAsync(
        RenderDocument document,
        ISet<string> ancestry,
        int depth,
        CancellationToken cancellationToken)
    {
        foreach (var page in document.Pages)
        {
            for (var i = page.Primitives.Count - 1; i >= 0; i--)
            {
                if (page.Primitives[i] is not SubreportPrimitive placeholder)
                {
                    continue;
                }

                var replacement = await ResolveOneAsync(placeholder, ancestry, depth, cancellationToken);
                page.Primitives.RemoveAt(i);
                page.Primitives.InsertRange(i, replacement);
            }
        }
    }

    private async Task<IReadOnlyList<RenderPrimitive>> ResolveOneAsync(
        SubreportPrimitive placeholder,
        ISet<string> ancestry,
        int depth,
        CancellationToken cancellationToken)
    {
        if (depth >= MaxSubreportDepth)
        {
            return ErrorBox(placeholder, "Subreport nesting too deep");
        }

        if (ancestry.Contains(placeholder.ReportId))
        {
            return ErrorBox(placeholder, "Circular subreport reference");
        }

        var childReport = await _subreportResolver.ResolveAsync(placeholder.ReportId, cancellationToken);
        if (childReport is null)
        {
            return ErrorBox(placeholder, "Subreport not found");
        }

        var childParameters = ParameterValues.Resolve(childReport, placeholder.Parameters);
        var childData = await dataResolver.ResolveAsync(childReport, childParameters, cancellationToken);
        var childDocument = childReport.LayoutMode switch
        {
            LayoutMode.Free => _freeLayout.Build(childReport, childData, childParameters),
            LayoutMode.Banded => _bandedLayout.Build(childReport, childData, childParameters),
            _ => null,
        };

        var firstPage = childDocument?.Pages.FirstOrDefault();
        if (childDocument is null || firstPage is null || firstPage.Primitives.Count == 0)
        {
            return [];
        }

        await ResolveImagesAsync(childDocument, cancellationToken);

        var nestedAncestry = new HashSet<string>(ancestry, StringComparer.OrdinalIgnoreCase) { placeholder.ReportId };
        await ResolveSubreportsAsync(childDocument, nestedAncestry, depth + 1, cancellationToken);

        var scale = childDocument.PageWidthPx > 0 ? placeholder.Width / childDocument.PageWidthPx : 1;
        var boxBottom = placeholder.Y + placeholder.Height;
        return firstPage.Primitives
            .Select(p => PrimitiveTransform.ScaleInto(p, scale, placeholder.X, placeholder.Y, boxBottom))
            .Where(p => p is not null)
            .Select(p => p!)
            .ToList();
    }

    private static IReadOnlyList<RenderPrimitive> ErrorBox(SubreportPrimitive placeholder, string message) =>
    [
        new RectanglePrimitive
        {
            X = placeholder.X, Y = placeholder.Y, Width = placeholder.Width, Height = placeholder.Height,
            BorderThicknessPx = 1, BorderColorHex = "#dc2626",
        },
        new TextPrimitive
        {
            X = placeholder.X + 4, Y = placeholder.Y + 4,
            Width = Math.Max(0, placeholder.Width - 8), Height = Math.Max(0, placeholder.Height - 8),
            Text = message, FontSizePt = 8, ColorHex = "#dc2626",
        },
    ];

    private static string SanitizeFileName(string name)
    {
        var chars = name.Trim().Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c).ToArray();
        return new string(chars);
    }
}
