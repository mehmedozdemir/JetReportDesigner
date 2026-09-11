using JetReportDesigner.Core.Model;
using JetReportDesigner.Storage.Entities;
using JetReportDesigner.Storage.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace JetReportDesigner.Storage.Connections;

/// <summary>Metadata for a registered connection. Never carries the connection string.</summary>
public sealed record RegisteredConnection(Guid Id, string Name, SqlProvider Provider, DateTime CreatedAtUtc);

/// <summary>Encrypts and decrypts connection strings at rest. Implemented in the API with ASP.NET Data Protection.</summary>
public interface IConnectionSecretProtector
{
    string Protect(string plaintext);

    string Unprotect(string ciphertext);
}

public interface IConnectionRepository
{
    Task<IReadOnlyList<RegisteredConnection>> ListAsync(CancellationToken cancellationToken);

    Task<RegisteredConnection?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<RegisteredConnection> CreateAsync(string name, SqlProvider provider, string connectionString, CancellationToken cancellationToken);

    /// <summary>Returns null when the connection does not exist. A null <paramref name="connectionString"/> keeps the stored secret.</summary>
    Task<RegisteredConnection?> UpdateAsync(Guid id, string name, SqlProvider provider, string? connectionString, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Decrypts the stored connection string for use by the SQL data source reader. Not exposed over HTTP.</summary>
    Task<(SqlProvider Provider, string ConnectionString)?> ResolveAsync(Guid id, CancellationToken cancellationToken);
}

internal sealed class ConnectionRepository(
    JetReportDbContext db,
    IConnectionSecretProtector protector,
    TimeProvider clock,
    ICurrentTenant tenant) : IConnectionRepository
{
    public async Task<IReadOnlyList<RegisteredConnection>> ListAsync(CancellationToken cancellationToken)
    {
        var rows = await db.Connections
            .AsNoTracking()
            .Where(c => c.TenantId == tenant.TenantId)
            .OrderBy(c => c.Name)
            .Select(c => new { c.Id, c.Name, c.Provider, c.CreatedAtUtc })
            .ToListAsync(cancellationToken);

        return rows.Select(r => new RegisteredConnection(r.Id, r.Name, ParseProvider(r.Provider), r.CreatedAtUtc)).ToList();
    }

    public async Task<RegisteredConnection?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await db.Connections.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenant.TenantId, cancellationToken);
        return row is null ? null : ToInfo(row);
    }

    public async Task<RegisteredConnection> CreateAsync(
        string name,
        SqlProvider provider,
        string connectionString,
        CancellationToken cancellationToken)
    {
        var row = new StoredConnection
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.TenantId,
            Name = name,
            Provider = ProviderString(provider),
            EncryptedConnectionString = protector.Protect(connectionString),
            CreatedAtUtc = clock.GetUtcNow().UtcDateTime,
        };
        db.Connections.Add(row);
        await db.SaveChangesAsync(cancellationToken);
        return ToInfo(row);
    }

    public async Task<RegisteredConnection?> UpdateAsync(
        Guid id,
        string name,
        SqlProvider provider,
        string? connectionString,
        CancellationToken cancellationToken)
    {
        var row = await db.Connections.FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenant.TenantId, cancellationToken);
        if (row is null)
        {
            return null;
        }

        row.Name = name;
        row.Provider = ProviderString(provider);
        if (connectionString is not null)
        {
            row.EncryptedConnectionString = protector.Protect(connectionString);
        }

        await db.SaveChangesAsync(cancellationToken);
        return ToInfo(row);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await db.Connections.Where(c => c.Id == id && c.TenantId == tenant.TenantId).ExecuteDeleteAsync(cancellationToken);
        return deleted > 0;
    }

    public async Task<(SqlProvider Provider, string ConnectionString)?> ResolveAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await db.Connections.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id && c.TenantId == tenant.TenantId, cancellationToken);
        return row is null
            ? null
            : (ParseProvider(row.Provider), protector.Unprotect(row.EncryptedConnectionString));
    }

    private static RegisteredConnection ToInfo(StoredConnection row) =>
        new(row.Id, row.Name, ParseProvider(row.Provider), row.CreatedAtUtc);

    private static string ProviderString(SqlProvider provider) => provider switch
    {
        SqlProvider.SqlServer => "sqlServer",
        SqlProvider.PostgreSql => "postgreSql",
        SqlProvider.Oracle => "oracle",
        _ => "sqlServer",
    };

    private static SqlProvider ParseProvider(string value) => value.ToLowerInvariant() switch
    {
        "postgresql" => SqlProvider.PostgreSql,
        "oracle" => SqlProvider.Oracle,
        _ => SqlProvider.SqlServer,
    };
}
