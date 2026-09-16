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

    /// <summary>
    /// For a label/field/pageInfo: when the resolved text needs more lines than
    /// <see cref="Bounds"/> is tall, grow the element (and, for a detail band element,
    /// the band instance) to fit instead of clipping. Other elements in the same band
    /// keep their designed position — growing one element does not push siblings down.
    /// </summary>
    public bool CanGrow { get; set; }

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

    public SubreportSpec? Subreport { get; set; }

    public BarcodeSpec? Barcode { get; set; }

    public MatrixSpec? Matrix { get; set; }
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

    /// <summary>Paints each cell in this column by where its value falls in the column's range.
    /// Null leaves the column unpainted.</summary>
    public ColorScale? ColorScale { get; set; }

    /// <summary>Draws a per-row mini trend chart instead of this column's text. Mutually
    /// meaningful on its own — a sparkline column's <see cref="Value"/> binding supplies the
    /// series, not a single displayed value, so <see cref="Format"/> and <see cref="ColorScale"/>
    /// are ignored when this is set.</summary>
    public SparklineSpec? Sparkline { get; set; }
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

/// <summary>
/// Embeds another saved report at this element's position. Its content is scaled to
/// this element's width and clipped to its height (V1: first page only — banded auto-
/// height/pagination-in-pagination is out of scope).
/// </summary>
public sealed class SubreportSpec
{
    /// <summary>Id of the referenced <c>StoredReport</c>.</summary>
    public string ReportId { get; set; } = string.Empty;

    /// <summary>
    /// Maps the referenced report's parameter names to a binding/expression evaluated
    /// against the parent's current row/context, e.g. <c>{"customerId": "{orders.customerId}"}</c>.
    /// </summary>
    public Dictionary<string, string> Parameters { get; set; } = [];
}

/// <summary>A barcode or QR/2D symbol. <see cref="Value"/> is a binding or expression, evaluated per row like any other bound field.</summary>
public sealed class BarcodeSpec
{
    /// <summary><c>qr</c> | <c>code128</c> | <c>ean13</c> | <c>code39</c> | <c>dataMatrix</c>.</summary>
    public string Symbology { get; set; } = "qr";

    public string Value { get; set; } = string.Empty;

    public string ForeColor { get; set; } = "#000000";

    public string BackColor { get; set; } = "#ffffff";

    /// <summary>Print the human-readable value under the bars. Ignored for the 2D symbologies (qr, dataMatrix).</summary>
    public bool ShowText { get; set; } = true;
}

/// <summary>
/// A pivot/cross-tab grid: one row per distinct <see cref="RowField"/> value, one
/// column per distinct <see cref="ColumnField"/> value (found in the data at render
/// time — there is no static column list like <see cref="TableSpec"/>), each cell the
/// <see cref="Aggregate"/> of <see cref="ValueField"/> over the rows matching that
/// row/column pair.
/// </summary>
public sealed class MatrixSpec
{
    public string DataSource { get; set; } = string.Empty;

    /// <summary>Binding or expression whose per-row value becomes a row.</summary>
    public string RowField { get; set; } = string.Empty;

    /// <summary>Header of the row-key column, e.g. "Region". Falls back to <see cref="RowField"/> when blank.</summary>
    public string? RowHeader { get; set; }

    /// <summary>Binding or expression whose per-row value becomes a column.</summary>
    public string ColumnField { get; set; } = string.Empty;

    /// <summary>Binding or expression aggregated into each cell.</summary>
    public string ValueField { get; set; } = string.Empty;

    public AggregateFunction Aggregate { get; set; } = AggregateFunction.Sum;

    public string? Format { get; set; }

    /// <summary>Paints the data cells by value — the classic heatmap. The range covers the
    /// grid's own cells, so totals rows/columns stay unpainted and don't flatten the scale.</summary>
    public ColorScale? ColorScale { get; set; }

    public bool ShowRowTotals { get; set; } = true;

    public bool ShowColumnTotals { get; set; } = true;
}
