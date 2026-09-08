using JetReportDesigner.Core.Model;

namespace JetReportDesigner.DataSources;

/// <summary>Rows and inferred field schema produced from a <see cref="DataSourceDefinition"/>.</summary>
public sealed record ResolvedDataSet(
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows,
    IReadOnlyList<DataField> Fields)
{
    public static readonly ResolvedDataSet Empty = new([], []);
}

/// <summary>Reads one kind of data source. Phase 1 ships JSON; REST and SQL arrive in Phase 3.</summary>
public interface IDataSourceReader
{
    DataSourceKind Kind { get; }

    Task<ResolvedDataSet> ReadAsync(
        DataSourceDefinition definition,
        IReadOnlyDictionary<string, object?> parameters,
        CancellationToken cancellationToken);
}
