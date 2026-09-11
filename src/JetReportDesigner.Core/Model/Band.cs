namespace JetReportDesigner.Core.Model;

/// <summary>
/// A horizontal section of a banded report. Header/footer bands render once (or once
/// per page/group); the detail band renders once per row of its data source.
/// </summary>
public sealed class Band
{
    public BandType Type { get; set; }

    /// <summary>Fixed band height in 1/96 inch units. V1 has no auto-grow.</summary>
    public double Height { get; set; } = 24;

    public bool Visible { get; set; } = true;

    /// <summary>Data source whose rows drive a <see cref="BandType.Detail"/> band.</summary>
    public string? DataSource { get; set; }

    /// <summary>Grouping spec for <see cref="BandType.GroupHeader"/> / <see cref="BandType.GroupFooter"/> bands.</summary>
    public GroupSpec? Group { get; set; }

    /// <summary>
    /// Nesting depth for a <see cref="BandType.GroupHeader"/> / <see cref="BandType.GroupFooter"/>
    /// band — 0 is the outermost group. A report can have more than one group header and
    /// more than one group footer, one pair per level, giving nested (multi-level) grouping.
    /// Ignored for every other band type.
    /// </summary>
    public int GroupLevel { get; set; }

    /// <summary>Repeat this header band at the top of every page it spans (group/report headers).</summary>
    public bool RepeatOnEveryPage { get; set; }

    /// <summary>Conditional-formatting rules for the band row: paint its background and cascade style to its elements.</summary>
    public List<FormatRule> FormatRules { get; set; } = [];

    /// <summary>A background image drawn across the whole band area, or null for none.</summary>
    public BackgroundImageSpec? BackgroundImage { get; set; }

    public List<ReportElement> Elements { get; set; } = [];
}

public sealed class GroupSpec
{
    public string DataSource { get; set; } = string.Empty;

    /// <summary>Binding or expression whose value change starts a new group.</summary>
    public string Expression { get; set; } = string.Empty;

    /// <summary>asc | desc.</summary>
    public string Sort { get; set; } = "asc";
}

/// <summary>The single free-layout body. Elements are positioned absolutely; content that overflows <see cref="Height"/> flows to further pages.</summary>
public sealed class ReportBody
{
    public double Height { get; set; } = 1000;

    public List<ReportElement> Elements { get; set; } = [];
}
