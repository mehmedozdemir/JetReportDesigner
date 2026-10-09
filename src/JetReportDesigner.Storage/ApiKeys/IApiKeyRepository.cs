using JetReportDesigner.Storage.Entities;

namespace JetReportDesigner.Storage.ApiKeys;

public sealed record NewApiKey(ApiKey Key, string PlainTextKey);

public interface IApiKeyRepository
{
    Task<IReadOnlyList<ApiKey>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Creates a key and returns it together with the plain-text secret — the only time the secret exists.</summary>
    Task<NewApiKey> CreateAsync(string name, string? description, DateTime? expiresAtUtc, string? createdByEmail, CancellationToken cancellationToken);

    Task<ApiKey?> UpdateAsync(Guid id, string name, string? description, DateTime? expiresAtUtc, CancellationToken cancellationToken);

    Task<ApiKey?> SetActiveAsync(Guid id, bool active, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Finds the usable key for a presented secret (active and not expired), across all tenants — the
    /// caller is not signed in, so there is no current tenant yet. Records the use. Null when there is none.
    /// </summary>
    Task<ApiKey?> AuthenticateAsync(string plainTextKey, CancellationToken cancellationToken);
}
