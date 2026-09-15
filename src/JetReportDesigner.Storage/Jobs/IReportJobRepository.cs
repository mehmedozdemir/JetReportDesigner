namespace JetReportDesigner.Storage.Jobs;

public static class ReportJobStatus
{
    public const string Queued = "Queued";
    public const string Running = "Running";
    public const string Succeeded = "Succeeded";
    public const string Failed = "Failed";
    public const string Cancelled = "Cancelled";
}

/// <summary>What a cancel request found: nothing to cancel, a queued job that was cancelled
/// outright, or a job already rendering — which the caller still has to stop in process.</summary>
public enum CancelOutcome
{
    NotCancellable,
    Cancelled,
    Running,
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
public sealed record ClaimedReportJob(Guid Id, Guid TenantId, Guid ReportId, string Format, Guid? ScheduleId);

public interface IReportJobRepository
{
    /// <summary>Null if <paramref name="reportId"/> doesn't exist in this tenant.
    /// <paramref name="scheduleId"/> is set only when a <c>ReportSchedule</c> firing created
    /// this job — the worker distributes per that schedule's settings once it succeeds.</summary>
    Task<ReportJobInfo?> EnqueueAsync(Guid reportId, string format, Guid createdByUserId, CancellationToken cancellationToken, Guid? scheduleId = null);

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

    /// <summary>Deletes finished jobs (and with them their stored results) completed before
    /// <paramref name="cutoffUtc"/>. Returns how many rows went.</summary>
    Task<int> DeleteFinishedBeforeAsync(DateTime cutoffUtc, CancellationToken cancellationToken);

    /// <summary>Marks a queued job cancelled. A job that's already rendering is reported back as
    /// <see cref="CancelOutcome.Running"/> so the caller can stop the work itself; anything
    /// already finished is <see cref="CancelOutcome.NotCancellable"/>. Tenant-scoped.</summary>
    Task<CancelOutcome> RequestCancelAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Records that a running job was stopped. Worker-side, so not tenant-scoped.</summary>
    Task MarkCancelledAsync(Guid id, CancellationToken cancellationToken);
}
