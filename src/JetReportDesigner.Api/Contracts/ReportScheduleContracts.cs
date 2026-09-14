using JetReportDesigner.Storage.Schedules;

namespace JetReportDesigner.Api.Contracts;

public sealed record ReportScheduleResponse(
    Guid Id,
    Guid ReportId,
    string ReportName,
    string Format,
    string Frequency,
    int MinuteOfDayUtc,
    int? DayOfWeek,
    int? DayOfMonth,
    bool Enabled,
    bool CreateShareLink,
    string? EmailRecipients,
    DateTime CreatedAtUtc,
    DateTime NextRunAtUtc,
    DateTime? LastRunAtUtc,
    Guid? LastJobId)
{
    public static ReportScheduleResponse From(ReportScheduleInfo s) => new(
        s.Id, s.ReportId, s.ReportName, s.Format, s.Frequency, s.MinuteOfDayUtc, s.DayOfWeek, s.DayOfMonth,
        s.Enabled, s.CreateShareLink, s.EmailRecipients, s.CreatedAtUtc, s.NextRunAtUtc, s.LastRunAtUtc, s.LastJobId);
}

public sealed record ReportScheduleRequest(
    string Format,
    string Frequency,
    int MinuteOfDayUtc,
    int? DayOfWeek,
    int? DayOfMonth,
    bool Enabled,
    bool CreateShareLink,
    string? EmailRecipients)
{
    public ReportScheduleFields ToFields() => new(
        Format.Equals("xlsx", StringComparison.OrdinalIgnoreCase) ? "xlsx" : "pdf",
        Frequency is "Weekly" or "Monthly" ? Frequency : "Daily",
        Math.Clamp(MinuteOfDayUtc, 0, 1439),
        DayOfWeek is >= 0 and <= 6 ? DayOfWeek : null,
        DayOfMonth is >= 1 and <= 31 ? DayOfMonth : null,
        Enabled,
        CreateShareLink,
        string.IsNullOrWhiteSpace(EmailRecipients) ? null : EmailRecipients.Trim());
}
