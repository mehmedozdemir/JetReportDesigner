namespace JetReportDesigner.Storage.Entities;

/// <summary>A folder for organizing reports on the Start screen. Folders nest (via
/// <see cref="ParentFolderId"/>) but carry no permissions of their own — visibility follows the
/// same Designer/Viewer role every report already uses.</summary>
public sealed class Folder
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    /// <summary>Null for a top-level folder.</summary>
    public Guid? ParentFolderId { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }
}
