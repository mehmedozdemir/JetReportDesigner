using JetReportDesigner.Storage.Email;

namespace JetReportDesigner.Api.Contracts;

/// <summary>Never carries the password.</summary>
public sealed record SmtpSettingsResponse(
    string Host,
    int Port,
    string Security,
    string Username,
    string FromEmail,
    string? FromName,
    bool HasPassword,
    DateTime UpdatedAtUtc)
{
    public static SmtpSettingsResponse From(SmtpSettingsInfo s) =>
        new(s.Host, s.Port, s.Security, s.Username, s.FromEmail, s.FromName, s.HasPassword, s.UpdatedAtUtc);
}

/// <summary>A null/blank <see cref="Password"/> keeps the previously saved one.</summary>
public sealed record SetSmtpSettingsRequest(string Host, int Port, string Security, string Username, string? Password, string FromEmail, string? FromName);

public sealed record SendTestEmailRequest(string ToEmail);
