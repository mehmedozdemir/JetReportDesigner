using JetReportDesigner.Core.Model;
using JetReportDesigner.DataSources;
using Microsoft.AspNetCore.Mvc;

namespace JetReportDesigner.Api.Controllers;

[ApiController]
[Route("api/datasources")]
[Produces("application/json")]
public sealed class DataSourcesController(IEnumerable<IDataSourceReader> readers) : ControllerBase
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

        return _readers.TryGetValue(definition.Kind, out var reader)
            ? await reader.ReadAsync(definition, new Dictionary<string, object?>(), cancellationToken)
            : null;
    }

    public sealed record SchemaResponse(IReadOnlyList<DataField> Fields);

    public sealed record PreviewResponse(
        IReadOnlyList<DataField> Fields,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows);
}
