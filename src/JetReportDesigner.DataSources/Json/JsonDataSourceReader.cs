using JetReportDesigner.Core.Model;

namespace JetReportDesigner.DataSources.Json;

public sealed class JsonDataSourceReader : IDataSourceReader
{
    public DataSourceKind Kind => DataSourceKind.Json;

    public Task<ResolvedDataSet> ReadAsync(
        DataSourceDefinition definition,
        DataSourceReadContext context,
        CancellationToken cancellationToken)
    {
        var config = definition.Json;
        if (config is null)
        {
            return Task.FromResult(ResolvedDataSet.Empty);
        }

        return Task.FromResult(JsonRows.Parse(config.InlineData, config.ResultPath));
    }
}
