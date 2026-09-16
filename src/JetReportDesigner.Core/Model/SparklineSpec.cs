namespace JetReportDesigner.Core.Model;

/// <summary>
/// A mini trend chart drawn inside a table cell in place of that column's text — the
/// "sparkline" of a per-row series. The column's own <see cref="TableColumn.Value"/>
/// binding supplies the series: a JSON array (<c>[4,7,2,9]</c>, the natural shape of a
/// nested array field from a JSON data source) or a comma-separated list of numbers.
/// </summary>
public sealed class SparklineSpec
{
    /// <summary><c>line</c> | <c>bar</c>.</summary>
    public string Type { get; set; } = "line";

    public string Color { get; set; } = "#2563eb";

    /// <summary>Line type only: fill the area under the line with a tint of <see cref="Color"/>.</summary>
    public bool ShowArea { get; set; } = true;

    /// <summary>Marks the series' last point in a different colour — "where things stand right
    /// now" — a line dot or the last bar. Null draws every point the same.</summary>
    public string? HighlightColor { get; set; }
}
