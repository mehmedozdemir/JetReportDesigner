using JetReportDesigner.Core.Model;

namespace JetReportDesigner.DataSources;

/// <summary>All data sources of a report, resolved to rows, keyed by source name.</summary>
public sealed class ReportData(IReadOnlyDictionary<string, ResolvedDataSet> sets)
{
    public IReadOnlyDictionary<string, ResolvedDataSet> Sets { get; } = sets;

    public ResolvedDataSet Get(string name) =>
        Sets.TryGetValue(name, out var set) ? set : ResolvedDataSet.Empty;

    /// <summary>Row <paramref name="index"/> of the named source, or an empty row.</summary>
    public IReadOnlyDictionary<string, object?> Row(string sourceName, int index)
    {
        var rows = Get(sourceName).Rows;
        return index >= 0 && index < rows.Count ? rows[index] : EmptyRow;
    }

    private static readonly IReadOnlyDictionary<string, object?> EmptyRow = new Dictionary<string, object?>();
}

/// <summary>Runs every data source in a report through its matching <see cref="IDataSourceReader"/>.</summary>
public sealed class ReportDataResolver(IEnumerable<IDataSourceReader> readers)
{
    private readonly IReadOnlyDictionary<DataSourceKind, IDataSourceReader> _readers =
        readers.ToDictionary(r => r.Kind);

    public async Task<ReportData> ResolveAsync(
        ReportDefinition report,
        IReadOnlyDictionary<string, object?> parameters,
        CancellationToken cancellationToken)
    {
        var sets = new Dictionary<string, ResolvedDataSet>(StringComparer.Ordinal);

        foreach (var source in report.DataSources)
        {
            if (source.Kind == DataSourceKind.None)
            {
                sets[source.Name] = ResolvedDataSet.Empty;
                continue;
            }

            if (!_readers.TryGetValue(source.Kind, out var reader))
            {
                throw new NotSupportedException($"No data source reader is registered for '{source.Kind}'.");
            }

            sets[source.Name] = await reader.ReadAsync(source, parameters, cancellationToken);
        }

        return new ReportData(sets);
    }
}
