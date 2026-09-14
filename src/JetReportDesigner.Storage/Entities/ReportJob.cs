namespace JetReportDesigner.Storage.Entities;

/// <summary>
/// A queued/in-progress/finished background render — "export this without making me wait",
/// and the execution engine a future <c>ReportSchedule</c> will reuse to run its own jobs.
/// The rendered bytes live directly on the row (own storage, DB-blob, like every other
/// binary this app persists) rather than going through <c>IAssetRepository</c>, which is
/// tenant-scoped via the signed-in user's claims — the background worker that processes
/// jobs has no such context. <see cref="ReportId"/> is a plain denormalised column, no
/// FK/cascade, matching this codebase's established convention.
/// </summary>
public sealed class ReportJob
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid ReportId { get; set; }

    /// <summary>Set when this job was created by a <c>ReportSchedule</c> firing rather than a
    /// direct "run in background" request — the job worker distributes (share link/email)
    /// per that schedule's settings once the job succeeds.</summary>
    public Guid? ScheduleId { get; set; }

    /// <summary>Snapshot at enqueue time — still meaningful to show even if the report is
    /// later renamed or deleted.</summary>
    public string ReportName { get; set; } = string.Empty;

    /// <summary>"pdf" | "xlsx".</summary>
    public string Format { get; set; } = "pdf";

    /// <summary>"Queued" | "Running" | "Succeeded" | "Failed".</summary>
    public string Status { get; set; } = "Queued";

    public byte[]? ResultContent { get; set; }

    public string? ResultContentType { get; set; }

    public string? ResultFileName { get; set; }

    public string? ErrorMessage { get; set; }

    public Guid CreatedByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? StartedAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }
}
