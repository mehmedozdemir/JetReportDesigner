namespace JetReportDesigner.Core.Model;

/// <summary>
/// A continuous colour scale — the "heatmap" of a matrix or a table column. Each numeric cell
/// value is placed between the low and high bounds and painted with the colour at that
/// position. This is deliberately not a <see cref="FormatRule"/>: a rule is a threshold test,
/// so expressing a gradient with rules would mean one rule per band of value, re-authored
/// every time the data's range moves.
/// </summary>
public sealed class ColorScale
{
    /// <summary>Fill for the lowest value, <c>#rrggbb</c>.</summary>
    public string LowColor { get; set; } = "#ffffff";

    /// <summary>Fill at the midpoint. Null blends straight from low to high.</summary>
    public string? MidColor { get; set; }

    /// <summary>Fill for the highest value, <c>#rrggbb</c>.</summary>
    public string HighColor { get; set; } = "#2563eb";

    /// <summary>Fixed lower bound. Null takes the lowest value actually present, which is what
    /// lets a scale be set up without knowing the data's range in advance.</summary>
    public double? Min { get; set; }

    /// <summary>Fixed upper bound. Null takes the highest value present.</summary>
    public double? Max { get; set; }
}
