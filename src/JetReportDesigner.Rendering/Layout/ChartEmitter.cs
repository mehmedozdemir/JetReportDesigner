using System.Globalization;
using JetReportDesigner.Core.Binding;
using JetReportDesigner.Core.Model;

namespace JetReportDesigner.Rendering.Layout;

using Row = IReadOnlyDictionary<string, object?>;

/// <summary>
/// Turns an <see cref="ElementType.Chart"/> into vector primitives (bars, lines, area
/// polygons, pie wedges plus axes, gridlines, category / tick labels and a legend).
/// Placed by the builders like <see cref="TableEmitter"/>, from the element's top-left.
/// </summary>
public static class ChartEmitter
{
    private static readonly string[] Palette =
    [
        "#2563eb", "#16a34a", "#f59e0b", "#dc2626", "#7c3aed",
        "#0891b2", "#db2777", "#65a30d", "#ea580c", "#4f46e5",
    ];

    private const string AxisColor = "#9ca3af";
    private const string GridColor = "#e5e7eb";
    private const string LabelColor = "#6b7280";

    public static IEnumerable<RenderPrimitive> Emit(
        ReportElement element,
        IReadOnlyDictionary<string, ReportStyle> styles,
        IReadOnlyList<Row> rows,
        BindingContext context,
        double offsetX,
        double offsetY)
    {
        var chart = element.Chart;
        if (chart is null || chart.Series.Count == 0)
        {
            yield break;
        }

        var style = EffectiveStyle.Resolve(element, styles, context);
        var b = element.Bounds;
        double left = b.X + offsetX;
        double top = b.Y + offsetY;

        var kind = (chart.Type ?? "column").Trim().ToLowerInvariant();
        var isPie = kind == "pie";
        var isBar = kind == "bar";

        var categories = rows.Count == 0
            ? []
            : rows.Select((r, i) => string.IsNullOrWhiteSpace(chart.Category)
                    ? (i + 1).ToString(CultureInfo.InvariantCulture)
                    : BindingResolver.ResolveValue(chart.Category, null, context.WithRow(r)))
                .ToList();

        var series = chart.Series
            .Select((s, i) => new SeriesData(
                string.IsNullOrWhiteSpace(s.Name) ? $"Series {i + 1}" : s.Name,
                string.IsNullOrWhiteSpace(s.Color) ? Palette[i % Palette.Length] : s.Color!,
                s.Format,
                rows.Select(r => Numeric(s.Value, r, context)).ToArray()))
            .ToList();

        // ---- regions ----
        const double pad = 6;
        double titleH = string.IsNullOrWhiteSpace(chart.Title) ? 0 : 20;
        var legendItems = isPie
            ? categories.Select((c, i) => (c, Palette[i % Palette.Length])).ToList()
            : series.Select(s => (s.Name, s.Color)).ToList();
        double legendW = chart.ShowLegend && legendItems.Count > 0 ? 96 : 0;
        double leftGutter = isPie ? pad : (isBar ? 64 : (chart.ShowGrid ? 44 : 10));
        double bottomGutter = isPie ? pad : 16;

        double plotX = left + pad + leftGutter;
        double plotY = top + pad + titleH;
        double plotW = Math.Max(10, b.Width - 2 * pad - leftGutter - legendW);
        double plotH = Math.Max(10, b.Height - 2 * pad - titleH - bottomGutter);
        double plotR = plotX + plotW;
        double plotB = plotY + plotH;

        if (!string.IsNullOrWhiteSpace(chart.Title))
        {
            yield return new TextPrimitive
            {
                X = left + pad, Y = top + pad, Width = Math.Max(0, b.Width - 2 * pad), Height = titleH,
                Text = chart.Title!, FontFamily = style.FontFamily, FontSizePt = 10, Bold = true,
                ColorHex = style.Color, HAlign = HorizontalAnchor.Center, VAlign = VerticalAnchor.Middle,
            };
        }

        if (isPie)
        {
            foreach (var p in EmitPie(series[0], categories, plotX, plotY, plotW, plotH))
            {
                yield return p;
            }
        }
        else
        {
            foreach (var p in EmitAxes(series, categories, kind, isBar, plotX, plotY, plotW, plotH, plotR, plotB, chart.ShowGrid, style))
            {
                yield return p;
            }
        }

        if (legendW > 0)
        {
            double ly = plotY;
            foreach (var (name, color) in legendItems)
            {
                if (ly + 12 > plotB + bottomGutter)
                {
                    break;
                }

                yield return new RectanglePrimitive
                {
                    X = plotR + 10, Y = ly + 1, Width = 9, Height = 9,
                    FillColorHex = color, BorderThicknessPx = 0,
                };
                yield return new TextPrimitive
                {
                    X = plotR + 23, Y = ly - 2, Width = legendW - 20, Height = 14,
                    Text = name, FontFamily = style.FontFamily, FontSizePt = 7,
                    ColorHex = LabelColor, VAlign = VerticalAnchor.Middle,
                };
                ly += 15;
            }
        }
    }

