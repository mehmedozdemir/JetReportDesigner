namespace JetReportDesigner.Core.Model;

// Serialized as camelCase strings by ReportJson / the API's shared JSON options
// (JsonStringEnumConverter with a camelCase policy). No per-type attributes so the
// single options object is the only place the wire format is defined.

/// <summary>How a report arranges its content.</summary>
public enum LayoutMode
{
    /// <summary>Banded report: header/detail/footer bands, detail repeats per row.</summary>
    Banded,

    /// <summary>Free layout: a fixed canvas with absolutely positioned elements, no repetition.</summary>
    Free,
}

public enum ParameterType
{
    String,
    Number,
    Boolean,
    Date,
    DateTime,
}

public enum DataSourceKind
{
    /// <summary>No external data; the report runs on parameter values only.</summary>
    None,

    /// <summary>Inline JSON pasted or uploaded by the designer.</summary>
    Json,

    /// <summary>An HTTP GET endpoint returning JSON.</summary>
    Rest,

    /// <summary>A parameterised SELECT against a registered database connection.</summary>
    Sql,
}

public enum FieldType
{
    String,
    Number,
    Boolean,
    Date,
    DateTime,
}

public enum BandType
{
    ReportHeader,
    PageHeader,
    GroupHeader,
    Detail,
    GroupFooter,
    PageFooter,
    ReportFooter,
}

public enum ElementType
{
    Label,
    Field,
    Table,
    Image,
    Line,
    Rectangle,
    PageInfo,
    Chart,
    Subreport,
    Barcode,
    Matrix,
}

public enum TextAlign
{
    Left,
    Center,
    Right,
    Justify,
}

public enum VerticalAlign
{
    Top,
    Middle,
    Bottom,
}

public enum AggregateFunction
{
    None,
    Sum,
    Count,
    Average,
    Min,
    Max,
    First,
    Last,
}

/// <summary>Scope an aggregate is computed over.</summary>
public enum AggregateScope
{
    Group,
    Report,
    Page,
}

/// <summary>Relational provider backing a stored database connection.</summary>
public enum SqlProvider
{
    SqlServer,
    PostgreSql,
    Oracle,
}
