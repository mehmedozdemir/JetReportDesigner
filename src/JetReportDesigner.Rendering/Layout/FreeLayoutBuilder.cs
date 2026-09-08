using JetReportDesigner.Core.Binding;
using JetReportDesigner.Core.Model;
using JetReportDesigner.DataSources;

namespace JetReportDesigner.Rendering.Layout;

/// <summary>
/// Builds a <see cref="RenderDocument"/> from a free-layout report. Phase 1: a single
/// page, elements positioned absolutely, bindings resolved against row 0 of the
/// report's first data source.
/// </summary>
public sealed class FreeLayoutBuilder
{
    public RenderDocument Build(
        ReportDefinition report,
        ReportData data,
        IReadOnlyDictionary<string, object?> parameters)
    {
        if (report.LayoutMode != LayoutMode.Free || report.Body is null)
        {
            throw new InvalidOperationException("FreeLayoutBuilder requires a free-layout report with a body.");
        }

        var (pageWidth, pageHeight) = PageGeometry.Resolve(report.Page);
        var primarySource = report.DataSources.FirstOrDefault()?.Name ?? string.Empty;
        var context = new BindingContext(data.Row(primarySource, 0), parameters);

        var primitives = report.Body.Elements
            .SelectMany(el => ElementEmitter.Emit(el, report.Styles, context, 0, 0))
            .ToList();

        return new RenderDocument
        {
            PageWidthPx = pageWidth,
            PageHeightPx = pageHeight,
            Pages = [new RenderPage { Primitives = primitives }],
        };
    }
}
