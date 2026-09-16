using JetReportDesigner.Storage.Entities;
using JetReportDesigner.Storage.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace JetReportDesigner.Storage.Jobs;

internal sealed class ReportJobRepository(JetReportDbContext db, TimeProvider clock, ICurrentTenant tenant) : IReportJobRepository
{
    /// <summary>Upper bound on a single page, so a hand-written <c>?take=100000</c> can't ask
    /// the database for the entire history.</summary>
    public const int MaxPageSize = 200;

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

    /// <summary>One page of jobs. Projected in the query rather than materialising entities:
    /// a job row carries its rendered result as a blob, and this list is polled every few
    /// seconds by every open tab (the Jobs page, the nav badge, the finished-job notifier), so
    /// selecting whole rows meant dragging up to fifty rendered PDFs out of the database on
    /// every poll.</summary>
    public async Task<ReportJobPage> ListAsync(ReportJobQuery query, CancellationToken cancellationToken)
    {
        var rows = db.ReportJobs
            .AsNoTracking()
            .Where(j => j.TenantId == tenant.TenantId);

        if (query.Statuses.Count > 0)
        {
            rows = rows.Where(j => query.Statuses.Contains(j.Status));
        }

        var total = await rows.CountAsync(cancellationToken);

        var take = Math.Clamp(query.Take, 1, MaxPageSize);
        var page = await Sort(rows, query)
            .Skip(Math.Max(query.Skip, 0))
            .Take(take)
            .Select(j => new ReportJobInfo(
                j.Id, j.ReportId, j.ReportName, j.Format, j.Status, j.ErrorMessage,
                j.CreatedAtUtc, j.StartedAtUtc, j.CompletedAtUtc, j.ScheduleId))
            .ToListAsync(cancellationToken);

        return new ReportJobPage(page, total);
    }

    /// <summary>Ties break on <c>Id</c>: without it two jobs created in the same tick can swap
    /// places between polls and the row you were about to click moves.</summary>
    private static IQueryable<ReportJob> Sort(IQueryable<ReportJob> rows, ReportJobQuery query)
    {
        var desc = query.Descending;
        var ordered = query.SortKey switch
        {
            ReportJobSort.ReportName => desc ? rows.OrderByDescending(j => j.ReportName) : rows.OrderBy(j => j.ReportName),
            ReportJobSort.Format => desc ? rows.OrderByDescending(j => j.Format) : rows.OrderBy(j => j.Format),
            ReportJobSort.Status => desc ? rows.OrderByDescending(j => j.Status) : rows.OrderBy(j => j.Status),
            _ => desc ? rows.OrderByDescending(j => j.CreatedAtUtc) : rows.OrderBy(j => j.CreatedAtUtc),
        };
        return ordered.ThenByDescending(j => j.Id);
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
