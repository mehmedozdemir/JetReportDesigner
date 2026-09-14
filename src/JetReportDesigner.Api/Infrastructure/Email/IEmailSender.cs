using JetReportDesigner.Storage.Email;

namespace JetReportDesigner.Api.Infrastructure.Email;

public sealed record OutgoingEmail(
    string ToEmail,
    string Subject,
    string BodyHtml,
    IReadOnlyList<EmailAttachment>? Attachments = null);

public sealed record EmailAttachment(string FileName, string ContentType, byte[] Content);

/// <summary>Sends mail through a tenant's own configured SMTP account. No sending happens
/// (a clear exception instead) when the tenant hasn't set one up.</summary>
public interface IEmailSender
{
    Task SendAsync(SmtpSettingsForSending settings, OutgoingEmail email, CancellationToken cancellationToken);
}
