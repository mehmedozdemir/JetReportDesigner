namespace JetReportDesigner.Storage.Jobs;

public static class ReportJobStatus
{
    public const string Queued = "Queued";
    public const string Running = "Running";
    public const string Succeeded = "Succeeded";
    public const string Failed = "Failed";
}

public sealed record ReportJobInfo(
    Guid Id,
    Guid ReportId,
    string ReportName,
    string Format,
    string Status,
    string? ErrorMessage,
    DateTime CreatedAtUtc,
    DateTime? StartedAtUtc,
    DateTime? CompletedAtUtc);

public sealed record ReportJobResult(byte[] Content, string ContentType, string FileName);

/// <summary>A job handed to the background worker — no tenant context available there,
/// so it carries the tenant id explicitly.</summary>
public sealed record ClaimedReportJob(Guid Id, Guid TenantId, Guid ReportId, string Format);

public interface IReportJobRepository
{
    /// <summary>Null if <paramref name="reportId"/> doesn't exist in this tenant.</summary>
    Task<ReportJobInfo?> EnqueueAsync(Guid reportId, string format, Guid createdByUserId, CancellationToken cancellationToken);

    /// <summary>Most recent jobs for the tenant (all reports), newest first.</summary>
    Task<IReadOnlyList<ReportJobInfo>> ListAsync(CancellationToken cancellationToken);

    Task<ReportJobInfo?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Null if the job doesn't exist or hasn't succeeded yet.</summary>
    Task<ReportJobResult?> GetResultAsync(Guid id, CancellationToken cancellationToken);

    // --- worker-side: no signed-in tenant, so these are not tenant-scoped ---

    /// <summary>Picks the oldest queued job (across every tenant) and marks it running.
    /// Null when the queue is empty.</summary>
    Task<ClaimedReportJob?> ClaimNextAsync(CancellationToken cancellationToken);

    Task CompleteAsync(Guid id, byte[] content, string contentType, string fileName, CancellationToken cancellationToken);

    Task FailAsync(Guid id, string errorMessage, CancellationToken cancellationToken);

    /// <summary>Resets any job stuck "Running" (the process crashed mid-job) back to
    /// "Queued" — call once at worker startup.</summary>
    Task RecoverStuckAsync(CancellationToken cancellationToken);
}
