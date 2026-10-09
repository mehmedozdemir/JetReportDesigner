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

    /// <summary>Who saved this version. Null for versions saved before this was recorded.</summary>
    public string? SavedByEmail { get; set; }

    /// <summary>Comma-separated tokens saying what changed from the previous version (see <c>ReportChanges</c>). Null when unknown.</summary>
    public string? Changes { get; set; }

    /// <summary>Set when this version was made by restoring an older one: that older version's number.</summary>
    public int? RestoredFromVersion { get; set; }
}
