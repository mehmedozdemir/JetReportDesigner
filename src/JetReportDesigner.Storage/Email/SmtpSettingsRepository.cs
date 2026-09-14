using JetReportDesigner.Storage.Connections;
using JetReportDesigner.Storage.Entities;
using JetReportDesigner.Storage.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace JetReportDesigner.Storage.Email;

internal sealed class SmtpSettingsRepository(JetReportDbContext db, IConnectionSecretProtector protector, TimeProvider clock, ICurrentTenant tenant)
    : ISmtpSettingsRepository
{
    public async Task<SmtpSettingsInfo?> GetAsync(CancellationToken cancellationToken)
    {
        var row = await db.SmtpSettings.AsNoTracking().FirstOrDefaultAsync(s => s.TenantId == tenant.TenantId, cancellationToken);
        return row is null ? null : ToInfo(row);
    }

    public async Task<SmtpSettingsInfo> SetAsync(
        string host,
        int port,
        string security,
        string username,
        string? password,
        string fromEmail,
        string? fromName,
        Guid updatedByUserId,
        CancellationToken cancellationToken)
    {
        var row = await db.SmtpSettings.FirstOrDefaultAsync(s => s.TenantId == tenant.TenantId, cancellationToken);
        if (row is null)
        {
            row = new SmtpSettings { TenantId = tenant.TenantId };
            db.SmtpSettings.Add(row);
        }

        row.Host = host;
        row.Port = port;
        row.Security = security;
        row.Username = username;
        if (!string.IsNullOrWhiteSpace(password))
        {
            row.EncryptedPassword = protector.Protect(password);
        }

        row.FromEmail = fromEmail;
        row.FromName = fromName;
        row.UpdatedAtUtc = clock.GetUtcNow().UtcDateTime;
        row.UpdatedByUserId = updatedByUserId;

        await db.SaveChangesAsync(cancellationToken);
        return ToInfo(row);
    }

    public async Task<bool> DeleteAsync(CancellationToken cancellationToken)
    {
        var deleted = await db.SmtpSettings.Where(s => s.TenantId == tenant.TenantId).ExecuteDeleteAsync(cancellationToken);
        return deleted > 0;
    }

    public async Task<SmtpSettingsForSending?> GetForTenantAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var row = await db.SmtpSettings.AsNoTracking().FirstOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken);
        return row is null
            ? null
            : new SmtpSettingsForSending(row.Host, row.Port, row.Security, row.Username, protector.Unprotect(row.EncryptedPassword), row.FromEmail, row.FromName);
    }

    private static SmtpSettingsInfo ToInfo(SmtpSettings row) => new(
        row.Host, row.Port, row.Security, row.Username, row.FromEmail, row.FromName,
        HasPassword: !string.IsNullOrEmpty(row.EncryptedPassword),
        row.UpdatedAtUtc);
}
