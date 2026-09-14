using JetReportDesigner.Storage.Entities;
using JetReportDesigner.Storage.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace JetReportDesigner.Storage.Schedules;

internal sealed class ReportScheduleRepository(JetReportDbContext db, TimeProvider clock, ICurrentTenant tenant) : IReportScheduleRepository
{
    public async Task<ReportScheduleInfo?> CreateAsync(Guid reportId, ReportScheduleFields fields, Guid createdByUserId, CancellationToken cancellationToken)
    {
        var report = await db.Reports
            .AsNoTracking()
            .Where(r => r.Id == reportId && r.TenantId == tenant.TenantId)
            .Select(r => new { r.Name })
            .FirstOrDefaultAsync(cancellationToken);
        if (report is null)
        {
            return null;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var schedule = new ReportSchedule
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.TenantId,
            ReportId = reportId,
            ReportName = report.Name,
            Format = fields.Format,
            Frequency = fields.Frequency,
            MinuteOfDayUtc = fields.MinuteOfDayUtc,
            DayOfWeek = fields.DayOfWeek,
            DayOfMonth = fields.DayOfMonth,
            Enabled = fields.Enabled,
            CreateShareLink = fields.CreateShareLink,
            EmailRecipients = fields.EmailRecipients,
            CreatedByUserId = createdByUserId,
            CreatedAtUtc = now,
            NextRunAtUtc = ScheduleRecurrence.NextRun(now, fields.Frequency, fields.MinuteOfDayUtc, fields.DayOfWeek, fields.DayOfMonth),
        };
        db.ReportSchedules.Add(schedule);
        await db.SaveChangesAsync(cancellationToken);
        return ToInfo(schedule);
    }

    public async Task<IReadOnlyList<ReportScheduleInfo>> ListAsync(CancellationToken cancellationToken)
    {
        var rows = await db.ReportSchedules
            .AsNoTracking()
            .Where(s => s.TenantId == tenant.TenantId)
            .OrderBy(s => s.NextRunAtUtc)
            .ToListAsync(cancellationToken);

        return rows.Select(ToInfo).ToList();
    }

    public async Task<ReportScheduleInfo?> UpdateAsync(Guid id, ReportScheduleFields fields, CancellationToken cancellationToken)
    {
        var schedule = await db.ReportSchedules.FirstOrDefaultAsync(s => s.Id == id && s.TenantId == tenant.TenantId, cancellationToken);
        if (schedule is null)
        {
            return null;
        }

        var now = clock.GetUtcNow().UtcDateTime;
        schedule.Format = fields.Format;
        schedule.Frequency = fields.Frequency;
        schedule.MinuteOfDayUtc = fields.MinuteOfDayUtc;
        schedule.DayOfWeek = fields.DayOfWeek;
        schedule.DayOfMonth = fields.DayOfMonth;
        schedule.Enabled = fields.Enabled;
        schedule.CreateShareLink = fields.CreateShareLink;
        schedule.EmailRecipients = fields.EmailRecipients;
        schedule.NextRunAtUtc = ScheduleRecurrence.NextRun(now, fields.Frequency, fields.MinuteOfDayUtc, fields.DayOfWeek, fields.DayOfMonth);

        await db.SaveChangesAsync(cancellationToken);
        return ToInfo(schedule);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await db.ReportSchedules.Where(s => s.Id == id && s.TenantId == tenant.TenantId).ExecuteDeleteAsync(cancellationToken);
        return deleted > 0;
    }

    public async Task<IReadOnlyList<DueReportSchedule>> ClaimDueAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var due = await db.ReportSchedules
            .Where(s => s.Enabled && s.NextRunAtUtc <= now)
            .ToListAsync(cancellationToken);
        if (due.Count == 0)
        {
            return [];
        }

        var claimed = new List<DueReportSchedule>(due.Count);
        foreach (var schedule in due)
        {
            claimed.Add(new DueReportSchedule(schedule.Id, schedule.TenantId, schedule.ReportId, schedule.Format, schedule.CreatedByUserId));
            schedule.NextRunAtUtc = ScheduleRecurrence.NextRun(now, schedule.Frequency, schedule.MinuteOfDayUtc, schedule.DayOfWeek, schedule.DayOfMonth);
        }

        await db.SaveChangesAsync(cancellationToken);
        return claimed;
    }

    public async Task RecordRunAsync(Guid scheduleId, Guid jobId, CancellationToken cancellationToken)
    {
        var schedule = await db.ReportSchedules.FirstOrDefaultAsync(s => s.Id == scheduleId, cancellationToken);
        if (schedule is null)
        {
            return;
        }

        schedule.LastRunAtUtc = clock.GetUtcNow().UtcDateTime;
        schedule.LastJobId = jobId;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<ScheduleDistributionSettings?> GetDistributionSettingsAsync(Guid scheduleId, CancellationToken cancellationToken)
    {
        var row = await db.ReportSchedules.AsNoTracking().FirstOrDefaultAsync(s => s.Id == scheduleId, cancellationToken);
        return row is null
            ? null
            : new ScheduleDistributionSettings(row.ReportId, row.ReportName, row.CreatedByUserId, row.CreateShareLink, row.EmailRecipients);
    }

    public async Task RecordDistributionResultAsync(Guid scheduleId, string? error, CancellationToken cancellationToken)
    {
        var schedule = await db.ReportSchedules.FirstOrDefaultAsync(s => s.Id == scheduleId, cancellationToken);
        if (schedule is null)
        {
            return;
        }

        schedule.LastDistributionAtUtc = clock.GetUtcNow().UtcDateTime;
        schedule.LastDistributionError = error;
        await db.SaveChangesAsync(cancellationToken);
    }

    private static ReportScheduleInfo ToInfo(ReportSchedule s) => new(
        s.Id, s.ReportId, s.ReportName, s.Format, s.Frequency, s.MinuteOfDayUtc, s.DayOfWeek, s.DayOfMonth,
        s.Enabled, s.CreateShareLink, s.EmailRecipients, s.CreatedAtUtc, s.NextRunAtUtc, s.LastRunAtUtc, s.LastJobId,
        s.LastDistributionAtUtc, s.LastDistributionError);
}
