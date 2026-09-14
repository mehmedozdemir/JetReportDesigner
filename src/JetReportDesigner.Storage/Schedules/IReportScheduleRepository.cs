namespace JetReportDesigner.Storage.Schedules;

public sealed record ReportScheduleInfo(
    Guid Id,
    Guid ReportId,
    string ReportName,
    string Format,
    string Frequency,
    int MinuteOfDayUtc,
    int? DayOfWeek,
    int? DayOfMonth,
    bool Enabled,
    bool CreateShareLink,
    string? EmailRecipients,
    DateTime CreatedAtUtc,
    DateTime NextRunAtUtc,
    DateTime? LastRunAtUtc,
    Guid? LastJobId);

public sealed record ReportScheduleFields(
    string Format,
    string Frequency,
    int MinuteOfDayUtc,
    int? DayOfWeek,
    int? DayOfMonth,
    bool Enabled,
    bool CreateShareLink,
    string? EmailRecipients);

/// <summary>A schedule due to fire, handed to the trigger — no tenant context there, so it
/// carries the tenant id explicitly, like <c>ClaimedReportJob</c>.</summary>
public sealed record DueReportSchedule(Guid Id, Guid TenantId, Guid ReportId, string Format, Guid CreatedByUserId);

/// <summary>What the job worker needs, post-completion, to distribute a schedule-triggered job's result.</summary>
public sealed record ScheduleDistributionSettings(Guid ReportId, string ReportName, Guid CreatedByUserId, bool CreateShareLink, string? EmailRecipients);

public interface IReportScheduleRepository
{
    /// <summary>Null if <paramref name="reportId"/> doesn't exist in this tenant.</summary>
    Task<ReportScheduleInfo?> CreateAsync(Guid reportId, ReportScheduleFields fields, Guid createdByUserId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ReportScheduleInfo>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Null if the schedule doesn't exist in this tenant. Recomputes NextRunAtUtc
    /// from now — editing a schedule always re-anchors it, never preserves drift.</summary>
    Task<ReportScheduleInfo?> UpdateAsync(Guid id, ReportScheduleFields fields, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);

    // --- worker-side: no signed-in tenant, so these are not tenant-scoped ---

    /// <summary>Every enabled schedule whose NextRunAtUtc has arrived — each is atomically
    /// advanced to its next occurrence (computed from "now", not the run that just fired) so
    /// the same tick never claims it twice and a long outage doesn't cause a catch-up storm.</summary>
    Task<IReadOnlyList<DueReportSchedule>> ClaimDueAsync(CancellationToken cancellationToken);

    Task RecordRunAsync(Guid scheduleId, Guid jobId, CancellationToken cancellationToken);

    Task<ScheduleDistributionSettings?> GetDistributionSettingsAsync(Guid scheduleId, CancellationToken cancellationToken);
}
