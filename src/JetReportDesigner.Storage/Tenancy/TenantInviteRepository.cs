using System.Security.Cryptography;
using JetReportDesigner.Storage.Entities;
using Microsoft.EntityFrameworkCore;

namespace JetReportDesigner.Storage.Tenancy;

internal sealed class TenantInviteRepository(JetReportDbContext db, TimeProvider clock) : ITenantInviteRepository
{
    // Excludes visually-ambiguous characters (0/O, 1/I/L) since codes are read and typed by hand.
    private const string CodeAlphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";
    private const int CodeLength = 8;

    public async Task<CreatedInvite> CreateAsync(Guid tenantId, string role, Guid createdByUserId, TimeSpan ttl, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var invite = new TenantInvite
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Code = await GenerateUniqueCodeAsync(cancellationToken),
            Role = role,
            CreatedByUserId = createdByUserId,
            CreatedAtUtc = now,
            ExpiresAtUtc = now + ttl,
        };
        db.TenantInvites.Add(invite);
        await db.SaveChangesAsync(cancellationToken);
        return new CreatedInvite(invite.Code, invite.Role, invite.ExpiresAtUtc);
    }

    public async Task<IReadOnlyList<PendingInvite>> ListPendingAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var rows = await db.TenantInvites
            .AsNoTracking()
            .Where(i => i.TenantId == tenantId && i.UsedAtUtc == null && i.ExpiresAtUtc > now)
            .OrderByDescending(i => i.CreatedAtUtc)
            .Select(i => new { i.Code, i.Role, i.CreatedAtUtc, i.ExpiresAtUtc })
            .ToListAsync(cancellationToken);

        return rows.Select(i => new PendingInvite(i.Code, i.Role, i.CreatedAtUtc, i.ExpiresAtUtc)).ToList();
    }

    public async Task<bool> RevokeAsync(Guid tenantId, string code, CancellationToken cancellationToken)
    {
        var deleted = await db.TenantInvites
            .Where(i => i.TenantId == tenantId && i.Code == code.ToUpperInvariant() && i.UsedAtUtc == null)
            .ExecuteDeleteAsync(cancellationToken);
        return deleted > 0;
    }

    public async Task<ConsumedInvite?> ConsumeAsync(string code, Guid usedByUserId, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var normalized = code.Trim().ToUpperInvariant();
        var invite = await db.TenantInvites
            .FirstOrDefaultAsync(i => i.Code == normalized && i.UsedAtUtc == null && i.ExpiresAtUtc > now, cancellationToken);
        if (invite is null)
        {
            return null;
        }

        invite.UsedAtUtc = now;
        invite.UsedByUserId = usedByUserId;
        await db.SaveChangesAsync(cancellationToken);
        return new ConsumedInvite(invite.TenantId, invite.Role);
    }

    private async Task<string> GenerateUniqueCodeAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var code = RandomNumberGenerator.GetString(CodeAlphabet, CodeLength);
            if (!await db.TenantInvites.AnyAsync(i => i.Code == code, cancellationToken))
            {
                return code;
            }
        }

        throw new InvalidOperationException("Could not generate a unique invite code.");
    }
}
