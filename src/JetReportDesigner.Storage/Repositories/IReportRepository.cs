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
}
