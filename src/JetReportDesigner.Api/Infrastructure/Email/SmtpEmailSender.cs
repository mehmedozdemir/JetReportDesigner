using JetReportDesigner.Storage.Email;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace JetReportDesigner.Api.Infrastructure.Email;

internal sealed class SmtpEmailSender : IEmailSender
{
    public async Task SendAsync(SmtpSettingsForSending settings, OutgoingEmail email, CancellationToken cancellationToken)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(settings.FromName ?? settings.FromEmail, settings.FromEmail));
        message.To.Add(MailboxAddress.Parse(email.ToEmail));
        message.Subject = email.Subject;

        var body = new BodyBuilder { HtmlBody = email.BodyHtml };
        foreach (var attachment in email.Attachments ?? [])
        {
            body.Attachments.Add(attachment.FileName, attachment.Content, ContentType.Parse(attachment.ContentType));
        }

        message.Body = body.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(settings.Host, settings.Port, ToSecureSocketOptions(settings.Security), cancellationToken);
        if (!string.IsNullOrEmpty(settings.Username))
        {
            await client.AuthenticateAsync(settings.Username, settings.Password, cancellationToken);
        }

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }

    private static SecureSocketOptions ToSecureSocketOptions(string security) => security switch
    {
        "None" => SecureSocketOptions.None,
        "SslOnConnect" => SecureSocketOptions.SslOnConnect,
        _ => SecureSocketOptions.StartTls,
    };
}
