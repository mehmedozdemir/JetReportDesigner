using System.Security.Cryptography;
using JetReportDesigner.Storage.Entities;
using Microsoft.EntityFrameworkCore;

namespace JetReportDesigner.Storage.Assets;

/// <summary>Asset metadata without the bytes — for listing and for the upload response.</summary>
public sealed record SavedAsset(Guid Id, string FileName, string ContentType, int ByteLength, DateTime CreatedAtUtc);

/// <summary>An asset's bytes together with the type needed to serve them.</summary>
public sealed record AssetContent(byte[] Bytes, string ContentType, string Sha256);

public interface IAssetRepository
{
    Task<IReadOnlyList<SavedAsset>> ListAsync(CancellationToken cancellationToken);

    Task<AssetContent?> GetContentAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Stores <paramref name="bytes"/>, or returns the existing asset when identical
    /// bytes were uploaded before (matched on SHA-256).
    /// </summary>
    Task<SavedAsset> AddAsync(byte[] bytes, string contentType, string fileName, CancellationToken cancellationToken);
}

internal sealed class AssetRepository(JetReportDbContext db, TimeProvider clock) : IAssetRepository
{
    public async Task<IReadOnlyList<SavedAsset>> ListAsync(CancellationToken cancellationToken)
    {
        var rows = await db.Assets
            .AsNoTracking()
            .OrderByDescending(a => a.CreatedAtUtc)
            .Select(a => new SavedAsset(a.Id, a.FileName, a.ContentType, a.ByteLength, a.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return rows;
    }

    public async Task<AssetContent?> GetContentAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await db.Assets
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        return row is null ? null : new AssetContent(row.Content, row.ContentType, row.Sha256);
    }

    public async Task<SavedAsset> AddAsync(
        byte[] bytes,
        string contentType,
        string fileName,
        CancellationToken cancellationToken)
    {
        var sha = Convert.ToHexStringLower(SHA256.HashData(bytes));

        var existing = await db.Assets.AsNoTracking().FirstOrDefaultAsync(a => a.Sha256 == sha, cancellationToken);
        if (existing is not null)
        {
            return new SavedAsset(existing.Id, existing.FileName, existing.ContentType, existing.ByteLength, existing.CreatedAtUtc);
        }

        var row = new StoredAsset
        {
            Id = Guid.NewGuid(),
            Sha256 = sha,
            ContentType = contentType,
            FileName = fileName,
            ByteLength = bytes.Length,
            Content = bytes,
            CreatedAtUtc = clock.GetUtcNow().UtcDateTime,
        };
        db.Assets.Add(row);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // A concurrent upload of the same bytes may have won the unique-index race.
            var race = await db.Assets.AsNoTracking().FirstOrDefaultAsync(a => a.Sha256 == sha, cancellationToken);
            if (race is null)
            {
                throw;
            }

            return new SavedAsset(race.Id, race.FileName, race.ContentType, race.ByteLength, race.CreatedAtUtc);
        }

        return new SavedAsset(row.Id, row.FileName, row.ContentType, row.ByteLength, row.CreatedAtUtc);
    }
}
