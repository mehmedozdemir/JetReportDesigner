namespace JetReportDesigner.Core.Model;

/// <summary>
/// A single visual element on a band or the free body. A flat shape with a
/// <see cref="Type"/> discriminator and nullable type-specific blocks, rather than a
/// polymorphic hierarchy, to keep JSON round-tripping and the designer simple.
/// </summary>
public sealed class ReportElement
{
    public string Id { get; set; } = string.Empty;

    public ElementType Type { get; set; }

    /// <summary>Position and size, relative to the containing band (banded) or page (free). Units: 1/96 inch.</summary>
    public Bounds Bounds { get; set; } = new();

    /// <summary>Name of a style in <see cref="ReportDefinition.Styles"/> applied before <see cref="Style"/>.</summary>
    public string? StyleRef { get; set; }

    /// <summary>Inline style overrides layered on top of <see cref="StyleRef"/>.</summary>
    public ReportStyle? Style { get; set; }

    /// <summary>Optional boolean expression; when it evaluates false the element is not rendered.</summary>
    public string? VisibleWhen { get; set; }

    /// <summary>Conditional-formatting rules evaluated per row; all matches layer on, in order.</summary>
    public List<FormatRule> FormatRules { get; set; } = [];

    // ---- type-specific ----

    /// <summary>Static text for <see cref="ElementType.Label"/>.</summary>
    public string? Text { get; set; }

    /// <summary>Binding (<c>{ds.field}</c>) or expression for <see cref="ElementType.Field"/> and <see cref="ElementType.PageInfo"/>.</summary>
    public string? Value { get; set; }

    /// <summary>.NET format string (e.g. <c>n2</c>, <c>dd.MM.yyyy</c>, <c>c</c>).</summary>
    public string? Format { get; set; }

    public AggregateFunction Aggregate { get; set; } = AggregateFunction.None;

    public AggregateScope AggregateScope { get; set; } = AggregateScope.Group;

    public ImageSpec? Image { get; set; }

    public LineSpec? Line { get; set; }

    public TableSpec? Table { get; set; }

    public ChartSpec? Chart { get; set; }
}

public sealed class Bounds
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
}

public sealed class ImageSpec
{
    /// <summary>A URL, a base64 data string, or a <c>{ds.field}</c> binding.</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>contain | cover | fill | none.</summary>
    public string Fit { get; set; } = "contain";
}

public sealed class LineSpec
{
    /// <summary>horizontal | vertical.</summary>
    public string Orientation { get; set; } = "horizontal";
}

public sealed class TableSpec
{
    public string DataSource { get; set; } = string.Empty;

    public bool ShowHeader { get; set; } = true;

    public List<TableColumn> Columns { get; set; } = [];
}

public sealed class TableColumn
{
    public string Header { get; set; } = string.Empty;

    /// <summary>Binding or expression evaluated per row.</summary>
    public string Value { get; set; } = string.Empty;

    public double Width { get; set; } = 80;

    public string? Format { get; set; }

    public TextAlign Align { get; set; } = TextAlign.Left;
}

public sealed class ChartSpec
{
    /// <summary><c>column</c> | <c>bar</c> | <c>line</c> | <c>area</c> | <c>pie</c>.</summary>
    public string Type { get; set; } = "column";

    public string DataSource { get; set; } = string.Empty;

    /// <summary>Binding or expression for the category (X) axis label, evaluated per row.</summary>
    public string Category { get; set; } = string.Empty;

    public List<ChartSeries> Series { get; set; } = [];

    public string? Title { get; set; }

    public bool ShowLegend { get; set; } = true;

    /// <summary>Draw value-axis gridlines and tick labels.</summary>
    public bool ShowGrid { get; set; } = true;
}

public sealed class ChartSeries
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Binding or expression evaluated per row; must resolve to a number.</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary><c>#rrggbb</c>, or null to take a colour from the palette by index.</summary>
    public string? Color { get; set; }

    public string? Format { get; set; }
}
