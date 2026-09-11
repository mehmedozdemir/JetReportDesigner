using JetReportDesigner.Api.Contracts;
using JetReportDesigner.Api.Infrastructure;
using JetReportDesigner.Core.Model;
using JetReportDesigner.Storage.Connections;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JetReportDesigner.Api.Controllers;

[ApiController]
[Route("api/connections")]
[Produces("application/json")]
public sealed class ConnectionsController(IConnectionRepository repository) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ConnectionResponse>>> List(CancellationToken cancellationToken)
    {
        var items = await repository.ListAsync(cancellationToken);
        return Ok(items.Select(ConnectionResponse.From).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ConnectionResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        var item = await repository.GetAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(ConnectionResponse.From(item));
    }

    [HttpPost]
    [Authorize(Policy = AuthPolicies.Designer)]
    public async Task<ActionResult<ConnectionResponse>> Create(
        [FromBody] CreateConnectionRequest body,
        CancellationToken cancellationToken)
    {
        if (!TryParseProvider(body.Provider, out var provider))
        {
            return Problem($"Unknown provider '{body.Provider}'.", statusCode: 400);
        }

        if (string.IsNullOrWhiteSpace(body.Name) || string.IsNullOrWhiteSpace(body.ConnectionString))
        {
            return Problem("Name and connectionString are required.", statusCode: 400);
        }

        var created = await repository.CreateAsync(body.Name.Trim(), provider, body.ConnectionString, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, ConnectionResponse.From(created));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthPolicies.Designer)]
    public async Task<ActionResult<ConnectionResponse>> Update(
        Guid id,
        [FromBody] UpdateConnectionRequest body,
        CancellationToken cancellationToken)
    {
        if (!TryParseProvider(body.Provider, out var provider))
        {
            return Problem($"Unknown provider '{body.Provider}'.", statusCode: 400);
        }

        var updated = await repository.UpdateAsync(id, body.Name.Trim(), provider, body.ConnectionString, cancellationToken);
        return updated is null ? NotFound() : Ok(ConnectionResponse.From(updated));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AuthPolicies.Designer)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        await repository.DeleteAsync(id, cancellationToken) ? NoContent() : NotFound();

    private static bool TryParseProvider(string value, out SqlProvider provider)
    {
        provider = value?.ToLowerInvariant() switch
        {
            "sqlserver" => SqlProvider.SqlServer,
            "postgresql" => SqlProvider.PostgreSql,
            "oracle" => SqlProvider.Oracle,
            _ => (SqlProvider)(-1),
        };
        return Enum.IsDefined(provider);
    }
}
