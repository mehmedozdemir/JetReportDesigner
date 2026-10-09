using JetReportDesigner.Core.Model;

namespace JetReportDesigner.Rendering.Layout;

/// <summary>
/// Draws one row's <see cref="SparklineSpec"/> inside a cell rectangle. Deliberately minimal:
/// no axis, no gridlines, no labels — the entire point of a sparkline is ink-per-value, read
/// as a shape rather than measured. Both chart types scale linearly between the series' own
/// min and max onto the cell's height, so a line and a bar sparkline of the same data read the
/// same way; a zero-anchored bar baseline was considered and dropped; it would make bar and
/// line sparklines disagree about the same series for no benefit a reader of a table cell notices.
/// </summary>
public static class SparklineEmitter
{
    private const double Padding = 3;
    private const double DotRadius = 1.6;
    private const double HighlightDotRadius = 2.4;

    public static IEnumerable<RenderPrimitive> Emit(SparklineSpec spec, IReadOnlyList<double> values, double x, double y, double w, double h)
    {
        if (values.Count == 0)
        {
            yield break;
        }

        var min = values.Min();
        var max = values.Max();
        var innerX = x + Padding;
        var innerY = y + Padding;
        var innerW = Math.Max(1, w - (2 * Padding));
        var innerH = Math.Max(1, h - (2 * Padding));
        var innerB = innerY + innerH;

        // A flat series (including a single point) has no "low" or "high" to show — every
        // point sits mid-height rather than the whole thing collapsing onto the baseline.
        double ValueToY(double v) => max - min <= double.Epsilon
            ? innerY + (innerH / 2)
            : innerB - ((v - min) / (max - min) * innerH);

        var isBar = string.Equals(spec.Type, "bar", StringComparison.OrdinalIgnoreCase);
        foreach (var p in isBar
            ? EmitBars(spec, values, innerX, innerW, innerB, ValueToY)
            : EmitLine(spec, values, innerX, innerW, ValueToY))
        {
            yield return p;
        }
    }

    private static IEnumerable<RenderPrimitive> EmitBars(
        SparklineSpec spec, IReadOnlyList<double> values, double innerX, double innerW, double innerB, Func<double, double> valueToY)
    {
        var n = values.Count;
        var slot = innerW / n;
        var barW = Math.Max(1, slot * 0.65);
        for (var i = 0; i < n; i++)
        {
            var barY = valueToY(values[i]);
            var last = i == n - 1;
            yield return new RectanglePrimitive
            {
                X = innerX + (i * slot) + ((slot - barW) / 2),
                Y = barY,
                Width = barW,
                Height = Math.Max(0.5, innerB - barY),
                FillColorHex = last && spec.HighlightColor is { Length: > 0 } hc ? hc : spec.Color,
                BorderThicknessPx = 0,
            };
        }
    }

    private static IEnumerable<RenderPrimitive> EmitLine(
        SparklineSpec spec, IReadOnlyList<double> values, double innerX, double innerW, Func<double, double> valueToY)
    {
        var n = values.Count;
        var pts = new PointPx[n];
        for (var i = 0; i < n; i++)
        {
            var px = n == 1 ? innerX + (innerW / 2) : innerX + (i * innerW / (n - 1));
            pts[i] = new PointPx(px, valueToY(values[i]));
        }

        if (spec.ShowArea && n > 1)
        {
            // Closed at the series' own lowest point rather than a data value of zero: a
            // sparkline has no axis, so "zero" isn't a position a reader can see anyway.
            var floorY = pts.Max(p => p.Y);
            var poly = new List<PointPx> { new(pts[0].X, floorY) };
            poly.AddRange(pts);
            poly.Add(new PointPx(pts[^1].X, floorY));
            yield return new PolygonPrimitive { Points = poly, FillColorHex = Tint(spec.Color) };
        }

        for (var i = 1; i < n; i++)
        {
            yield return new LinePrimitive
            {
                X = pts[i - 1].X, Y = pts[i - 1].Y, X2 = pts[i].X, Y2 = pts[i].Y,
                ThicknessPx = 1.25, ColorHex = spec.Color,
            };
        }

        for (var i = 0; i < n; i++)
        {
            var last = i == n - 1;
            var highlighted = last && spec.HighlightColor is { Length: > 0 };
            var r = highlighted ? HighlightDotRadius : DotRadius;
            if (!highlighted && n > 1)
            {
                // Only the (possibly highlighted) last point earns a dot when there's a line to
                // read; every point rendered as one would out-weigh the line itself.
                continue;
            }

            yield return new RectanglePrimitive
            {
                X = pts[i].X - r, Y = pts[i].Y - r, Width = r * 2, Height = r * 2,
                FillColorHex = highlighted ? spec.HighlightColor! : spec.Color, BorderThicknessPx = 0,
            };
        }
    }

    /// <summary>Lightens a colour for the area fill under a line — the same tint ChartEmitter
    /// uses for its own area series, so a sparkline and a full chart read as one family.</summary>
    private static string Tint(string hex)
    {
        if (hex.Length != 7 || hex[0] != '#')
        {
            return hex;
        }

        int Mix(int c) => c + (int)((255 - c) * 0.75);
        var r = Mix(Convert.ToInt32(hex.Substring(1, 2), 16));
        var g = Mix(Convert.ToInt32(hex.Substring(3, 2), 16));
        var b = Mix(Convert.ToInt32(hex.Substring(5, 2), 16));
        return $"#{r:x2}{g:x2}{b:x2}";
    }
}
