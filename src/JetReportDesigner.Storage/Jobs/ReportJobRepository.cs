using JetReportDesigner.Storage.Entities;
using JetReportDesigner.Storage.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace JetReportDesigner.Storage.Jobs;

internal sealed class ReportJobRepository(JetReportDbContext db, TimeProvider clock, ICurrentTenant tenant) : IReportJobRepository
{
    public async Task<ReportJobInfo?> EnqueueAsync(Guid reportId, string format, Guid createdByUserId, CancellationToken cancellationToken)
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

    public async Task<IReadOnlyList<ReportJobInfo>> ListAsync(CancellationToken cancellationToken)
    {
        var rows = await db.ReportJobs
            .AsNoTracking()
            .Where(j => j.TenantId == tenant.TenantId)
            .OrderByDescending(j => j.CreatedAtUtc)
            .Take(50)
            .ToListAsync(cancellationToken);

        return rows.Select(ToInfo).ToList();
    }

    public async Task<ReportJobInfo?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await db.ReportJobs.AsNoTracking().FirstOrDefaultAsync(j => j.Id == id && j.TenantId == tenant.TenantId, cancellationToken);
        return row is null ? null : ToInfo(row);
    }

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
        return new ClaimedReportJob(job.Id, job.TenantId, job.ReportId, job.Format);
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

    public Task RecoverStuckAsync(CancellationToken cancellationToken) =>
        db.ReportJobs
            .Where(j => j.Status == ReportJobStatus.Running)
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.Status, ReportJobStatus.Queued), cancellationToken);

    private static ReportJobInfo ToInfo(ReportJob j) => new(
        j.Id, j.ReportId, j.ReportName, j.Format, j.Status, j.ErrorMessage, j.CreatedAtUtc, j.StartedAtUtc, j.CompletedAtUtc);
}
