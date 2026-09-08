using JetReportDesigner.Api.Contracts;
using JetReportDesigner.Storage.Connections;
using Microsoft.AspNetCore.Mvc;

namespace JetReportDesigner.Api.Controllers;

[ApiController]
[Route("api/sqlqueries")]
[Produces("application/json")]
public sealed class SqlQueriesController(ISqlQueryRepository repository) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SqlQueryResponse>>> List(
        [FromQuery] Guid connectionId,
        CancellationToken cancellationToken)
    {
        if (connectionId == Guid.Empty)
        {
            return Problem("A connectionId is required.", statusCode: 400);
        }

        var items = await repository.ListAsync(connectionId, cancellationToken);
        return Ok(items.Select(SqlQueryResponse.From).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SqlQueryResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        var item = await repository.GetAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(SqlQueryResponse.From(item));
    }

    [HttpPost]
    public async Task<ActionResult<SqlQueryResponse>> Create(
        [FromBody] CreateSqlQueryRequest body,
        CancellationToken cancellationToken)
    {
        if (body.ConnectionId == Guid.Empty
            || string.IsNullOrWhiteSpace(body.Name)
            || string.IsNullOrWhiteSpace(body.CommandText))
        {
            return Problem("connectionId, name and commandText are required.", statusCode: 400);
        }

        var created = await repository.CreateAsync(body.ConnectionId, body.Name.Trim(), body.CommandText, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, SqlQueryResponse.From(created));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<SqlQueryResponse>> Update(
        Guid id,
        [FromBody] UpdateSqlQueryRequest body,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(body.Name) || string.IsNullOrWhiteSpace(body.CommandText))
        {
            return Problem("name and commandText are required.", statusCode: 400);
        }

        var updated = await repository.UpdateAsync(id, body.Name.Trim(), body.CommandText, cancellationToken);
        return updated is null ? NotFound() : Ok(SqlQueryResponse.From(updated));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        await repository.DeleteAsync(id, cancellationToken) ? NoContent() : NotFound();
}
