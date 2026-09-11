namespace JetReportDesigner.Storage.Tenancy;

public sealed record CreatedInvite(string Code, string Role, DateTime ExpiresAtUtc);

public sealed record PendingInvite(string Code, string Role, DateTime CreatedAtUtc, DateTime ExpiresAtUtc);

public sealed record ConsumedInvite(Guid TenantId, string Role);

public interface ITenantInviteRepository
{
    Task<CreatedInvite> CreateAsync(Guid tenantId, string role, Guid createdByUserId, TimeSpan ttl, CancellationToken cancellationToken);

    /// <summary>Invites not yet used and not yet expired.</summary>
    Task<IReadOnlyList<PendingInvite>> ListPendingAsync(Guid tenantId, CancellationToken cancellationToken);

    /// <summary>Deletes a not-yet-used invite. False if it doesn't exist, belongs to another tenant, or was already used.</summary>
    Task<bool> RevokeAsync(Guid tenantId, string code, CancellationToken cancellationToken);

    /// <summary>Redeems a valid, unexpired, unused invite (case-insensitive code) and marks it used.
    /// Null if the code is unknown, expired, or already used.</summary>
    Task<ConsumedInvite?> ConsumeAsync(string code, Guid usedByUserId, CancellationToken cancellationToken);
}
