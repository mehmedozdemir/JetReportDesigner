using JetReportDesigner.Storage.Entities;
using JetReportDesigner.Storage.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace JetReportDesigner.Storage.Jobs;

internal sealed class ReportJobRepository(JetReportDbContext db, TimeProvider clock, ICurrentTenant tenant) : IReportJobRepository
{
    /// <summary>Also surfaced in the Jobs page's footnote, so the two can't drift apart.</summary>
    public const int ListLimit = 50;

    public async Task<ReportJobInfo?> EnqueueAsync(Guid reportId, string format, Guid createdByUserId, CancellationToken cancellationToken, Guid? scheduleId = null)
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

        var job = new ReportJob
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.TenantId,
            ReportId = reportId,
            ScheduleId = scheduleId,
            ReportName = report.Name,
            Format = format,
            Status = ReportJobStatus.Queued,
            CreatedByUserId = createdByUserId,
            CreatedAtUtc = clock.GetUtcNow().UtcDateTime,
        };
        db.ReportJobs.Add(job);
        await db.SaveChangesAsync(cancellationToken);
        return ToInfo(job);
    }

    /// <summary>The most recent <see cref="ListLimit"/> jobs. Projected in the query rather than
    /// materialising entities: a job row carries its rendered result as a blob, and this list is
    /// polled every few seconds by every open tab (the Jobs page, the nav badge, the
    /// finished-job notifier), so selecting whole rows meant dragging up to fifty rendered PDFs
    /// out of the database on every poll.</summary>
    public async Task<IReadOnlyList<ReportJobInfo>> ListAsync(CancellationToken cancellationToken)
    {
        var rows = await db.ReportJobs
            .AsNoTracking()
            .Where(j => j.TenantId == tenant.TenantId)
            .OrderByDescending(j => j.CreatedAtUtc)
            .Take(ListLimit)
            .Select(j => new ReportJobInfo(
                j.Id, j.ReportId, j.ReportName, j.Format, j.Status, j.ErrorMessage,
                j.CreatedAtUtc, j.StartedAtUtc, j.CompletedAtUtc, j.ScheduleId))
            .ToListAsync(cancellationToken);

        return rows;
    }

    public Task<ReportJobInfo?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.ReportJobs
            .AsNoTracking()
            .Where(j => j.Id == id && j.TenantId == tenant.TenantId)
            .Select(j => new ReportJobInfo(
                j.Id, j.ReportId, j.ReportName, j.Format, j.Status, j.ErrorMessage,
                j.CreatedAtUtc, j.StartedAtUtc, j.CompletedAtUtc, j.ScheduleId))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<ReportJobResult?> GetResultAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await db.ReportJobs
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == id && j.TenantId == tenant.TenantId && j.Status == ReportJobStatus.Succeeded, cancellationToken);

        return row?.ResultContent is null
            ? null
            : new ReportJobResult(row.ResultContent, row.ResultContentType ?? "application/octet-stream", row.ResultFileName ?? "report");
    }

    public async Task<ClaimedReportJob?> ClaimNextAsync(CancellationToken cancellationToken)
    {
        var job = await db.ReportJobs
            .Where(j => j.Status == ReportJobStatus.Queued)
            .OrderBy(j => j.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (job is null)
        {
            return null;
        }

        job.Status = ReportJobStatus.Running;
        job.StartedAtUtc = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(cancellationToken);
        return new ClaimedReportJob(job.Id, job.TenantId, job.ReportId, job.Format, job.ScheduleId);
    }

    public async Task CompleteAsync(Guid id, byte[] content, string contentType, string fileName, CancellationToken cancellationToken)
    {
        var job = await db.ReportJobs.FirstOrDefaultAsync(j => j.Id == id, cancellationToken);
        if (job is null)
        {
            return;
        }

        job.Status = ReportJobStatus.Succeeded;
        job.ResultContent = content;
        job.ResultContentType = contentType;
        job.ResultFileName = fileName;
        job.CompletedAtUtc = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task FailAsync(Guid id, string errorMessage, CancellationToken cancellationToken)
    {
        var job = await db.ReportJobs.FirstOrDefaultAsync(j => j.Id == id, cancellationToken);
        if (job is null)
        {
            return;
        }

        job.Status = ReportJobStatus.Failed;
        job.ErrorMessage = errorMessage;
        job.CompletedAtUtc = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Drops finished jobs past their retention window — every row holds a rendered
    /// report, so a daily schedule left alone would grow the database without limit. Runs
    /// tenant-agnostically from the background worker, which has no signed-in tenant.</summary>
    public Task<int> DeleteFinishedBeforeAsync(DateTime cutoffUtc, CancellationToken cancellationToken) =>
        db.ReportJobs
            .Where(j => j.CompletedAtUtc != null && j.CompletedAtUtc < cutoffUtc)
            .ExecuteDeleteAsync(cancellationToken);

    public async Task<CancelOutcome> RequestCancelAsync(Guid id, CancellationToken cancellationToken)
    {
        var job = await db.ReportJobs.FirstOrDefaultAsync(j => j.Id == id && j.TenantId == tenant.TenantId, cancellationToken);
        if (job is null)
        {
            return CancelOutcome.NotCancellable;
        }

        if (job.Status == ReportJobStatus.Running)
        {
            return CancelOutcome.Running;
        }

        if (job.Status != ReportJobStatus.Queued)
        {
            return CancelOutcome.NotCancellable;
        }

        job.Status = ReportJobStatus.Cancelled;
        job.CompletedAtUtc = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(cancellationToken);
        return CancelOutcome.Cancelled;
    }

    public async Task MarkCancelledAsync(Guid id, CancellationToken cancellationToken)
    {
        var job = await db.ReportJobs.FirstOrDefaultAsync(j => j.Id == id, cancellationToken);
        if (job is null)
        {
            return;
        }

        job.Status = ReportJobStatus.Cancelled;
        job.CompletedAtUtc = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task RecoverStuckAsync(CancellationToken cancellationToken) =>
        db.ReportJobs
            .Where(j => j.Status == ReportJobStatus.Running)
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.Status, ReportJobStatus.Queued), cancellationToken);

    private static ReportJobInfo ToInfo(ReportJob j) => new(
        j.Id, j.ReportId, j.ReportName, j.Format, j.Status, j.ErrorMessage, j.CreatedAtUtc, j.StartedAtUtc,
        j.CompletedAtUtc, j.ScheduleId);
}
