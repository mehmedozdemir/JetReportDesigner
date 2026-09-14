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

/// <summary>Fields beyond <see cref="ToEmail"/> are optional — when <see cref="Host"/> is given,
/// the test is sent with these in-progress form values instead of the saved account, so a
/// wrong setting can be caught before Save. A blank/missing <see cref="Password"/> falls back to
/// the already-saved one, same as Save's own convention.</summary>
public sealed record SendTestEmailRequest(
    string ToEmail,
    string? Host = null,
    int? Port = null,
    string? Security = null,
    string? Username = null,
    string? Password = null,
    string? FromEmail = null,
    string? FromName = null);
