using JetReportDesigner.Core.Model;
using JetReportDesigner.Core.Serialization;
using JetReportDesigner.Storage.Entities;
using Microsoft.EntityFrameworkCore;

namespace JetReportDesigner.Storage.Repositories;

internal sealed class ReportRepository(JetReportDbContext db, TimeProvider clock) : IReportRepository
{
    public async Task<IReadOnlyList<ReportSummary>> ListAsync(CancellationToken cancellationToken)
    {
        var rows = await db.Reports
            .AsNoTracking()
            .OrderByDescending(r => r.UpdatedAtUtc)
            .Select(r => new { r.Id, r.Name, r.LayoutMode, r.CreatedAtUtc, r.UpdatedAtUtc })
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new ReportSummary(r.Id, r.Name, ParseLayout(r.LayoutMode), r.CreatedAtUtc, r.UpdatedAtUtc))
            .ToList();
    }

    public async Task<ReportRecord?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await db.Reports.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        return row is null ? null : ToRecord(row);
    }

    public async Task<ReportRecord> CreateAsync(ReportDefinition definition, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var id = definition.Id == Guid.Empty ? Guid.NewGuid() : definition.Id;
        definition.Id = id;

        var row = new StoredReport
        {
            Id = id,
            Name = definition.Name,
            Description = definition.Description,
            LayoutMode = LayoutToString(definition.LayoutMode),
            DefinitionJson = ReportJson.Serialize(definition),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            ConcurrencyToken = Guid.NewGuid(),
        };

        db.Reports.Add(row);
        db.ReportVersions.Add(NewVersion(row, version: 1));
        await db.SaveChangesAsync(cancellationToken);
        return ToRecord(row);
    }

    public async Task<ReportRecord?> UpdateAsync(
        Guid id,
        ReportDefinition definition,
        Guid? expectedToken,
        CancellationToken cancellationToken)
    {
        var row = await db.Reports.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (row is null)
        {
            return null;
        }

        if (expectedToken is { } token && row.ConcurrencyToken != token)
        {
            throw new ReportConcurrencyException(id);
        }

        definition.Id = id;
        row.Name = definition.Name;
        row.Description = definition.Description;
        row.LayoutMode = LayoutToString(definition.LayoutMode);
        row.DefinitionJson = ReportJson.Serialize(definition);
        row.UpdatedAtUtc = clock.GetUtcNow().UtcDateTime;
        row.ConcurrencyToken = Guid.NewGuid();

        var nextVersion = await NextVersionNumberAsync(id, cancellationToken);
        db.ReportVersions.Add(NewVersion(row, nextVersion));

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ReportConcurrencyException(id);
        }

        return ToRecord(row);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await db.ReportVersions.Where(v => v.ReportId == id).ExecuteDeleteAsync(cancellationToken);
        var deleted = await db.Reports.Where(r => r.Id == id).ExecuteDeleteAsync(cancellationToken);
        return deleted > 0;
    }

    public async Task<IReadOnlyList<ReportVersionInfo>> ListVersionsAsync(Guid id, CancellationToken cancellationToken)
    {
        var rows = await db.ReportVersions
            .AsNoTracking()
            .Where(v => v.ReportId == id)
            .OrderByDescending(v => v.Version)
            .Select(v => new { v.Version, v.Name, v.SavedAtUtc })
            .ToListAsync(cancellationToken);

        return rows.Select(v => new ReportVersionInfo(v.Version, v.Name, v.SavedAtUtc)).ToList();
    }

    public async Task<ReportVersionRecord?> GetVersionAsync(Guid id, int version, CancellationToken cancellationToken)
    {
        var row = await db.ReportVersions
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.ReportId == id && v.Version == version, cancellationToken);

        return row is null
            ? null
            : new ReportVersionRecord(row.Version, row.Name, row.SavedAtUtc, ReportJson.Deserialize(row.DefinitionJson));
    }

    public async Task<ReportRecord?> RestoreVersionAsync(Guid id, int version, CancellationToken cancellationToken)
    {
        var snapshot = await GetVersionAsync(id, version, cancellationToken);
        if (snapshot is null)
        {
            return null;
        }

        return await UpdateAsync(id, snapshot.Definition, expectedToken: null, cancellationToken);
    }

    private async Task<int> NextVersionNumberAsync(Guid id, CancellationToken cancellationToken)
    {
        var max = await db.ReportVersions
            .Where(v => v.ReportId == id)
            .Select(v => (int?)v.Version)
            .MaxAsync(cancellationToken);
        return (max ?? 0) + 1;
    }

    private StoredReportVersion NewVersion(StoredReport row, int version) => new()
    {
        Id = Guid.NewGuid(),
        ReportId = row.Id,
        Version = version,
        Name = row.Name,
        DefinitionJson = row.DefinitionJson,
        SavedAtUtc = clock.GetUtcNow().UtcDateTime,
    };

    private static ReportRecord ToRecord(StoredReport row) => new(
        row.Id,
        ReportJson.Deserialize(row.DefinitionJson),
        row.CreatedAtUtc,
        row.UpdatedAtUtc,
        row.ConcurrencyToken);

    private static string LayoutToString(LayoutMode mode) =>
        mode == LayoutMode.Banded ? "banded" : "free";

    private static LayoutMode ParseLayout(string value) =>
        string.Equals(value, "banded", StringComparison.OrdinalIgnoreCase) ? LayoutMode.Banded : LayoutMode.Free;
}
