using System.Security.Cryptography;
using JetReportDesigner.Storage.Entities;
using JetReportDesigner.Storage.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace JetReportDesigner.Storage.Sharing;

internal sealed class ReportShareRepository(JetReportDbContext db, TimeProvider clock, ICurrentTenant tenant) : IReportShareRepository
{
    public async Task<ReportShareInfo?> CreateAsync(Guid reportId, Guid createdByUserId, string? createdByEmail, CancellationToken cancellationToken)
    {
        if (!await db.Reports.AnyAsync(r => r.Id == reportId && r.TenantId == tenant.TenantId, cancellationToken))
        {
            return null;
        }

        var share = new ReportShare
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.TenantId,
            ReportId = reportId,
            Token = await GenerateUniqueTokenAsync(cancellationToken),
            CreatedByUserId = createdByUserId,
            CreatedByEmail = createdByEmail,
            CreatedAtUtc = clock.GetUtcNow().UtcDateTime,
        };
        db.ReportShares.Add(share);
        await db.SaveChangesAsync(cancellationToken);
        return new ReportShareInfo(share.Token, share.CreatedAtUtc, share.CreatedByEmail);
    }

    public async Task<IReadOnlyList<ReportShareInfo>> ListForReportAsync(Guid reportId, CancellationToken cancellationToken)
    {
        var rows = await db.ReportShares
            .AsNoTracking()
            .Where(s => s.ReportId == reportId && s.TenantId == tenant.TenantId)
            .OrderByDescending(s => s.CreatedAtUtc)
            .Select(s => new { s.Token, s.CreatedAtUtc, s.CreatedByEmail })
            .ToListAsync(cancellationToken);

        return rows.Select(s => new ReportShareInfo(s.Token, s.CreatedAtUtc, s.CreatedByEmail)).ToList();
    }

    public async Task<bool> RevokeAsync(Guid reportId, string token, CancellationToken cancellationToken)
    {
        var deleted = await db.ReportShares
            .Where(s => s.ReportId == reportId && s.TenantId == tenant.TenantId && s.Token == token)
            .ExecuteDeleteAsync(cancellationToken);
        return deleted > 0;
    }

    public async Task<ResolvedShare?> ResolveAsync(string token, CancellationToken cancellationToken)
    {
        var row = await db.ReportShares
            .AsNoTracking()
            .Where(s => s.Token == token)
            .Select(s => new { s.TenantId, s.ReportId })
            .FirstOrDefaultAsync(cancellationToken);

        return row is null ? null : new ResolvedShare(row.TenantId, row.ReportId);
    }

    private async Task<string> GenerateUniqueTokenAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var token = RandomNumberGenerator.GetHexString(32, lowercase: true);
            if (!await db.ReportShares.AnyAsync(s => s.Token == token, cancellationToken))
            {
                return token;
            }
        }

        throw new InvalidOperationException("Could not generate a unique share token.");
    }
}
