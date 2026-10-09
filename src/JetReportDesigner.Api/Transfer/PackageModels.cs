using JetReportDesigner.Core.Model;

namespace JetReportDesigner.Api.Transfer;

/// <summary>
/// The report package ("*.jrdpkg") is a zip with a fixed layout:
/// <code>
/// manifest.json                what is inside, where it came from, and a SHA-256 of every other file
/// folders.json                 folder paths to recreate (including empty ones)
/// reports/{code}.json          one report definition each
/// assets/{sha256}.{ext}        images the reports use
/// </code>
/// It never contains secrets: database connections travel as a name + provider only, and sensitive REST
/// header/query values are removed on export.
/// </summary>
public static class PackageFormat
{
    public const string Kind = "jetreportdesigner.package";

    /// <summary>Bumped when the layout changes incompatibly. An importer refuses a package newer than it understands.</summary>
    public const int Version = 1;

    public const string Extension = ".jrdpkg";

    public const string ManifestFile = "manifest.json";
    public const string FoldersFile = "folders.json";
}

public sealed class PackageManifest
{
    public string Kind { get; set; } = PackageFormat.Kind;

    public int FormatVersion { get; set; } = PackageFormat.Version;

    public DateTime ExportedAtUtc { get; set; }

    public string? ExportedBy { get; set; }

    /// <summary>Free-text name of the environment it was exported from ("UAT"), from <c>Transfer:EnvironmentName</c>.</summary>
    public string? SourceEnvironment { get; set; }

    public string? AppVersion { get; set; }

    public List<PackageReportEntry> Reports { get; set; } = [];

    public List<PackageAssetEntry> Assets { get; set; } = [];

    /// <summary>Database connections the reports use, by name only. The target must have connections with these names.</summary>
    public List<PackageConnectionEntry> Connections { get; set; } = [];

    /// <summary>SHA-256 (lower-case hex) of the folders file.</summary>
    public string FoldersSha256 { get; set; } = string.Empty;

    /// <summary>How many sensitive header/query values were removed from the reports on export.</summary>
    public int RedactedValues { get; set; }
}

public sealed class PackageReportEntry
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>The report's id in the source environment — only used to rewrite references between the packaged reports.</summary>
    public Guid SourceId { get; set; }

    /// <summary>"selected" (asked for) or "dependency" (pulled in because a selected report embeds it as a subreport).</summary>
    public string Role { get; set; } = "selected";

    /// <summary>Folder path inside the package ("Finans/Aylık"), or null for the top level.</summary>
    public string? Folder { get; set; }

    public string File { get; set; } = string.Empty;

    public string Sha256 { get; set; } = string.Empty;
}

public sealed class PackageAssetEntry
{
    public Guid SourceId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public string File { get; set; } = string.Empty;

    public string Sha256 { get; set; } = string.Empty;
}

public sealed class PackageConnectionEntry
{
    public string Name { get; set; } = string.Empty;

    public SqlProvider Provider { get; set; }
}

/// <summary>A package as read from disk/upload: parsed and checksum-verified, nothing applied yet.</summary>
public sealed class LoadedPackage
{
    public required PackageManifest Manifest { get; init; }

    public required List<string> Folders { get; init; }

    /// <summary>Report code → its definition.</summary>
    public required Dictionary<string, ReportDefinition> Reports { get; init; }

    /// <summary>Source asset id → bytes.</summary>
    public required Dictionary<Guid, byte[]> Assets { get; init; }
}

/// <summary>The package can't be read: not a package, damaged, too large, or from a newer version.</summary>
public sealed class InvalidPackageException(string reason) : Exception(reason);
