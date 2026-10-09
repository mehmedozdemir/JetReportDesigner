using System.Security.Cryptography;
using System.Text;
using JetReportDesigner.Storage.Entities;
using JetReportDesigner.Storage.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace JetReportDesigner.Storage.ApiKeys;

internal sealed class ApiKeyRepository(JetReportDbContext db, TimeProvider clock, ICurrentTenant tenant) : IApiKeyRepository
{
    public const string Prefix = "jrd_";

    /// <summary>"Last used" is only rewritten when older than this, so a busy client does not turn every call into a write.</summary>
    private static readonly TimeSpan UseTouchInterval = TimeSpan.FromMinutes(1);

    public static string Hash(string key) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));

    public async Task<IReadOnlyList<ApiKey>> ListAsync(CancellationToken cancellationToken) =>
        await db.ApiKeys
            .AsNoTracking()
            .Where(k => k.TenantId == tenant.TenantId)
            .OrderByDescending(k => k.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    public async Task<NewApiKey> CreateAsync(
        string name,
        string? description,
        DateTime? expiresAtUtc,
        string? createdByEmail,
        CancellationToken cancellationToken)
    {
        var secret = Prefix + Base64Url(RandomNumberGenerator.GetBytes(32));
        var key = new ApiKey
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.TenantId,
            Name = name.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            KeyHash = Hash(secret),
            KeyPrefix = secret[..(Prefix.Length + 8)],
            IsActive = true,
            ExpiresAtUtc = expiresAtUtc,
            CreatedAtUtc = clock.GetUtcNow().UtcDateTime,
            CreatedByEmail = createdByEmail,
        };

        db.ApiKeys.Add(key);
        await db.SaveChangesAsync(cancellationToken);
        return new NewApiKey(key, secret);
    }

    public async Task<ApiKey?> UpdateAsync(Guid id, string name, string? description, DateTime? expiresAtUtc, CancellationToken cancellationToken)
    {
        var key = await db.ApiKeys.FirstOrDefaultAsync(k => k.Id == id && k.TenantId == tenant.TenantId, cancellationToken);
        if (key is null)
        {
            return null;
        }

        key.Name = name.Trim();
        key.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        key.ExpiresAtUtc = expiresAtUtc;
        await db.SaveChangesAsync(cancellationToken);
        return key;
    }

    public async Task<ApiKey?> SetActiveAsync(Guid id, bool active, CancellationToken cancellationToken)
    {
        var key = await db.ApiKeys.FirstOrDefaultAsync(k => k.Id == id && k.TenantId == tenant.TenantId, cancellationToken);
        if (key is null)
        {
            return null;
        }

        key.IsActive = active;
        await db.SaveChangesAsync(cancellationToken);
        return key;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken) =>
        await db.ApiKeys.Where(k => k.Id == id && k.TenantId == tenant.TenantId).ExecuteDeleteAsync(cancellationToken) > 0;

    public async Task<ApiKey?> AuthenticateAsync(string plainTextKey, CancellationToken cancellationToken)
    {
        var hash = Hash(plainTextKey);
        var key = await db.ApiKeys.FirstOrDefaultAsync(k => k.KeyHash == hash, cancellationToken);
        var now = clock.GetUtcNow().UtcDateTime;
        if (key is null || !key.IsActive || (key.ExpiresAtUtc is { } expires && expires <= now))
        {
            return null;
        }

        if (key.LastUsedAtUtc is null || now - key.LastUsedAtUtc > UseTouchInterval)
        {
            key.LastUsedAtUtc = now;
            await db.SaveChangesAsync(cancellationToken);
        }

        return key;
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
