using JetReportDesigner.Storage.Entities;
using Microsoft.EntityFrameworkCore;

namespace JetReportDesigner.Storage.Connections;

/// <summary>A SELECT query saved against a connection.</summary>
public sealed record SavedSqlQuery(Guid Id, Guid ConnectionId, string Name, string CommandText, DateTime CreatedAtUtc);

public interface ISqlQueryRepository
{
    Task<IReadOnlyList<SavedSqlQuery>> ListAsync(Guid connectionId, CancellationToken cancellationToken);

    Task<SavedSqlQuery?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<SavedSqlQuery> CreateAsync(Guid connectionId, string name, string commandText, CancellationToken cancellationToken);

    /// <summary>Returns null when the query does not exist.</summary>
    Task<SavedSqlQuery?> UpdateAsync(Guid id, string name, string commandText, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);
}

internal sealed class SqlQueryRepository(JetReportDbContext db, TimeProvider clock) : ISqlQueryRepository
{
    public async Task<IReadOnlyList<SavedSqlQuery>> ListAsync(Guid connectionId, CancellationToken cancellationToken)
    {
        var rows = await db.SqlQueries
            .AsNoTracking()
            .Where(q => q.ConnectionId == connectionId)
            .OrderBy(q => q.Name)
            .ToListAsync(cancellationToken);

        return rows.Select(ToDto).ToList();
    }

    public async Task<SavedSqlQuery?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await db.SqlQueries.AsNoTracking().FirstOrDefaultAsync(q => q.Id == id, cancellationToken);
        return row is null ? null : ToDto(row);
    }

    public async Task<SavedSqlQuery> CreateAsync(
        Guid connectionId,
        string name,
        string commandText,
        CancellationToken cancellationToken)
    {
        var row = new StoredSqlQuery
        {
            Id = Guid.NewGuid(),
            ConnectionId = connectionId,
            Name = name,
            CommandText = commandText,
            CreatedAtUtc = clock.GetUtcNow().UtcDateTime,
        };
        db.SqlQueries.Add(row);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(row);
    }

    public async Task<SavedSqlQuery?> UpdateAsync(
        Guid id,
        string name,
        string commandText,
        CancellationToken cancellationToken)
    {
        var row = await db.SqlQueries.FirstOrDefaultAsync(q => q.Id == id, cancellationToken);
        if (row is null)
        {
            return null;
        }

        row.Name = name;
        row.CommandText = commandText;
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(row);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await db.SqlQueries.Where(q => q.Id == id).ExecuteDeleteAsync(cancellationToken);
        return deleted > 0;
    }

    private static SavedSqlQuery ToDto(StoredSqlQuery row) =>
        new(row.Id, row.ConnectionId, row.Name, row.CommandText, row.CreatedAtUtc);
}
