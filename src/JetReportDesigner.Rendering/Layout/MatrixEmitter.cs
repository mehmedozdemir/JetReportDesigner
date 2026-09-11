using JetReportDesigner.Core.Binding;
using JetReportDesigner.Core.Model;

namespace JetReportDesigner.Rendering.Layout;

using Row = IReadOnlyDictionary<string, object?>;

/// <summary>
/// Emits a <see cref="ElementType.Matrix"/>: a pivot grid built from the data at
/// render time — one row per distinct <see cref="MatrixSpec.RowField"/> value, one
/// column per distinct <see cref="MatrixSpec.ColumnField"/> value (capped at
/// <see cref="MaxColumns"/> to keep a runaway cardinality from producing an unusable
/// grid), each cell <see cref="MatrixSpec.Aggregate"/> of <see cref="MatrixSpec.ValueField"/>
/// over the matching rows. Row/column keys sort alphabetically. Placed by the builders
/// like <see cref="TableEmitter"/>.
/// </summary>
public static class MatrixEmitter
{
    private const int MaxColumns = 12;

    public static IEnumerable<RenderPrimitive> Emit(
        ReportElement element,
        IReadOnlyDictionary<string, ReportStyle> styles,
        IReadOnlyList<Row> rows,
        BindingContext context,
        double offsetX,
        double offsetY)
    {
        var matrix = element.Matrix;
        if (matrix is null || rows.Count == 0)
        {
            yield break;
        }

        var style = EffectiveStyle.Resolve(element, styles, context);
        var rowHeight = Math.Max(16, style.FontSizePt * (96.0 / 72.0) * 1.5);
        var b = element.Bounds;
        var x0 = b.X + offsetX;
        var y0 = b.Y + offsetY;
        var grid = style.Border?.Color;

        string RowKeyOf(Row r) => BindingResolver.ResolveValue(matrix.RowField, null, context.WithRow(r));
        string ColKeyOf(Row r) => BindingResolver.ResolveValue(matrix.ColumnField, null, context.WithRow(r));

        var colKeys = rows.Select(ColKeyOf).Distinct()
            .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
            .Take(MaxColumns)
            .ToList();
        var colSet = new HashSet<string>(colKeys, StringComparer.OrdinalIgnoreCase);

        var buckets = new Dictionary<string, Dictionary<string, List<Row>>>(StringComparer.OrdinalIgnoreCase);
        var rowKeys = new List<string>();
        foreach (var r in rows)
        {
            var ck = ColKeyOf(r);
            if (!colSet.Contains(ck))
            {
                continue;
            }

            var rk = RowKeyOf(r);
            if (!buckets.TryGetValue(rk, out var byCol))
            {
                buckets[rk] = byCol = new Dictionary<string, List<Row>>(StringComparer.OrdinalIgnoreCase);
                rowKeys.Add(rk);
            }

            if (!byCol.TryGetValue(ck, out var list))
            {
                byCol[ck] = list = [];
            }

            list.Add(r);
        }

        rowKeys.Sort(StringComparer.OrdinalIgnoreCase);

        IReadOnlyList<Row> RowsAt(string rowKey, string colKey) =>
            buckets.TryGetValue(rowKey, out var byCol) && byCol.TryGetValue(colKey, out var list) ? list : [];

        var rowHeaderW = Math.Min(140, b.Width * 0.3);
        var totalColW = matrix.ShowRowTotals ? Math.Min(70, b.Width * 0.15) : 0;
        var dataColW = Math.Max(24, (b.Width - rowHeaderW - totalColW) / Math.Max(1, colKeys.Count));

        var y = y0;

        // header row
        var x = x0;
        yield return Cell(matrix.RowHeader is { Length: > 0 } h ? h : FieldLabel(matrix.RowField), x, y, rowHeaderW, rowHeight, style, bold: true);
        x += rowHeaderW;
        foreach (var ck in colKeys)
        {
            yield return Cell(ck, x, y, dataColW, rowHeight, style, bold: true, align: HorizontalAnchor.Right);
            x += dataColW;
        }

        if (matrix.ShowRowTotals)
        {
            yield return Cell("Total", x, y, totalColW, rowHeight, style, bold: true, align: HorizontalAnchor.Right);
        }

        if (grid is { } gc)
        {
            yield return HLine(x0, y + rowHeight, b.Width, gc);
        }

        y += rowHeight;

        // body rows
        foreach (var rk in rowKeys)
        {
            x = x0;
            yield return Cell(rk, x, y, rowHeaderW, rowHeight, style, bold: false);
            x += rowHeaderW;

            var rowAll = new List<Row>();
            foreach (var ck in colKeys)
            {
                var cellRows = RowsAt(rk, ck);
                rowAll.AddRange(cellRows);
                var value = AggregateComputer.Compute(matrix.Aggregate, matrix.ValueField, cellRows);
                yield return Cell(BindingResolver.FormatValue(value, matrix.Format, context.Culture), x, y, dataColW, rowHeight, style, align: HorizontalAnchor.Right);
                x += dataColW;
            }

            if (matrix.ShowRowTotals)
            {
                var total = AggregateComputer.Compute(matrix.Aggregate, matrix.ValueField, rowAll);
                yield return Cell(BindingResolver.FormatValue(total, matrix.Format, context.Culture), x, y, totalColW, rowHeight, style, bold: true, align: HorizontalAnchor.Right);
            }

            if (grid is { } rgc)
            {
                yield return HLine(x0, y + rowHeight, b.Width, rgc);
            }

            y += rowHeight;
        }

        // column-totals row
        if (matrix.ShowColumnTotals)
        {
            x = x0;
            yield return Cell("Total", x, y, rowHeaderW, rowHeight, style, bold: true);
            x += rowHeaderW;
            foreach (var ck in colKeys)
            {
                var colRows = rowKeys.SelectMany(rk => RowsAt(rk, ck)).ToList();
                var value = AggregateComputer.Compute(matrix.Aggregate, matrix.ValueField, colRows);
                yield return Cell(BindingResolver.FormatValue(value, matrix.Format, context.Culture), x, y, dataColW, rowHeight, style, bold: true, align: HorizontalAnchor.Right);
                x += dataColW;
            }

            if (matrix.ShowRowTotals)
            {
                var grand = AggregateComputer.Compute(matrix.Aggregate, matrix.ValueField, rows.Where(r => colSet.Contains(ColKeyOf(r))).ToList());
                yield return Cell(BindingResolver.FormatValue(grand, matrix.Format, context.Culture), x, y, totalColW, rowHeight, style, bold: true, align: HorizontalAnchor.Right);
            }
        }
    }

    private static TextPrimitive Cell(
        string text,
        double x,
        double y,
        double w,
        double h,
        EffectiveStyle style,
        bool bold = false,
        HorizontalAnchor align = HorizontalAnchor.Left) => new()
    {
        X = x + 2, Y = y, Width = Math.Max(0, w - 4), Height = h,
        Text = text, FontFamily = style.FontFamily, FontSizePt = style.FontSizePt,
        Bold = bold, ColorHex = style.Color, HAlign = align, VAlign = VerticalAnchor.Middle,
    };

    private static LinePrimitive HLine(double x, double y, double width, string color) =>
        new() { X = x, Y = y, X2 = x + width, Y2 = y, ThicknessPx = 0.5, ColorHex = color };

    private static string FieldLabel(string? binding)
    {
        if (string.IsNullOrWhiteSpace(binding))
        {
            return string.Empty;
        }

        var trimmed = binding.Trim().Trim('{', '}');
        var dot = trimmed.LastIndexOf('.');
        return dot >= 0 ? trimmed[(dot + 1)..] : trimmed;
    }
}
