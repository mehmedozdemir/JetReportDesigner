using JetReportDesigner.Core.Model;

namespace JetReportDesigner.Storage.Repositories;

public sealed record ReportSummary(
    Guid Id,
    string Name,
    LayoutMode LayoutMode,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    string? CreatedByEmail = null,
    string? Code = null);

public sealed record ReportRecord(
    Guid Id,
    ReportDefinition Definition,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    Guid ConcurrencyToken,
    string? CreatedByEmail = null);

public sealed record ReportVersionInfo(
    int Version,
    string Name,
    DateTime SavedAtUtc,
    string? SavedByEmail = null,
    IReadOnlyList<string>? Changes = null,
    int? RestoredFromVersion = null);

public sealed record ReportVersionRecord(
    int Version,
    string Name,
    DateTime SavedAtUtc,
    ReportDefinition Definition,
    string? SavedByEmail = null,
    IReadOnlyList<string>? Changes = null,
    int? RestoredFromVersion = null);

/// <summary>Thrown when a report is saved with a code another report in the organization already uses.</summary>
public sealed class ReportCodeConflictException(string code)
    : Exception($"Another report already uses the code '{code}'.")
{
    public string Code { get; } = code;
}

/// <summary>Thrown by <see cref="IReportRepository.UpdateAsync"/> when the supplied concurrency token is stale.</summary>
public sealed class ReportConcurrencyException(Guid id)
    : Exception($"Report {id} was modified by another writer.");

public interface IReportRepository
{
    Task<IReadOnlyList<ReportSummary>> ListAsync(CancellationToken cancellationToken);

    Task<ReportRecord?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Looks up a report by an explicit tenant id rather than the current signed-in
    /// tenant — for the anonymous share/render path, which has no signed-in tenant to key off.</summary>
    Task<ReportRecord?> GetForTenantAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);

    Task<ReportRecord> CreateAsync(
        ReportDefinition definition,
        CancellationToken cancellationToken,
        Guid? createdByUserId = null,
        string? createdByEmail = null,
        string? origin = null);

    /// <summary>
    /// Saves a new definition and, when it actually differs from the current one, a new version
    /// (a save that changes nothing creates no version). Returns null when the report does not exist. Throws <see cref="ReportConcurrencyException"/> on a token mismatch.</summary>
    Task<ReportRecord?> UpdateAsync(
        Guid id,
        ReportDefinition definition,
        Guid? expectedToken,
        CancellationToken cancellationToken,
        string? savedByEmail = null,
        string? origin = null);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<ReportVersionInfo>> ListVersionsAsync(Guid id, CancellationToken cancellationToken);

    Task<ReportVersionRecord?> GetVersionAsync(Guid id, int version, CancellationToken cancellationToken);

    /// <summary>Makes the given version the current definition (which itself becomes a new version). Null when the report or version does not exist.</summary>
    Task<ReportRecord?> RestoreVersionAsync(Guid id, int version, CancellationToken cancellationToken, string? savedByEmail = null);
}
