using JetReportDesigner.Core.Model;

namespace JetReportDesigner.Api.Transfer;

// ---- export ---------------------------------------------------------------------------------------------

/// <param name="ReportIds">Reports to export.</param>
/// <param name="FolderIds">Folders to export, with every subfolder and report inside them.</param>
/// <param name="StripSampleData">Remove the sample rows stored inside JSON data sources (they can hold real data).</param>
public sealed record ExportRequest(List<Guid>? ReportIds, List<Guid>? FolderIds, bool StripSampleData = false);

public sealed record ExportPlanItem(Guid Id, string Code, string Name, string Role, string? Folder);

/// <param name="Warnings">Tokens like <c>subreportMissing|name</c> or <c>assetMissing|id</c>.</param>
public sealed record ExportPlan(
    IReadOnlyList<ExportPlanItem> Reports,
    IReadOnlyList<string> Folders,
    int Assets,
    IReadOnlyList<PackageConnectionEntry> Connections,
    int RedactedValues,
    IReadOnlyList<string> Warnings);

public sealed record ExportedPackage(byte[] Content, string FileName);

// ---- import ---------------------------------------------------------------------------------------------

public enum ImportAction
{
    /// <summary>Add a report that does not exist here yet.</summary>
    Create,

    /// <summary>Overwrite the existing report with the same code — as a new version, so it can be rolled back.</summary>
    Update,

    /// <summary>Add the package's report next to the existing one, under a new code.</summary>
    Copy,

    Skip,
}

public sealed record ImportDecision(string Code, ImportAction Action);

/// <param name="TargetFolderId">Where new reports go (their package folders are recreated beneath it). Null = top level.</param>
/// <param name="Decisions">Per-report choices; reports not listed get the default action.</param>
public sealed record ImportOptions(Guid? TargetFolderId, List<ImportDecision>? Decisions);

public sealed record ImportSource(string? Environment, DateTime ExportedAtUtc, string? ExportedBy, string? AppVersion);

/// <param name="Status">"new" | "changed" | "identical" | "invalid".</param>
/// <param name="Changes">For "changed": what differs from the report that is there now (same tokens as the version history).</param>
public sealed record ImportPlanItem(
    string Code,
    string Name,
    string Role,
    string? Folder,
    string Status,
    IReadOnlyList<string> Changes,
    string? ExistingName,
    int? ExistingVersion,
    IReadOnlyList<string> Errors,
    ImportAction DefaultAction,
    IReadOnlyList<ImportAction> AllowedActions,
    ImportAction Action);

/// <param name="Status">"found" | "missing" | "providerMismatch".</param>
public sealed record ImportConnectionCheck(string Name, SqlProvider Provider, string Status);

public sealed record ImportPlan(
    ImportSource Source,
    IReadOnlyList<ImportPlanItem> Items,
    IReadOnlyList<string> Folders,
    int AssetsNew,
    int AssetsReused,
    IReadOnlyList<ImportConnectionCheck> Connections,
    int RedactedValues,
    IReadOnlyList<string> Warnings);

public sealed record ImportResultItem(string Code, string Name, Guid? Id, ImportAction Action, int? Version);

public sealed record ImportResult(
    int Created,
    int Updated,
    int Copies,
    int Skipped,
    IReadOnlyList<ImportResultItem> Items,
    IReadOnlyList<string> Warnings);