    private static IEnumerable<RenderPrimitive> EmitAxes(
        List<SeriesData> series,
        List<string> categories,
        string kind,
        bool isBar,
        double plotX,
        double plotY,
        double plotW,
        double plotH,
        double plotR,
        double plotB,
        bool showGrid,
        EffectiveStyle style)
    {
        var all = series.SelectMany(s => s.Values).DefaultIfEmpty(0).ToList();
        double dataMax = all.Count == 0 ? 1 : all.Max();
        double dataMin = all.Count == 0 ? 0 : all.Min();
        double max = NiceCeil(Math.Max(dataMax, 0));
        double min = Math.Min(dataMin, 0);
        if (Math.Abs(max - min) < 1e-9)
        {
            max = min + 1;
        }

        double n = Math.Max(1, categories.Count);
        const int ticks = 4;

        // gridlines + tick labels
        if (showGrid)
        {
            for (var t = 0; t <= ticks; t++)
            {
                double v = min + (max - min) * t / ticks;
                if (isBar)
                {
                    double gx = plotX + plotW * t / ticks;
                    yield return Line(gx, plotY, gx, plotB, GridColor, 1);
                    yield return new TextPrimitive
                    {
                        X = gx - 24, Y = plotB + 2, Width = 48, Height = 12, Text = TickLabel(v),
                        FontFamily = style.FontFamily, FontSizePt = 7, ColorHex = LabelColor,
                        HAlign = HorizontalAnchor.Center,
                    };
                }
                else
                {
                    double gy = plotB - plotH * t / ticks;
                    yield return Line(plotX, gy, plotR, gy, GridColor, 1);
                    yield return new TextPrimitive
                    {
                        X = plotX - 42, Y = gy - 6, Width = 38, Height = 12, Text = TickLabel(v),
                        FontFamily = style.FontFamily, FontSizePt = 7, ColorHex = LabelColor,
                        HAlign = HorizontalAnchor.Right, VAlign = VerticalAnchor.Middle,
                    };
                }
            }
        }

        // axis lines
        yield return Line(plotX, plotY, plotX, plotB, AxisColor, 1);
        yield return Line(plotX, plotB, plotR, plotB, AxisColor, 1);

        double ValueToY(double v) => plotB - (v - min) / (max - min) * plotH;
        double ValueToX(double v) => plotX + (v - min) / (max - min) * plotW;

        if (kind is "line" or "area")
        {
            foreach (var s in series)
            {
                var pts = new List<PointPx>();
                for (var i = 0; i < categories.Count; i++)
                {
                    double px = plotX + (i + 0.5) * plotW / n;
                    double py = ValueToY(i < s.Values.Length ? s.Values[i] : 0);
                    pts.Add(new PointPx(px, py));
                }

                if (pts.Count == 0)
                {
                    continue;
                }

                if (kind == "area")
                {
                    var poly = new List<PointPx> { new(pts[0].X, ValueToY(Math.Max(0, min))) };
                    poly.AddRange(pts);
                    poly.Add(new PointPx(pts[^1].X, ValueToY(Math.Max(0, min))));
                    yield return new PolygonPrimitive { Points = poly, FillColorHex = Tint(s.Color) };
                }

                for (var i = 1; i < pts.Count; i++)
                {
                    yield return new LinePrimitive
                    {
                        X = pts[i - 1].X, Y = pts[i - 1].Y, X2 = pts[i].X, Y2 = pts[i].Y,
                        ThicknessPx = 1.75, ColorHex = s.Color,
                    };
                }

                foreach (var p in pts)
                {
                    yield return new RectanglePrimitive
                    {
                        X = p.X - 2, Y = p.Y - 2, Width = 4, Height = 4,
                        FillColorHex = s.Color, BorderThicknessPx = 0,
                    };
                }
            }
        }
        else if (isBar)
        {
            double groupH = plotH / n;
            double barH = groupH * 0.8 / Math.Max(1, series.Count);
            double zeroX = ValueToX(0);
            for (var i = 0; i < categories.Count; i++)
            {
                for (var j = 0; j < series.Count; j++)
                {
                    double v = i < series[j].Values.Length ? series[j].Values[i] : 0;
                    double vx = ValueToX(v);
                    double by = plotY + i * groupH + groupH * 0.1 + j * barH;
                    yield return new RectanglePrimitive
                    {
                        X = Math.Min(zeroX, vx), Y = by, Width = Math.Abs(vx - zeroX), Height = barH * 0.9,
                        FillColorHex = series[j].Color, BorderThicknessPx = 0,
                    };
                }

                yield return new TextPrimitive
                {
                    X = plotX - 62, Y = plotY + i * groupH, Width = 58, Height = groupH,
                    Text = i < categories.Count ? categories[i] : string.Empty,
                    FontFamily = style.FontFamily, FontSizePt = 7, ColorHex = LabelColor,
                    HAlign = HorizontalAnchor.Right, VAlign = VerticalAnchor.Middle,
                };
            }
        }
        else // column
        {
            double groupW = plotW / n;
            double barW = groupW * 0.8 / Math.Max(1, series.Count);
            double zeroY = ValueToY(0);
            for (var i = 0; i < categories.Count; i++)
            {
                for (var j = 0; j < series.Count; j++)
                {
                    double v = i < series[j].Values.Length ? series[j].Values[i] : 0;
                    double vy = ValueToY(v);
                    double bx = plotX + i * groupW + groupW * 0.1 + j * barW;
                    yield return new RectanglePrimitive
                    {
                        X = bx, Y = Math.Min(zeroY, vy), Width = barW * 0.9, Height = Math.Abs(vy - zeroY),
                        FillColorHex = series[j].Color, BorderThicknessPx = 0,
                    };
                }

                yield return new TextPrimitive
                {
                    X = plotX + i * groupW, Y = plotB + 2, Width = groupW, Height = 12,
                    Text = i < categories.Count ? categories[i] : string.Empty,
                    FontFamily = style.FontFamily, FontSizePt = 7, ColorHex = LabelColor,
                    HAlign = HorizontalAnchor.Center,
                };
            }
        }
    }

