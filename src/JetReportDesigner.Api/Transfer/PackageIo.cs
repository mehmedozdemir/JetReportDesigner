using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using JetReportDesigner.Core.Model;
using JetReportDesigner.Core.Serialization;

namespace JetReportDesigner.Api.Transfer;

/// <summary>Reads and writes the package zip, with the limits that make reading an untrusted upload safe.</summary>
public static partial class PackageIo
{
    /// <summary>Largest package accepted (compressed).</summary>
    public const long MaxPackageBytes = 64L * 1024 * 1024;

    /// <summary>Largest total size of everything inside, uncompressed — a zip bomb stops here.</summary>
    public const long MaxUncompressedBytes = 256L * 1024 * 1024;

    public const long MaxEntryBytes = 32L * 1024 * 1024;

    public const int MaxEntries = 2_000;

    private static readonly JsonSerializerOptions Json = ReportJson.Apply(new JsonSerializerOptions { WriteIndented = true });

    // Only these entry names are ever read; anything else (".."-paths, absolute paths, odd names) is refused outright.
    [GeneratedRegex("^reports/[a-z0-9][a-z0-9_-]{0,63}\\.json$")]
    private static partial Regex ReportEntry();

    [GeneratedRegex("^assets/[0-9a-f]{64}\\.[a-z0-9]{1,5}$")]
    private static partial Regex AssetEntry();

    public static string Sha256(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    // ---- write -------------------------------------------------------------------------------------------

    public static byte[] Write(
        PackageManifest manifest,
        IReadOnlyList<string> folders,
        IReadOnlyDictionary<string, ReportDefinition> reports,
        IReadOnlyDictionary<Guid, byte[]> assets)
    {
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            var foldersBytes = JsonSerializer.SerializeToUtf8Bytes(folders, Json);
            manifest.FoldersSha256 = Sha256(foldersBytes);

            foreach (var entry in manifest.Reports)
            {
                var bytes = JsonSerializer.SerializeToUtf8Bytes(reports[entry.Code], Json);
                entry.File = $"reports/{entry.Code}.json";
                entry.Sha256 = Sha256(bytes);
                Add(zip, entry.File, bytes);
            }

            foreach (var entry in manifest.Assets)
            {
                var bytes = assets[entry.SourceId];
                entry.Sha256 = Sha256(bytes);
                entry.File = $"assets/{entry.Sha256}.{ExtensionFor(entry.ContentType)}";
                Add(zip, entry.File, bytes);
            }

            Add(zip, PackageFormat.FoldersFile, foldersBytes);
            Add(zip, PackageFormat.ManifestFile, JsonSerializer.SerializeToUtf8Bytes(manifest, Json));
        }

        return output.ToArray();
    }

