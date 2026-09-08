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
        var deleted = await db.Reports.Where(r => r.Id == id).ExecuteDeleteAsync(cancellationToken);
        return deleted > 0;
    }

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
