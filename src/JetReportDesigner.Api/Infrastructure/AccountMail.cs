using System.Net;
using JetReportDesigner.Api.Infrastructure.Email;
using JetReportDesigner.Api.Localization;
using JetReportDesigner.Storage.Email;

namespace JetReportDesigner.Api.Infrastructure;

/// <summary>
/// Account emails (password reset, team invitation) and the links inside them. Mail goes out through
/// the organization's own SMTP account; without one, nothing is sent and callers fall back to links
/// a Designer can copy.
/// </summary>
public sealed class AccountMail(
    ISmtpSettingsRepository smtp,
    IEmailSender sender,
    IApiStrings strings,
    IConfiguration configuration,
    ILogger<AccountMail> logger)
{
    /// <summary>Absolute base for links: <c>App:PublicBaseUrl</c> when set (needed behind a proxy), else the request's own origin.</summary>
    public string BaseUrl(HttpRequest request) =>
        (configuration["App:PublicBaseUrl"] is { Length: > 0 } configured ? configured : $"{request.Scheme}://{request.Host}").TrimEnd('/');

    public string ResetLink(HttpRequest request, string email, string token) =>
        $"{BaseUrl(request)}/reset-password?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}";

    public string InviteLink(HttpRequest request, string code) => $"{BaseUrl(request)}/join/{Uri.EscapeDataString(code)}";

    public async Task<bool> IsConfiguredAsync(Guid tenantId, CancellationToken ct) =>
        await smtp.GetForTenantAsync(tenantId, ct) is not null;

    public Task<bool> SendResetAsync(Guid tenantId, string toEmail, string link, CancellationToken ct) =>
        SendAsync(tenantId, toEmail, strings["mail.reset.subject"], Body(
            strings["mail.reset.title"],
            strings["mail.reset.body"],
            strings["mail.reset.button"],
            link,
            strings["mail.reset.footer"]), ct);

    public Task<bool> SendInviteAsync(Guid tenantId, string toEmail, string organization, string inviter, string link, string code, CancellationToken ct) =>
        SendAsync(tenantId, toEmail, strings.Format("mail.invite.subject", organization), Body(
            strings.Format("mail.invite.title", organization),
            strings.Format("mail.invite.body", inviter, organization),
            strings["mail.invite.button"],
            link,
            strings.Format("mail.invite.footer", code)), ct);

    private async Task<bool> SendAsync(Guid tenantId, string toEmail, string subject, string html, CancellationToken ct)
    {
        var settings = await smtp.GetForTenantAsync(tenantId, ct);
        if (settings is null)
        {
            return false;
        }

        try
        {
            await sender.SendAsync(settings, new OutgoingEmail(toEmail, subject, html), ct);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Account email to {Email} could not be sent", toEmail);
            return false;
        }
    }

    private static string Body(string title, string text, string button, string link, string footer)
    {
        static string E(string s) => WebUtility.HtmlEncode(s);
        return $"""
            <div style="font-family:Roboto,Arial,sans-serif;max-width:520px;margin:0 auto;padding:32px 24px;color:#1f1f1f">
              <h1 style="font-size:22px;font-weight:400;margin:0 0 16px">{E(title)}</h1>
              <p style="font-size:14px;line-height:1.6;margin:0 0 24px;color:#444746">{E(text)}</p>
              <a href="{E(link)}" style="display:inline-block;background:#0b57d0;color:#fff;text-decoration:none;padding:10px 24px;border-radius:20px;font-size:14px;font-weight:500">{E(button)}</a>
              <p style="font-size:12px;line-height:1.6;margin:28px 0 0;color:#5f6368">{E(footer)}</p>
              <p style="font-size:12px;margin:8px 0 0;color:#5f6368;word-break:break-all">{E(link)}</p>
            </div>
            """;
    }
}
