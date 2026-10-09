using JetReportDesigner.Core.Model;
using JetReportDesigner.Storage.Repositories;

namespace JetReportDesigner.Api.Contracts;

public sealed record ReportSummaryResponse(
    Guid Id,
    string Name,
    string LayoutMode,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    Guid? FolderId,
    string? CreatedByEmail,
    string? Code)
{
    public static ReportSummaryResponse From(ReportSummary s, Guid? folderId = null) => new(
        s.Id,
        s.Name,
        s.LayoutMode == Core.Model.LayoutMode.Banded ? "banded" : "free",
        s.CreatedAtUtc,
        s.UpdatedAtUtc,
        folderId,
        s.CreatedByEmail,
        s.Code);
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

public sealed record ReportIssueResponse(string Severity, string Message, string? ElementId)
{
    public static ReportIssueResponse From(JetReportDesigner.Core.Validation.ReportIssue i) =>
        new(i.Severity.ToString().ToLowerInvariant(), i.Message, i.ElementId);
}

/// <param name="Changes">What this save changed, as tokens (see <c>ReportChanges</c>); empty for the first version or unknown.</param>
/// <param name="RestoredFromVersion">Set when this version was made by restoring an older one.</param>
public sealed record ReportVersionResponse(
    int Version,
    string Name,
    DateTime SavedAtUtc,
    string? SavedByEmail,
    IReadOnlyList<string> Changes,
    int? RestoredFromVersion)
{
    public static ReportVersionResponse From(ReportVersionInfo v) => new(
        v.Version,
        v.Name,
        DateTime.SpecifyKind(v.SavedAtUtc, DateTimeKind.Utc),
        v.SavedByEmail,
        v.Changes ?? [],
        v.RestoredFromVersion);
}

public sealed record ReportVersionDetailResponse(
    int Version,
    string Name,
    DateTime SavedAtUtc,
    ReportDefinition Definition,
    string? SavedByEmail,
    IReadOnlyList<string> Changes,
    int? RestoredFromVersion)
{
    public static ReportVersionDetailResponse From(ReportVersionRecord v) => new(
        v.Version,
        v.Name,
        DateTime.SpecifyKind(v.SavedAtUtc, DateTimeKind.Utc),
        v.Definition,
        v.SavedByEmail,
        v.Changes ?? [],
        v.RestoredFromVersion);
}
