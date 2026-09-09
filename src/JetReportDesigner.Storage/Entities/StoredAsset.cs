namespace JetReportDesigner.Storage.Entities;

/// <summary>
/// A binary asset (currently images) uploaded from the designer and referenced by a
/// report as <c>asset:{Id}</c>. Content is addressed by <see cref="Sha256"/> so the
/// same bytes are stored once regardless of the uploaded file name.
/// </summary>
public sealed class StoredAsset
{
    public Guid Id { get; set; }

    /// <summary>Lower-case hex SHA-256 of <see cref="Content"/>. Unique — uploads de-duplicate on it.</summary>
    public string Sha256 { get; set; } = string.Empty;

    /// <summary>MIME type, e.g. <c>image/png</c>. Verified against the file's magic bytes on upload.</summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>Original file name, kept for display and download only.</summary>
    public string FileName { get; set; } = string.Empty;

    public int ByteLength { get; set; }

    public byte[] Content { get; set; } = [];

    public DateTime CreatedAtUtc { get; set; }
}
