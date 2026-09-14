namespace JetReportDesigner.Storage.Email;

/// <summary>Never carries the password — for the settings screen.</summary>
public sealed record SmtpSettingsInfo(
    string Host,
    int Port,
    string Security,
    string Username,
    string FromEmail,
    string? FromName,
    bool HasPassword,
    DateTime UpdatedAtUtc);

/// <summary>The decrypted form used to actually send mail — never exposed over HTTP.</summary>
public sealed record SmtpSettingsForSending(
    string Host,
    int Port,
    string Security,
    string Username,
    string Password,
    string FromEmail,
    string? FromName);

public interface ISmtpSettingsRepository
{
    Task<SmtpSettingsInfo?> GetAsync(CancellationToken cancellationToken);

    /// <summary>Upserts the tenant's mail account. A null/blank <paramref name="password"/> keeps
    /// the previously stored one (so editing other fields doesn't force re-entering it).</summary>
    Task<SmtpSettingsInfo> SetAsync(
        string host,
        int port,
        string security,
        string username,
        string? password,
        string fromEmail,
        string? fromName,
        Guid updatedByUserId,
        CancellationToken cancellationToken);

    Task<bool> DeleteAsync(CancellationToken cancellationToken);

    /// <summary>Decrypted settings for the background sender — keyed by an explicit tenant id
    /// since a scheduled job has no signed-in tenant to key off.</summary>
    Task<SmtpSettingsForSending?> GetForTenantAsync(Guid tenantId, CancellationToken cancellationToken);
}
