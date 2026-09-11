namespace JetReportDesigner.Storage.Entities;

/// <summary>Which folder a report is filed under. A report with no row here is at the root
/// (unfiled) — this is a membership table, not a column on <see cref="StoredReport"/>, so it
/// works the same way regardless of which <c>IReportRepository</c> (database or filesystem)
/// actually holds the report's content.</summary>
public sealed class ReportFolderEntry
{
    /// <summary>The report's id. Primary key — a report is filed in at most one folder.</summary>
    public Guid ReportId { get; set; }

    public Guid TenantId { get; set; }

    public Guid FolderId { get; set; }
}
