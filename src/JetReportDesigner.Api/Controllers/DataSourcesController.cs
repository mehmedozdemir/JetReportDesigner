using JetReportDesigner.Core.Model;
using JetReportDesigner.DataSources;
using JetReportDesigner.Storage.Connections;
using Microsoft.AspNetCore.Mvc;

namespace JetReportDesigner.Api.Controllers;

[ApiController]
[Route("api/datasources")]
[Produces("application/json")]
public sealed class DataSourcesController(
    IEnumerable<IDataSourceReader> readers,
    IConnectionRepository connections) : ControllerBase
{
    private readonly IReadOnlyDictionary<DataSourceKind, IDataSourceReader> _readers =
        readers.ToDictionary(r => r.Kind);

    /// <summary>Infer the field list for a data source without persisting it. Phase 1: JSON only.</summary>
    [HttpPost("schema")]
    public async Task<ActionResult<SchemaResponse>> Schema(
        [FromBody] DataSourceDefinition definition,
        CancellationToken cancellationToken)
    {
        var set = await Read(definition, cancellationToken);
        return set is null
            ? Problem($"No reader for data source kind '{definition.Kind}'.", statusCode: 501)
            : Ok(new SchemaResponse(set.Fields));
    }

    /// <summary>Field list plus the first <paramref name="take"/> rows, for the designer's data preview.</summary>
    [HttpPost("preview")]
    public async Task<ActionResult<PreviewResponse>> Preview(
        [FromBody] DataSourceDefinition definition,
        [FromQuery] int take = 20,
        CancellationToken cancellationToken = default)
    {
        var set = await Read(definition, cancellationToken);
        if (set is null)
        {
            return Problem($"No reader for data source kind '{definition.Kind}'.", statusCode: 501);
        }

        var rows = set.Rows.Take(Math.Clamp(take, 1, 200)).ToList();
        return Ok(new PreviewResponse(set.Fields, rows));
    }

    private async Task<ResolvedDataSet?> Read(DataSourceDefinition definition, CancellationToken cancellationToken)
    {
        if (definition.Kind == DataSourceKind.None)
        {
            return ResolvedDataSet.Empty;
        }

        if (!_readers.TryGetValue(definition.Kind, out var reader))
        {
            return null;
        }

        var context = new DataSourceReadContext(
            new Dictionary<string, object?>(),
            await ResolveConnectionsAsync(definition, cancellationToken));

        return await reader.ReadAsync(definition, context, cancellationToken);
    }

    /// <summary>
    /// The designer previews a data source before the report (and its connection refs)
    /// are saved, so resolve the SQL connection by name straight from the registry.
    /// </summary>
    private async Task<IReadOnlyList<ConnectionRef>> ResolveConnectionsAsync(
        DataSourceDefinition definition,
        CancellationToken cancellationToken)
    {
        var name = definition.Sql?.Connection;
        if (definition.Kind != DataSourceKind.Sql || string.IsNullOrWhiteSpace(name))
        {
            return [];
        }

        var registered = await connections.ListAsync(cancellationToken);
        var match = registered.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.Ordinal));
        return match is null
            ? []
            : [new ConnectionRef { Name = match.Name, ConnectionId = match.Id, Provider = match.Provider }];
    }

    public sealed record SchemaResponse(IReadOnlyList<DataField> Fields);

    public sealed record PreviewResponse(
        IReadOnlyList<DataField> Fields,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows);
}