    private static IEnumerable<RenderPrimitive> EmitPie(
        SeriesData series,
        List<string> categories,
        double plotX,
        double plotY,
        double plotW,
        double plotH)
    {
        double total = series.Values.Where(v => v > 0).Sum();
        if (total <= 0)
        {
            yield break;
        }

        double cx = plotX + plotW / 2;
        double cy = plotY + plotH / 2;
        double r = Math.Min(plotW, plotH) / 2 * 0.92;
        double angle = -90;

        for (var i = 0; i < series.Values.Length; i++)
        {
            double v = series.Values[i];
            if (v <= 0)
            {
                continue;
            }

            double sweep = v / total * 360;
            yield return new WedgePrimitive
            {
                X = cx, Y = cy, Radius = r, StartAngleDeg = angle, SweepAngleDeg = sweep,
                FillColorHex = Palette[i % Palette.Length], StrokeColorHex = "#ffffff", StrokeWidthPx = 1,
            };
            angle += sweep;
        }
    }

    private static double Numeric(string? expr, Row row, BindingContext context)
    {
        var field = FieldOf(expr);
        if (field is not null && row.TryGetValue(field, out var raw) && raw is not null)
        {
            switch (raw)
            {
                case byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal:
                    return Convert.ToDouble(raw, CultureInfo.InvariantCulture);
                case bool b:
                    return b ? 1 : 0;
            }
        }

        var text = BindingResolver.ResolveValue(expr, null, context.WithRow(row));
        return double.TryParse(text, NumberStyles.Any, context.Culture, out var d)
            || double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out d)
            ? d
            : 0;
    }

    private static string? FieldOf(string? expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return null;
        }

        var e = expression.Trim().Trim('{', '}').Trim();
        var dot = e.LastIndexOf('.');
        if (dot <= 0 || dot == e.Length - 1)
        {
            return null;
        }

        var field = e[(dot + 1)..];
        return field.All(c => char.IsLetterOrDigit(c) || c == '_') ? field : null;
    }

    private static LinePrimitive Line(double x, double y, double x2, double y2, string color, double w) =>
        new() { X = x, Y = y, X2 = x2, Y2 = y2, ColorHex = color, ThicknessPx = w };

    private static string TickLabel(double v)
    {
        var abs = Math.Abs(v);
        return abs >= 1_000_000 ? (v / 1_000_000).ToString("0.#", CultureInfo.InvariantCulture) + "M"
            : abs >= 1_000 ? (v / 1_000).ToString("0.#", CultureInfo.InvariantCulture) + "k"
            : v.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static double NiceCeil(double v)
    {
        if (v <= 0)
        {
            return 1;
        }

        double mag = Math.Pow(10, Math.Floor(Math.Log10(v)));
        double norm = v / mag;
        double step = norm <= 1 ? 1 : norm <= 2 ? 2 : norm <= 5 ? 5 : 10;
        return step * mag;
    }

    /// <summary>A washed-out version of a hex colour for area fills.</summary>
    private static string Tint(string hex)
    {
        if (hex.Length != 7 || hex[0] != '#')
        {
            return hex;
        }

        int Mix(int c) => (int)(c + (255 - c) * 0.65);
        var r = Mix(Convert.ToInt32(hex.Substring(1, 2), 16));
        var g = Mix(Convert.ToInt32(hex.Substring(3, 2), 16));
        var bl = Mix(Convert.ToInt32(hex.Substring(5, 2), 16));
        return $"#{r:x2}{g:x2}{bl:x2}";
    }

    private sealed record SeriesData(string Name, string Color, string? Format, double[] Values);
}
