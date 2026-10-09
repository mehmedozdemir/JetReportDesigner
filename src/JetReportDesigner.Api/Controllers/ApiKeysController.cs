using JetReportDesigner.Api.Infrastructure;
using JetReportDesigner.Storage.ApiKeys;
using JetReportDesigner.Storage.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JetReportDesigner.Api.Controllers;

/// <summary>Create, switch off, re-date and delete the API keys external applications use. Designers only.</summary>
[ApiController]
[Route("api/api-keys")]
[Authorize(Policy = AuthPolicies.Designer)]
[Produces("application/json")]
public sealed class ApiKeysController(IApiKeyRepository keys, TimeProvider clock) : ControllerBase
{
    public sealed record ApiKeyRequest(string Name, string? Description, DateTime? ExpiresAtUtc);

    public sealed record SetActiveRequest(bool IsActive);

    public sealed record ApiKeyResponse(
        Guid Id,
        string Name,
        string? Description,
        string KeyPrefix,
        bool IsActive,
        DateTime? ExpiresAtUtc,
        DateTime CreatedAtUtc,
        string? CreatedByEmail,
        DateTime? LastUsedAtUtc,
        string Status)
    {
        /// <summary>"active" | "disabled" | "expired" — the state that decides whether the key works right now.</summary>
        public static ApiKeyResponse From(ApiKey k, DateTime nowUtc) => new(
            k.Id,
            k.Name,
            k.Description,
            k.KeyPrefix,
            k.IsActive,
            k.ExpiresAtUtc is { } e ? DateTime.SpecifyKind(e, DateTimeKind.Utc) : null,
            DateTime.SpecifyKind(k.CreatedAtUtc, DateTimeKind.Utc),
            k.CreatedByEmail,
            k.LastUsedAtUtc is { } u ? DateTime.SpecifyKind(u, DateTimeKind.Utc) : null,
            !k.IsActive ? "disabled" : k.ExpiresAtUtc is { } exp && exp <= nowUtc ? "expired" : "active");
    }

    /// <summary>The only response that ever carries the secret.</summary>
    public sealed record CreatedApiKeyResponse(ApiKeyResponse Key, string Secret);

    [HttpGet]
    public async Task<IReadOnlyList<ApiKeyResponse>> List(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        return (await keys.ListAsync(cancellationToken)).Select(k => ApiKeyResponse.From(k, now)).ToList();
    }

    [HttpPost]
    public async Task<ActionResult<CreatedApiKeyResponse>> Create([FromBody] ApiKeyRequest request, CancellationToken cancellationToken)
    {
        if (Validate(request) is { } problem)
        {
            return problem;
        }

        var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? User.FindFirst("email")?.Value;
        var created = await keys.CreateAsync(request.Name, request.Description, ToUtc(request.ExpiresAtUtc), email, cancellationToken);
        return Ok(new CreatedApiKeyResponse(ApiKeyResponse.From(created.Key, clock.GetUtcNow().UtcDateTime), created.PlainTextKey));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiKeyResponse>> Update(Guid id, [FromBody] ApiKeyRequest request, CancellationToken cancellationToken)
    {
        if (Validate(request) is { } problem)
        {
            return problem;
        }

        var key = await keys.UpdateAsync(id, request.Name, request.Description, ToUtc(request.ExpiresAtUtc), cancellationToken);
        return key is null ? NotFound() : Ok(ApiKeyResponse.From(key, clock.GetUtcNow().UtcDateTime));
    }

    [HttpPut("{id:guid}/active")]
    public async Task<ActionResult<ApiKeyResponse>> SetActive(Guid id, [FromBody] SetActiveRequest request, CancellationToken cancellationToken)
    {
        var key = await keys.SetActiveAsync(id, request.IsActive, cancellationToken);
        return key is null ? NotFound() : Ok(ApiKeyResponse.From(key, clock.GetUtcNow().UtcDateTime));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        await keys.DeleteAsync(id, cancellationToken) ? NoContent() : NotFound();

    private static DateTime? ToUtc(DateTime? value) =>
        value is { } v ? (v.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(v, DateTimeKind.Utc) : v.ToUniversalTime()) : null;

    private ActionResult? Validate(ApiKeyRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 200)
        {
            return ValidationProblem("The name is required and at most 200 characters.");
        }

        if (request.Description?.Length > 1000)
        {
            return ValidationProblem("The description is at most 1000 characters.");
        }

        if (ToUtc(request.ExpiresAtUtc) is { } expires && expires <= clock.GetUtcNow().UtcDateTime)
        {
            return ValidationProblem("The expiry must be in the future.");
        }

        return null;
    }
}
