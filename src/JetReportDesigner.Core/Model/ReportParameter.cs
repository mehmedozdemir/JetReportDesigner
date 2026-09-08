namespace JetReportDesigner.Core.Model;

/// <summary>
/// A value supplied when the report is run (e.g. a date range). Parameters can be
/// injected into REST queries, SQL commands and expressions via <c>{param:name}</c>.
/// </summary>
public sealed class ReportParameter
{
    public string Name { get; set; } = string.Empty;

    public ParameterType Type { get; set; } = ParameterType.String;

    public string? Label { get; set; }

    /// <summary>Default value as a JSON-compatible scalar (string, number, bool) or null.</summary>
    public object? DefaultValue { get; set; }

    public bool Required { get; set; }

    /// <summary>Optional fixed list of allowed values for a picker in the run dialog.</summary>
    public List<object>? AllowedValues { get; set; }
}
