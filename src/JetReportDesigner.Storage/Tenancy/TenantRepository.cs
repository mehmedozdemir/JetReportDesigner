using JetReportDesigner.Storage.Entities;
using Microsoft.EntityFrameworkCore;

namespace JetReportDesigner.Storage.Tenancy;

internal sealed class TenantRepository(JetReportDbContext db, TimeProvider clock) : ITenantRepository
{
    public async Task<Guid> CreateAsync(string name, CancellationToken cancellationToken)
    {
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = name,
            CreatedAtUtc = clock.GetUtcNow().UtcDateTime,
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync(cancellationToken);
        return tenant.Id;
    }

    public async Task<TenantInfo?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        return row is null ? null : new TenantInfo(row.Id, row.Name, row.CreatedAtUtc);
    }

    public async Task<TenantInfo?> RenameAsync(Guid id, string name, CancellationToken cancellationToken)
    {
        var row = await db.Tenants.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (row is null)
        {
            return null;
        }

        row.Name = name;
        await db.SaveChangesAsync(cancellationToken);
        return new TenantInfo(row.Id, row.Name, row.CreatedAtUtc);
    }
}
