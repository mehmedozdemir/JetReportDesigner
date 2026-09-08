using JetReportDesigner.Core.Binding;
using JetReportDesigner.Core.Model;

namespace JetReportDesigner.Rendering.Layout;

using Row = IReadOnlyDictionary<string, object?>;

/// <summary>
/// Emits a <see cref="ElementType.Table"/>: an optional header row plus one row per
/// data-source row, laid out from the element's top-left. Row height derives from the
/// element font size; a border style draws grid lines.
/// </summary>
public static class TableEmitter
{
    public static IEnumerable<RenderPrimitive> Emit(
        ReportElement element,
        IReadOnlyDictionary<string, ReportStyle> styles,
        IReadOnlyList<Row> rows,
        BindingContext context,
        double offsetX,
        double offsetY)
    {
        var table = element.Table;
        if (table is null || table.Columns.Count == 0)
        {
            yield break;
        }

        var style = EffectiveStyle.Resolve(element, styles);
        var rowHeight = Math.Max(16, style.FontSizePt * (96.0 / 72.0) * 1.5);
        var totalWidth = table.Columns.Sum(c => c.Width);
        var x0 = element.Bounds.X + offsetX;
        var y = element.Bounds.Y + offsetY;
        var grid = style.Border?.Color;

        if (table.ShowHeader)
        {
            var cx = x0;
            foreach (var column in table.Columns)
            {
                yield return HeaderCell(column, cx, y, rowHeight, style);
                cx += column.Width;
            }

            if (grid is not null)
            {
                yield return HLine(x0, y + rowHeight, totalWidth, grid);
            }

            y += rowHeight;
        }

        foreach (var row in rows)
        {
            var rowContext = context.WithRow(row);
            var cx = x0;
            foreach (var column in table.Columns)
            {
                yield return BodyCell(column, cx, y, rowHeight, style, rowContext);
                cx += column.Width;
            }

            if (grid is not null)
            {
                yield return HLine(x0, y + rowHeight, totalWidth, grid);
            }

            y += rowHeight;
        }
    }

    private static TextPrimitive HeaderCell(TableColumn column, double x, double y, double h, EffectiveStyle style) => new()
    {
        X = x + 2,
        Y = y,
        Width = Math.Max(0, column.Width - 4),
        Height = h,
        Text = column.Header,
        FontFamily = style.FontFamily,
        FontSizePt = style.FontSizePt,
        Bold = true,
        ColorHex = style.Color,
        VAlign = VerticalAnchor.Middle,
        HAlign = Anchor(column.Align),
    };

    private static TextPrimitive BodyCell(
        TableColumn column,
        double x,
        double y,
        double h,
        EffectiveStyle style,
        BindingContext context) => new()
    {
        X = x + 2,
        Y = y,
        Width = Math.Max(0, column.Width - 4),
        Height = h,
        Text = BindingResolver.ResolveValue(column.Value, column.Format, context),
        FontFamily = style.FontFamily,
        FontSizePt = style.FontSizePt,
        ColorHex = style.Color,
        VAlign = VerticalAnchor.Middle,
        HAlign = Anchor(column.Align),
    };

    private static LinePrimitive HLine(double x, double y, double width, string color) => new()
    {
        X = x, Y = y, X2 = x + width, Y2 = y, ThicknessPx = 0.5, ColorHex = color,
    };

    private static HorizontalAnchor Anchor(TextAlign align) => align switch
    {
        TextAlign.Center => HorizontalAnchor.Center,
        TextAlign.Right => HorizontalAnchor.Right,
        _ => HorizontalAnchor.Left,
    };
}
