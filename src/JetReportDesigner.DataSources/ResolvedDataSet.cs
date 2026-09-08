using JetReportDesigner.Core.Model;

namespace JetReportDesigner.DataSources;

/// <summary>Rows and inferred field schema produced from a <see cref="DataSourceDefinition"/>.</summary>
public sealed record ResolvedDataSet(
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows,
    IReadOnlyList<DataField> Fields)
{
    public static readonly ResolvedDataSet Empty = new([], []);
}

/// <summary>Everything a reader needs beyond the data source itself: resolved parameters and the report's connection references.</summary>
public sealed class DataSourceReadContext(
    IReadOnlyDictionary<string, object?> parameters,
    IReadOnlyList<ConnectionRef>? connections = null)
{
    public IReadOnlyDictionary<string, object?> Parameters { get; } = parameters;

    public IReadOnlyList<ConnectionRef> Connections { get; } = connections ?? [];
}

/// <summary>Reads one kind of data source. Phase 1 ships JSON; REST arrives in Phase 3A, SQL in 3C.</summary>
public interface IDataSourceReader
{
    DataSourceKind Kind { get; }

    Task<ResolvedDataSet> ReadAsync(
        DataSourceDefinition definition,
        DataSourceReadContext context,
        CancellationToken cancellationToken);
}
