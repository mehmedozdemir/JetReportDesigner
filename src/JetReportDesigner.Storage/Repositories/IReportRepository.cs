using JetReportDesigner.Core.Model;

namespace JetReportDesigner.Storage.Repositories;

public sealed record ReportSummary(
    Guid Id,
    string Name,
    LayoutMode LayoutMode,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record ReportRecord(
    Guid Id,
    ReportDefinition Definition,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    Guid ConcurrencyToken);

public sealed record ReportVersionInfo(int Version, string Name, DateTime SavedAtUtc);

public sealed record ReportVersionRecord(int Version, string Name, DateTime SavedAtUtc, ReportDefinition Definition);

/// <summary>Thrown by <see cref="IReportRepository.UpdateAsync"/> when the supplied concurrency token is stale.</summary>
public sealed class ReportConcurrencyException(Guid id)
    : Exception($"Report {id} was modified by another writer.");

public interface IReportRepository
{
    Task<IReadOnlyList<ReportSummary>> ListAsync(CancellationToken cancellationToken);

    Task<ReportRecord?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<ReportRecord> CreateAsync(ReportDefinition definition, CancellationToken cancellationToken);

    /// <summary>Returns null when the report does not exist. Throws <see cref="ReportConcurrencyException"/> on a token mismatch.</summary>
    Task<ReportRecord?> UpdateAsync(
        Guid id,
        ReportDefinition definition,
        Guid? expectedToken,
        CancellationToken cancellationToken);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<ReportVersionInfo>> ListVersionsAsync(Guid id, CancellationToken cancellationToken);

    Task<ReportVersionRecord?> GetVersionAsync(Guid id, int version, CancellationToken cancellationToken);

    /// <summary>Makes the given version the current definition (which itself becomes a new version). Null when the report or version does not exist.</summary>
    Task<ReportRecord?> RestoreVersionAsync(Guid id, int version, CancellationToken cancellationToken);
}
