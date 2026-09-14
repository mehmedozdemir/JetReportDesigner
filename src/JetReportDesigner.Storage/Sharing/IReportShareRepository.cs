namespace JetReportDesigner.Storage.Sharing;

public sealed record ReportShareInfo(string Token, DateTime CreatedAtUtc, string? CreatedByEmail);

/// <summary>A share resolved by its token alone — no tenant context available (anonymous caller).</summary>
public sealed record ResolvedShare(Guid TenantId, Guid ReportId);

public interface IReportShareRepository
{
    /// <summary>Null if <paramref name="reportId"/> doesn't exist in this tenant.</summary>
    Task<ReportShareInfo?> CreateAsync(Guid reportId, Guid createdByUserId, string? createdByEmail, CancellationToken cancellationToken);

    Task<IReadOnlyList<ReportShareInfo>> ListForReportAsync(Guid reportId, CancellationToken cancellationToken);

    Task<bool> RevokeAsync(Guid reportId, string token, CancellationToken cancellationToken);

    /// <summary>Anonymous-safe: resolves a token with no tenant filtering. Null if the token
    /// doesn't exist (never issued, mistyped, or already revoked).</summary>
    Task<ResolvedShare?> ResolveAsync(string token, CancellationToken cancellationToken);
}
