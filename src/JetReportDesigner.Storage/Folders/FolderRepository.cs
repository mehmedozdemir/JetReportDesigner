using JetReportDesigner.Storage.Entities;
using JetReportDesigner.Storage.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace JetReportDesigner.Storage.Folders;

internal sealed class FolderRepository(JetReportDbContext db, TimeProvider clock, ICurrentTenant tenant) : IFolderRepository
{
    public async Task<IReadOnlyList<FolderInfo>> ListAsync(CancellationToken cancellationToken)
    {
        var rows = await db.Folders
            .AsNoTracking()
            .Where(f => f.TenantId == tenant.TenantId)
            .OrderBy(f => f.Name)
            .ToListAsync(cancellationToken);

        return rows.Select(ToInfo).ToList();
    }

    public async Task<FolderInfo?> CreateAsync(string name, Guid? parentFolderId, CancellationToken cancellationToken)
    {
        if (parentFolderId is { } parentId && !await db.Folders.AnyAsync(f => f.Id == parentId && f.TenantId == tenant.TenantId, cancellationToken))
        {
            return null;
        }

        var folder = new Folder
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.TenantId,
            ParentFolderId = parentFolderId,
            Name = name,
            CreatedAtUtc = clock.GetUtcNow().UtcDateTime,
        };
        db.Folders.Add(folder);
        await db.SaveChangesAsync(cancellationToken);
        return ToInfo(folder);
    }

    public async Task<FolderInfo?> RenameAsync(Guid id, string name, CancellationToken cancellationToken)
    {
        var folder = await db.Folders.FirstOrDefaultAsync(f => f.Id == id && f.TenantId == tenant.TenantId, cancellationToken);
        if (folder is null)
        {
            return null;
        }

        folder.Name = name;
        await db.SaveChangesAsync(cancellationToken);
        return ToInfo(folder);
    }

    public async Task<FolderDeleteResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var exists = await db.Folders.AnyAsync(f => f.Id == id && f.TenantId == tenant.TenantId, cancellationToken);
        if (!exists)
        {
            return FolderDeleteResult.NotFound;
        }

        var hasSubfolders = await db.Folders.AnyAsync(f => f.ParentFolderId == id && f.TenantId == tenant.TenantId, cancellationToken);
        var hasReports = await db.ReportFolderEntries.AnyAsync(e => e.FolderId == id && e.TenantId == tenant.TenantId, cancellationToken);
        if (hasSubfolders || hasReports)
        {
            return FolderDeleteResult.NotEmpty;
        }

        await db.Folders.Where(f => f.Id == id && f.TenantId == tenant.TenantId).ExecuteDeleteAsync(cancellationToken);
        return FolderDeleteResult.Deleted;
    }

    public async Task<IReadOnlyDictionary<Guid, Guid>> GetReportFolderMapAsync(CancellationToken cancellationToken)
    {
        var rows = await db.ReportFolderEntries
            .AsNoTracking()
            .Where(e => e.TenantId == tenant.TenantId)
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(e => e.ReportId, e => e.FolderId);
    }

    public async Task<bool> SetReportFolderAsync(Guid reportId, Guid? folderId, CancellationToken cancellationToken)
    {
        if (folderId is { } id && !await db.Folders.AnyAsync(f => f.Id == id && f.TenantId == tenant.TenantId, cancellationToken))
        {
            return false;
        }

        var existing = await db.ReportFolderEntries.FirstOrDefaultAsync(e => e.ReportId == reportId && e.TenantId == tenant.TenantId, cancellationToken);

        if (folderId is null)
        {
            if (existing is not null)
            {
                db.ReportFolderEntries.Remove(existing);
                await db.SaveChangesAsync(cancellationToken);
            }

            return true;
        }

        if (existing is null)
        {
            db.ReportFolderEntries.Add(new ReportFolderEntry { ReportId = reportId, TenantId = tenant.TenantId, FolderId = folderId.Value });
        }
        else
        {
            existing.FolderId = folderId.Value;
        }

        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static FolderInfo ToInfo(Folder f) => new(f.Id, f.Name, f.ParentFolderId, f.CreatedAtUtc);
}
