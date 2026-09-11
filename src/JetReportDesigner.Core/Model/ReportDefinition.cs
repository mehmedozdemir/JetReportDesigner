namespace JetReportDesigner.Core.Model;

/// <summary>
/// The complete, self-contained definition of a report: page setup, parameters,
/// data sources, named styles and layout (bands or a free body). This is the
/// document that the designer edits and the renderer consumes; it is persisted
/// verbatim as JSON.
/// </summary>
public sealed class ReportDefinition
{
    /// <summary>Schema version of this document. Bumped on breaking model changes.</summary>
    public int SchemaVersion { get; set; } = 1;

    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public LayoutMode LayoutMode { get; set; } = LayoutMode.Free;

    public PageSetup Page { get; set; } = new();

    /// <summary>Internal coordinate unit. Fixed at 1/96 inch ("px"); the UI converts for display.</summary>
    public string Unit { get; set; } = "px";

    /// <summary>
    /// Culture used for number and date formatting (e.g. <c>tr-TR</c>). Null or empty
    /// means "use whatever culture the render process is running in".
    /// </summary>
    public string? Culture { get; set; }

    public List<ReportParameter> Parameters { get; set; } = [];

    /// <summary>References to registered database connections used by SQL data sources.</summary>
    public List<ConnectionRef> Connections { get; set; } = [];

    public List<DataSourceDefinition> DataSources { get; set; } = [];

    /// <summary>Named, reusable styles referenced by elements via <see cref="ReportElement.StyleRef"/>.</summary>
    public Dictionary<string, ReportStyle> Styles { get; set; } = [];

    /// <summary>Bands, used when <see cref="LayoutMode"/> is <see cref="LayoutMode.Banded"/>.</summary>
    public List<Band> Bands { get; set; } = [];

    /// <summary>The single free-layout body, used when <see cref="LayoutMode"/> is <see cref="LayoutMode.Free"/>.</summary>
    public ReportBody? Body { get; set; }
}

/// <summary>Page size, orientation and margins. All measurements are in 1/96 inch units.</summary>
public sealed class PageSetup
{
    /// <summary>A4 | A5 | Letter | Legal | Custom.</summary>
    public string Size { get; set; } = "A4";

    /// <summary>portrait | landscape.</summary>
    public string Orientation { get; set; } = "portrait";

    /// <summary>Used only when <see cref="Size"/> is "Custom".</summary>
    public double? CustomWidth { get; set; }

    /// <summary>Used only when <see cref="Size"/> is "Custom".</summary>
    public double? CustomHeight { get; set; }

    public Margins Margins { get; set; } = new();

    /// <summary>
    /// Number of side-by-side layout columns the detail band flows into (mailing
    /// labels, a catalog grid, a directory) — left to right, then down to the next
    /// row of columns. Group headers/footers, page header/footer and report
    /// header/footer always span the full page width; a mid-row-of-columns group
    /// change or page break flushes the current row before continuing. 1 = the
    /// ordinary single-column report.
    /// </summary>
    public int Columns { get; set; } = 1;

    /// <summary>Gap between columns in 1/96 inch units, when <see cref="Columns"/> is more than 1.</summary>
    public double ColumnSpacing { get; set; } = 16;

    /// <summary>A background image drawn on every page (watermark, letterhead), or null for none.</summary>
    public BackgroundImageSpec? BackgroundImage { get; set; }
}

public sealed class Margins
{
    public double Top { get; set; } = 40;
    public double Right { get; set; } = 40;
    public double Bottom { get; set; } = 40;
    public double Left { get; set; } = 40;
}
