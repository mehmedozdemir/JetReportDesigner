namespace JetReportDesigner.Storage.Entities;

/// <summary>
/// A recurring background render — reuses <see cref="ReportJob"/>/the job worker as its
/// execution engine (a schedule firing just enqueues a job); this row only holds the
/// recurrence rule and what to do once that job succeeds.
/// </summary>
public sealed class ReportSchedule
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid ReportId { get; set; }

    /// <summary>Snapshot at creation time, for display even if the report is later renamed.</summary>
    public string ReportName { get; set; } = string.Empty;

    /// <summary>"pdf" | "xlsx".</summary>
    public string Format { get; set; } = "pdf";

    /// <summary>"Daily" | "Weekly" | "Monthly".</summary>
    public string Frequency { get; set; } = "Daily";

    /// <summary>0–1439, UTC.</summary>
    public int MinuteOfDayUtc { get; set; }

    /// <summary>0 (Sunday) – 6 (Saturday). Only meaningful when <see cref="Frequency"/> is "Weekly".</summary>
    public int? DayOfWeek { get; set; }

    /// <summary>1–31, clamped to shorter months. Only meaningful when <see cref="Frequency"/> is "Monthly".</summary>
    public int? DayOfMonth { get; set; }

    public bool Enabled { get; set; } = true;

    /// <summary>Create a fresh public share link each time this schedule's job succeeds.</summary>
    public bool CreateShareLink { get; set; }

    /// <summary>Comma-separated addresses to email the result to on success — requires the
    /// tenant's mail account to be configured; silently skipped otherwise. Null/empty = don't email.</summary>
    public string? EmailRecipients { get; set; }

    public Guid CreatedByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime NextRunAtUtc { get; set; }

    public DateTime? LastRunAtUtc { get; set; }

    /// <summary>The most recent job this schedule created — a plain denormalised pointer, not
    /// an FK, matching this codebase's convention for cross-entity references.</summary>
    public Guid? LastJobId { get; set; }
}
