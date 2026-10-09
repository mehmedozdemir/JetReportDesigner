using System.IO.Compression;
using System.Text;
using System.Text.Json;
using JetReportDesigner.Api.Transfer;
using JetReportDesigner.Core.Model;

namespace JetReportDesigner.Api.Tests;

/// <summary>Reading a package is reading an upload: these are the ways it must refuse to be fooled.</summary>
public class PackageIoTests
{
    private static byte[] Zip(params (string Name, byte[] Bytes)[] files)
    {
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, bytes) in files)
            {
                using var s = zip.CreateEntry(name).Open();
                s.Write(bytes);
            }
        }

        return output.ToArray();
    }

    private static byte[] Manifest(Action<PackageManifest>? tweak = null)
    {
        var manifest = new PackageManifest { ExportedAtUtc = DateTime.UtcNow };
        tweak?.Invoke(manifest);
        return JsonSerializer.SerializeToUtf8Bytes(manifest, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    private static string Reason(byte[] package) => Assert.Throws<InvalidPackageException>(() => PackageIo.Read(package)).Message;

    [Fact]
    public void A_package_round_trips_with_its_checksums()
    {
        var report = new ReportDefinition { Name = "R", Code = "r", LayoutMode = LayoutMode.Free, Body = new ReportBody { Elements = [] } };
        var manifest = new PackageManifest
        {
            ExportedAtUtc = DateTime.UtcNow,
            Reports = [new PackageReportEntry { Code = "r", Name = "R", SourceId = Guid.NewGuid() }],
        };

        var bytes = PackageIo.Write(manifest, ["A/B"], new Dictionary<string, ReportDefinition> { ["r"] = report }, new Dictionary<Guid, byte[]>());
        var loaded = PackageIo.Read(bytes);

        Assert.Equal("R", loaded.Reports["r"].Name);
        Assert.Equal(["A/B"], loaded.Folders);
    }

    [Theory]
    [InlineData("../evil.json")]
    [InlineData("/etc/passwd")]
    [InlineData("reports/../../x.json")]
    [InlineData("reports/UPPER.json")]
    [InlineData("script.sh")]
    public void Files_outside_the_layout_are_refused(string name) =>
        Assert.Contains("unexpected file", Reason(Zip((PackageFormat.ManifestFile, Manifest()), (name, [1]))), StringComparison.Ordinal);

    [Fact]
    public void Something_that_is_not_a_zip_or_not_ours_is_refused()
    {
        Assert.Contains("not a valid zip", Reason(Encoding.UTF8.GetBytes("hello")), StringComparison.Ordinal);
        Assert.Contains("manifest.json is missing", Reason(Zip(("folders.json", [91, 93]))), StringComparison.Ordinal);
        Assert.Contains("not a JetReportDesigner", Reason(Zip((PackageFormat.ManifestFile, Manifest(m => m.Kind = "other")))), StringComparison.Ordinal);
    }

    [Fact]
    public void A_package_from_a_newer_format_asks_for_an_update()
    {
        var reason = Reason(Zip((PackageFormat.ManifestFile, Manifest(m => m.FormatVersion = PackageFormat.Version + 1))));

        Assert.Contains("newer than this server", reason, StringComparison.Ordinal);
    }

    [Fact]
    public void A_file_that_inflates_beyond_the_limit_is_stopped_while_reading()
    {
        // Tiny on the wire, huge when read: the declared size is never trusted.
        var bomb = new byte[(int)PackageIo.MaxEntryBytes + 1024];
        var package = Zip((PackageFormat.ManifestFile, Manifest()), ("assets/" + new string('a', 64) + ".png", bomb));

        Assert.True(package.Length < 200_000);
        Assert.Contains("larger than allowed", Reason(package), StringComparison.Ordinal);
    }

    [Fact]
    public void A_missing_or_altered_report_file_is_detected()
    {
        var manifest = Manifest(m => m.Reports = [new PackageReportEntry { Code = "r", Name = "R", File = "reports/r.json", Sha256 = new string('0', 64) }]);

        Assert.Contains("incomplete", Reason(Zip((PackageFormat.ManifestFile, manifest))), StringComparison.Ordinal);
        Assert.Contains("checksum", Reason(Zip((PackageFormat.ManifestFile, manifest), ("reports/r.json", Encoding.UTF8.GetBytes("{}")))), StringComparison.Ordinal);
    }
}