    private static void Add(ZipArchive zip, string name, byte[] bytes)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var stream = entry.Open();
        stream.Write(bytes);
    }

    private static string ExtensionFor(string contentType) => contentType.ToLowerInvariant() switch
    {
        "image/png" => "png",
        "image/jpeg" => "jpg",
        "image/gif" => "gif",
        "image/webp" => "webp",
        "image/svg+xml" => "svg",
        "image/bmp" => "bmp",
        _ => "bin",
    };

    // ---- read --------------------------------------------------------------------------------------------

    /// <summary>Opens, size-checks and checksum-verifies a package. Throws <see cref="InvalidPackageException"/> for anything wrong with it.</summary>
    public static LoadedPackage Read(byte[] package)
    {
        if (package.Length == 0 || package.Length > MaxPackageBytes)
        {
            throw new InvalidPackageException("The file is empty or larger than the allowed package size.");
        }

        ZipArchive zip;
        try
        {
            zip = new ZipArchive(new MemoryStream(package), ZipArchiveMode.Read);
        }
        catch (InvalidDataException)
        {
            throw new InvalidPackageException("This is not a report package (not a valid zip file).");
        }

        using (zip)
        {
            if (zip.Entries.Count == 0 || zip.Entries.Count > MaxEntries)
            {
                throw new InvalidPackageException("The package has no content or too many files.");
            }

            long total = 0;
            var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            foreach (var entry in zip.Entries)
            {
                if (entry.FullName.EndsWith('/'))
                {
                    continue; // a directory marker
                }

                var allowed = entry.FullName is PackageFormat.ManifestFile or PackageFormat.FoldersFile
                    || ReportEntry().IsMatch(entry.FullName)
                    || AssetEntry().IsMatch(entry.FullName);
                if (!allowed)
                {
                    throw new InvalidPackageException($"The package contains an unexpected file ('{Truncate(entry.FullName)}').");
                }

                // Never trust the declared size: read with a hard cap.
                var bytes = ReadCapped(entry, Math.Min(MaxEntryBytes, MaxUncompressedBytes - total));
                total += bytes.Length;
                if (!files.TryAdd(entry.FullName, bytes))
                {
                    throw new InvalidPackageException("The package lists the same file twice.");
                }
            }

            if (!files.TryGetValue(PackageFormat.ManifestFile, out var manifestBytes))
            {
                throw new InvalidPackageException("This is not a report package (manifest.json is missing).");
            }

            PackageManifest manifest;
            try
            {
                manifest = JsonSerializer.Deserialize<PackageManifest>(manifestBytes, Json)
                    ?? throw new InvalidPackageException("The package manifest is empty.");
            }
            catch (JsonException)
            {
                throw new InvalidPackageException("The package manifest is damaged.");
            }

            if (manifest.Kind != PackageFormat.Kind)
            {
                throw new InvalidPackageException("This is not a JetReportDesigner report package.");
            }

            if (manifest.FormatVersion > PackageFormat.Version || manifest.FormatVersion < 1)
            {
                throw new InvalidPackageException(
                    $"The package format (v{manifest.FormatVersion}) is newer than this server understands (v{PackageFormat.Version}). Update the target first.");
            }

            var folders = new List<string>();
            if (files.TryGetValue(PackageFormat.FoldersFile, out var folderBytes))
            {
                Verify(PackageFormat.FoldersFile, folderBytes, manifest.FoldersSha256);
                try
                {
                    folders = JsonSerializer.Deserialize<List<string>>(folderBytes, Json) ?? [];
                }
                catch (JsonException)
                {
                    throw new InvalidPackageException("The package's folder list is damaged.");
                }
            }

            var reports = new Dictionary<string, ReportDefinition>(StringComparer.Ordinal);
            foreach (var entry in manifest.Reports)
            {
                if (!ReportCode.IsValid(entry.Code) || ReportCode.Normalize(entry.Code) != entry.Code || entry.File != $"reports/{entry.Code}.json")
                {
                    throw new InvalidPackageException($"The package lists a report with an invalid code ('{Truncate(entry.Code)}').");
                }

                var bytes = Fetch(files, entry.File, entry.Sha256);
                try
                {
                    reports[entry.Code] = ReportJson.Deserialize(Encoding.UTF8.GetString(bytes));
                }
                catch (JsonException)
                {
                    throw new InvalidPackageException($"The report '{entry.Code}' is damaged.");
                }
            }

            if (reports.Count != manifest.Reports.Count)
            {
                throw new InvalidPackageException("The package lists the same report code twice.");
            }

            var assets = new Dictionary<Guid, byte[]>();
            foreach (var entry in manifest.Assets)
            {
                if (!AssetEntry().IsMatch(entry.File))
                {
                    throw new InvalidPackageException("The package lists an invalid image file.");
                }

                assets[entry.SourceId] = Fetch(files, entry.File, entry.Sha256);
            }

            return new LoadedPackage { Manifest = manifest, Folders = folders, Reports = reports, Assets = assets };
        }
    }

    private static byte[] Fetch(Dictionary<string, byte[]> files, string name, string expectedSha)
    {
        if (!files.TryGetValue(name, out var bytes))
        {
            throw new InvalidPackageException($"The package is incomplete ('{Truncate(name)}' is missing).");
        }

        Verify(name, bytes, expectedSha);
        return bytes;
    }

    private static void Verify(string name, byte[] bytes, string expectedSha)
    {
        if (!string.Equals(Sha256(bytes), expectedSha, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidPackageException($"The package is damaged or was modified ('{Truncate(name)}' does not match its checksum).");
        }
    }

    private static byte[] ReadCapped(ZipArchiveEntry entry, long cap)
    {
        if (cap <= 0)
        {
            throw new InvalidPackageException("The package is larger than the allowed uncompressed size.");
        }

        using var stream = entry.Open();
        using var buffer = new MemoryStream();
        var chunk = new byte[81_920];
        int read;
        while ((read = stream.Read(chunk, 0, chunk.Length)) > 0)
        {
            if (buffer.Length + read > cap)
            {
                throw new InvalidPackageException("The package contains a file larger than allowed.");
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    private static string Truncate(string value) => value.Length <= 60 ? value : value[..60] + "…";
}
