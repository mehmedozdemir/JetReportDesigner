using JetReportDesigner.Storage.Jobs;

namespace JetReportDesigner.Api.Contracts;

public sealed record ReportJobResponse(
    Guid Id,
    Guid ReportId,
    string ReportName,
    string Format,
    string Status,
    string? ErrorMessage,
    DateTime CreatedAtUtc,
    DateTime? StartedAtUtc,
    DateTime? CompletedAtUtc,
    Guid? ScheduleId)
{
    public static ReportJobResponse From(ReportJobInfo j) =>
        new(j.Id, j.ReportId, j.ReportName, j.Format, j.Status, j.ErrorMessage, j.CreatedAtUtc, j.StartedAtUtc,
            j.CompletedAtUtc, j.ScheduleId);
}

/// <summary><paramref name="Total"/> is how many jobs match the filter overall, so the page
/// can show "1–25 / 132" and know whether there is a next page.</summary>
public sealed record ReportJobPageResponse(IReadOnlyList<ReportJobResponse> Items, int Total);

public sealed record EnqueueReportJobRequest(string Format);
