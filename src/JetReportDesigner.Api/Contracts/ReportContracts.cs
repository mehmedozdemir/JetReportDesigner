using JetReportDesigner.Core.Model;
using JetReportDesigner.Storage.Repositories;

namespace JetReportDesigner.Api.Contracts;

public sealed record ReportSummaryResponse(
    Guid Id,
    string Name,
    string LayoutMode,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc)
{
    public static ReportSummaryResponse From(ReportSummary s) => new(
        s.Id,
        s.Name,
        s.LayoutMode == Core.Model.LayoutMode.Banded ? "banded" : "free",
        s.CreatedAtUtc,
        s.UpdatedAtUtc);
}

public sealed record ReportResponse(
    Guid Id,
    ReportDefinition Definition,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    Guid ConcurrencyToken)
{
    public static ReportResponse From(ReportRecord r) => new(
        r.Id, r.Definition, r.CreatedAtUtc, r.UpdatedAtUtc, r.ConcurrencyToken);
}
