namespace JetReportDesigner.Core.Model;

/// <summary>
/// A named source of rows for the report. Exactly one of <see cref="Json"/>,
/// <see cref="Rest"/> or <see cref="Sql"/> is populated, selected by <see cref="Kind"/>
/// (<see cref="DataSourceKind.None"/> leaves all null).
/// </summary>
public sealed class DataSourceDefinition
{
    public string Name { get; set; } = string.Empty;

    public DataSourceKind Kind { get; set; } = DataSourceKind.None;

    public JsonSourceConfig? Json { get; set; }

    public RestSourceConfig? Rest { get; set; }

    public SqlSourceConfig? Sql { get; set; }

    /// <summary>
    /// The field list (discovered from a sample or declared by hand) that the
    /// designer shows in its data tree. Not authoritative at render time.
    /// </summary>
    public List<DataField> Fields { get; set; } = [];
}

public sealed class JsonSourceConfig
{
    /// <summary>The JSON document, expected to be an array of objects (or an object with an array via <see cref="ResultPath"/>).</summary>
    public string InlineData { get; set; } = "[]";

    /// <summary>JSONPath to the row array. Defaults to the document root.</summary>
    public string ResultPath { get; set; } = "$";
}

public sealed class RestSourceConfig
{
    public string Url { get; set; } = string.Empty;

    public string Method { get; set; } = "GET";

    public Dictionary<string, string> Headers { get; set; } = [];

    public Dictionary<string, string> Query { get; set; } = [];

    /// <summary>JSONPath to the row array within the response body.</summary>
    public string ResultPath { get; set; } = "$";
}

public sealed class SqlSourceConfig
{
    /// <summary>Name of a <see cref="ConnectionRef"/> declared on the report.</summary>
    public string Connection { get; set; } = string.Empty;

    public string CommandText { get; set; } = string.Empty;

    public List<SqlSourceParameter> Parameters { get; set; } = [];

    public int TimeoutSeconds { get; set; } = 30;

    public int MaxRows { get; set; } = 50_000;
}

public sealed class SqlSourceParameter
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Literal value or a <c>{param:name}</c> placeholder resolved at render time.</summary>
    public string Value { get; set; } = string.Empty;
}

/// <summary>A reference from a report to a registered database connection. The connection string is never stored here.</summary>
public sealed class ConnectionRef
{
    public string Name { get; set; } = string.Empty;

    public Guid ConnectionId { get; set; }

    public SqlProvider Provider { get; set; }
}

public sealed class DataField
{
    public string Name { get; set; } = string.Empty;

    public FieldType Type { get; set; } = FieldType.String;
}
