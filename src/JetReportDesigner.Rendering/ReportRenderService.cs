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
}

public sealed record RenderResult(byte[] Content, string ContentType, string FileName);

/// <summary>
/// Report + parameters + data → rendered output. Phase 1 handles free-layout
/// reports to PDF and HTML; banded rendering arrives in Phase 2.
/// </summary>
public sealed class ReportRenderService(ReportDataResolver dataResolver, IPdfRenderer pdfRenderer)
{
    private readonly FreeLayoutBuilder _freeLayout = new();
    private readonly BandedLayoutBuilder _bandedLayout = new();
    private readonly HtmlReportRenderer _htmlRenderer = new();

    public async Task<RenderResult> RenderAsync(
        ReportDefinition report,
        IReadOnlyDictionary<string, object?>? parameters,
        RenderFormat format,
        CancellationToken cancellationToken)
    {
        var resolvedParameters = ParameterValues.Resolve(report, parameters);
        var data = await dataResolver.ResolveAsync(report, resolvedParameters, cancellationToken);

        var document = report.LayoutMode switch
        {
            LayoutMode.Free => _freeLayout.Build(report, data, resolvedParameters),
            LayoutMode.Banded => _bandedLayout.Build(report, data, resolvedParameters),
            _ => throw new NotSupportedException($"Unknown layout mode '{report.LayoutMode}'."),
        };

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

    private static string SanitizeFileName(string name)
    {
        var chars = name.Trim().Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c).ToArray();
        return new string(chars);
    }
}
