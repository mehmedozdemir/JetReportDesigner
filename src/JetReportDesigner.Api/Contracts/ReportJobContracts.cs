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
    DateTime? CompletedAtUtc)
{
    public static ReportJobResponse From(ReportJobInfo j) =>
        new(j.Id, j.ReportId, j.ReportName, j.Format, j.Status, j.ErrorMessage, j.CreatedAtUtc, j.StartedAtUtc, j.CompletedAtUtc);
}

public sealed record EnqueueReportJobRequest(string Format);
