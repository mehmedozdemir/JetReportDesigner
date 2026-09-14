namespace JetReportDesigner.Storage.Entities;

/// <summary>
/// A tenant's single outgoing-mail account — used for scheduled-report distribution.
/// One row per tenant (<see cref="TenantId"/> is the primary key). The password is
/// encrypted at rest via the same <c>IConnectionSecretProtector</c> used for SQL
/// connection strings.
/// </summary>
public sealed class SmtpSettings
{
    public Guid TenantId { get; set; }

    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 587;

    /// <summary>"None" | "StartTls" | "SslOnConnect".</summary>
    public string Security { get; set; } = "StartTls";

    public string Username { get; set; } = string.Empty;

    public string EncryptedPassword { get; set; } = string.Empty;

    public string FromEmail { get; set; } = string.Empty;

    public string? FromName { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public Guid UpdatedByUserId { get; set; }
}
