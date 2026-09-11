using JetReportDesigner.Core.Binding;
using JetReportDesigner.Core.Model;
using JetReportDesigner.DataSources;

namespace JetReportDesigner.Rendering.Layout;

/// <summary>
/// Builds a <see cref="RenderDocument"/> from a free-layout report: a single page,
/// elements positioned absolutely, bindings resolved against row 0 of the report's
/// first data source. A table element iterates its own data source.
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
        var primaryRows = data.Get(primarySource).Rows;
        var context = new BindingContext(data.Row(primarySource, 0), parameters)
        {
            Culture = CultureResolver.Resolve(report.Culture),
            AggregateRows = primaryRows,
            RowNumber = primaryRows.Count > 0 ? 1 : 0,
            TotalRows = primaryRows.Count,
        };

        var primitives = new List<RenderPrimitive>();
        if (report.Page.BackgroundImage is { Source: { Length: > 0 } pageBg } pageBgSpec)
        {
            primitives.Add(new ImagePrimitive
            {
                X = 0, Y = 0, Width = pageWidth, Height = pageHeight,
                Source = pageBg, Fit = ElementEmitter.ParseFit(pageBgSpec.Fit),
            });
        }

        foreach (var element in report.Body.Elements)
        {
            if (element.Type == ElementType.Table)
            {
                var rows = data.Get(element.Table?.DataSource ?? primarySource).Rows;
                primitives.AddRange(TableEmitter.Emit(element, report.Styles, rows, context, 0, 0));
            }
            else if (element.Type == ElementType.Chart)
            {
                var source = string.IsNullOrWhiteSpace(element.Chart?.DataSource) ? primarySource : element.Chart!.DataSource;
                primitives.AddRange(ChartEmitter.Emit(element, report.Styles, data.Get(source).Rows, context, 0, 0));
            }
            else if (element.Type == ElementType.Subreport)
            {
                if (SubreportEmitter.Emit(element, context, 0, 0) is { } placeholder)
                {
                    primitives.Add(placeholder);
                }
            }
            else
            {
                primitives.AddRange(ElementEmitter.Emit(element, report.Styles, context, 0, 0));
            }
        }

        return new RenderDocument
        {
            PageWidthPx = pageWidth,
            PageHeightPx = pageHeight,
            Pages = [new RenderPage { Primitives = primitives }],
        };
    }
}
