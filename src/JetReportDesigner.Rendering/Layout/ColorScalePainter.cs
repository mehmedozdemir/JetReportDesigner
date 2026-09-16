using System.Globalization;
using JetReportDesigner.Core.Model;

namespace JetReportDesigner.Rendering.Layout;

/// <summary>Turns a <see cref="ColorScale"/> plus a set of values into per-cell fills.</summary>
public static class ColorScalePainter
{
    /// <summary>
    /// The cell's value as a number, or null when it isn't one. Unlike the coercions in
    /// <see cref="AggregateComputer"/> and <see cref="ChartEmitter"/>, which fold anything
    /// unparseable to zero, a scale has to tell "not a number" from "zero": a blank cell
    /// painted as zero would read as a real low value.
    /// </summary>
    public static double? Number(object? value) => value switch
    {
        null => null,
        double d => d,
        float f => f,
        long l => l,
        int i => i,
        short s => s,
        decimal m => (double)m,
        bool => null,
        string t => double.TryParse(t, NumberStyles.Any, CultureInfo.InvariantCulture, out var p) ? p : null,
        IConvertible c => TryConvert(c),
        _ => null,
    };

    private static double? TryConvert(IConvertible c)
    {
        try
        {
            return c.ToDouble(CultureInfo.InvariantCulture);
        }
        catch (Exception e) when (e is FormatException or InvalidCastException or OverflowException)
        {
            return null;
        }
    }

    /// <summary>
    /// The bounds to scale against: whatever the scale fixes, otherwise the extent of
    /// <paramref name="values"/>. Null when there is nothing numeric to scale.
    /// </summary>
    public static (double Min, double Max)? Range(ColorScale scale, IEnumerable<double> values)
    {
        double? low = scale.Min, high = scale.Max;
        if (low is null || high is null)
        {
            var seen = false;
            double min = 0, max = 0;
            foreach (var v in values)
            {
                if (!seen)
                {
                    min = max = v;
                    seen = true;
                    continue;
                }

                if (v < min)
                {
                    min = v;
                }

                if (v > max)
                {
                    max = v;
                }
            }

            if (!seen)
            {
                return null;
            }

            low ??= min;
            high ??= max;
        }

        return low > high ? (high.Value, low.Value) : (low.Value, high.Value);
    }

    /// <summary>The fill for one value. Values outside the bounds clamp to the end colours.</summary>
    public static string Fill(ColorScale scale, double value, double min, double max)
    {
        // Every value identical (or a zero-width fixed range): there is no "high" or "low" to
        // show, so the whole thing sits at the middle of the scale rather than reading as all-max.
        var t = max - min <= double.Epsilon ? 0.5 : Math.Clamp((value - min) / (max - min), 0, 1);

        if (scale.MidColor is not { Length: > 0 } mid)
        {
            return Mix(scale.LowColor, scale.HighColor, t);
        }

        return t <= 0.5 ? Mix(scale.LowColor, mid, t * 2) : Mix(mid, scale.HighColor, (t - 0.5) * 2);
    }

    /// <summary>
    /// Black or white, whichever stays readable on <paramref name="backgroundHex"/>. A scale
    /// that runs into dark colours would otherwise leave the darkest — most significant —
    /// cells as near-black text on near-black fill.
    /// </summary>
    public static string TextOn(string backgroundHex)
    {
        var (r, g, b) = Parse(backgroundHex);

        // Relative luminance (WCAG): the eye is far more sensitive to green than to blue, so a
        // plain RGB average would call a saturated blue "light" and pick unreadable black text.
        static double Channel(int c)
        {
            var s = c / 255.0;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }

        var luminance = (0.2126 * Channel(r)) + (0.7152 * Channel(g)) + (0.0722 * Channel(b));
        return luminance > 0.179 ? "#000000" : "#ffffff";
    }

    private static string Mix(string fromHex, string toHex, double t)
    {
        var (r1, g1, b1) = Parse(fromHex);
        var (r2, g2, b2) = Parse(toHex);
        var r = (int)Math.Round(r1 + ((r2 - r1) * t));
        var g = (int)Math.Round(g1 + ((g2 - g1) * t));
        var b = (int)Math.Round(b1 + ((b2 - b1) * t));
        return $"#{r:x2}{g:x2}{b:x2}";
    }

    /// <summary>Anything unparseable reads as white, matching the model's default low colour.</summary>
    private static (int R, int G, int B) Parse(string? hex)
    {
        var h = (hex ?? string.Empty).TrimStart('#');
        if (h.Length == 3)
        {
            h = string.Concat(h[0], h[0], h[1], h[1], h[2], h[2]);
        }

        if (h.Length != 6 || !int.TryParse(h, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var packed))
        {
            return (255, 255, 255);
        }

        return ((packed >> 16) & 0xFF, (packed >> 8) & 0xFF, packed & 0xFF);
    }
}
