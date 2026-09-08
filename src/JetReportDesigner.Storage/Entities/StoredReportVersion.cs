namespace JetReportDesigner.Storage.Entities;

/// <summary>An immutable snapshot of a report, written on every update.</summary>
public sealed class StoredReportVersion
{
    public Guid Id { get; set; }

    public Guid ReportId { get; set; }

    /// <summary>1-based, monotonically increasing per report.</summary>
    public int Version { get; set; }

    public string Name { get; set; } = string.Empty;

    public string DefinitionJson { get; set; } = string.Empty;

    public DateTime SavedAtUtc { get; set; }
}
