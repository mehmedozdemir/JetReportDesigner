using JetReportDesigner.Core.Model;
using JetReportDesigner.Storage.Repositories;
using JetReportDesigner.Storage.Tenancy;

namespace JetReportDesigner.Storage.Tests;

internal sealed class FixedTenant(Guid tenantId) : ICurrentTenant
{
    public Guid TenantId { get; } = tenantId;
}

public sealed class FileSystemReportRepositoryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "jrd-fs-" + Guid.NewGuid().ToString("N"));
    private readonly FileSystemReportRepository _repo;

    public FileSystemReportRepositoryTests() => _repo = new FileSystemReportRepository(_root, TimeProvider.System, new FixedTenant(Guid.NewGuid()));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static ReportDefinition Report(string name) => new()
    {
        Name = name,
        LayoutMode = LayoutMode.Free,
        Body = new ReportBody { Height = 800, Elements = [] },
    };

    [Fact]
    public async Task History_records_who_and_what_and_skips_saves_that_change_nothing()
    {
        var created = await _repo.CreateAsync(Report("Invoice"), CancellationToken.None, createdByEmail: "a@x.com");

        // Saving the same thing again must not add a version.
        await _repo.UpdateAsync(created.Id, Report("Invoice"), null, CancellationToken.None, "b@x.com");
        Assert.Single(await _repo.ListVersionsAsync(created.Id, CancellationToken.None));

        var edited = Report("Invoice");
        edited.Body!.Elements.Add(new ReportElement { Id = "x", Type = ElementType.Label, Text = "hi" });
        await _repo.UpdateAsync(created.Id, edited, null, CancellationToken.None, "b@x.com");

        var versions = await _repo.ListVersionsAsync(created.Id, CancellationToken.None);
        Assert.Equal([2, 1], versions.Select(v => v.Version));
        Assert.Equal("b@x.com", versions[0].SavedByEmail);
        Assert.Equal(["added:1"], versions[0].Changes);
        Assert.Equal("a@x.com", versions[1].SavedByEmail);
    }

    [Fact]
    public async Task Restoring_an_old_version_makes_a_new_one_and_keeps_the_history()
    {
        var created = await _repo.CreateAsync(Report("Invoice"), CancellationToken.None);
        var edited = Report("Invoice");
        edited.Body!.Elements.Add(new ReportElement { Id = "x", Type = ElementType.Label, Text = "oops" });
        await _repo.UpdateAsync(created.Id, edited, null, CancellationToken.None);

        var restored = await _repo.RestoreVersionAsync(created.Id, 1, CancellationToken.None, "boss@x.com");

        Assert.Empty(restored!.Definition.Body!.Elements);
        var versions = await _repo.ListVersionsAsync(created.Id, CancellationToken.None);
        Assert.Equal([3, 2, 1], versions.Select(v => v.Version));
        Assert.Equal(1, versions[0].RestoredFromVersion);
        Assert.Equal("boss@x.com", versions[0].SavedByEmail);
        // The version that was current before the restore is still there, so the restore itself can be undone.
        var oops = await _repo.GetVersionAsync(created.Id, 2, CancellationToken.None);
        Assert.Single(oops!.Definition.Body!.Elements);
    }

    [Fact]
    public async Task Crud_And_Listing_RoundTrip()
    {
        var created = await _repo.CreateAsync(Report("Invoice"), CancellationToken.None);
        Assert.NotEqual(Guid.Empty, created.Id);

        var fetched = await _repo.GetAsync(created.Id, CancellationToken.None);
        Assert.Equal("Invoice", fetched!.Definition.Name);
        Assert.Equal(created.ConcurrencyToken, fetched.ConcurrencyToken);

        var list = await _repo.ListAsync(CancellationToken.None);
        Assert.Contains(list, r => r.Id == created.Id && r.Name == "Invoice");

        Assert.True(await _repo.DeleteAsync(created.Id, CancellationToken.None));
        Assert.Null(await _repo.GetAsync(created.Id, CancellationToken.None));
        Assert.False(await _repo.DeleteAsync(created.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Concurrency_Token_Is_Enforced()
    {
        var created = await _repo.CreateAsync(Report("A"), CancellationToken.None);
        var stale = created.ConcurrencyToken;

        var updated = await _repo.UpdateAsync(created.Id, Report("B"), stale, CancellationToken.None);
        Assert.Equal("B", updated!.Definition.Name);

        await Assert.ThrowsAsync<ReportConcurrencyException>(() =>
            _repo.UpdateAsync(created.Id, Report("C"), stale, CancellationToken.None));
    }

    [Fact]
    public async Task Versions_Are_Written_And_Restorable()
    {
        var created = await _repo.CreateAsync(Report("v1"), CancellationToken.None);
        await _repo.UpdateAsync(created.Id, Report("v2"), null, CancellationToken.None);
        await _repo.UpdateAsync(created.Id, Report("v3"), null, CancellationToken.None);

        var versions = await _repo.ListVersionsAsync(created.Id, CancellationToken.None);
        Assert.Equal([3, 2, 1], versions.Select(v => v.Version).ToArray());
        Assert.Equal("v1", versions.Single(v => v.Version == 1).Name);

        var restored = await _repo.RestoreVersionAsync(created.Id, 1, CancellationToken.None);
        Assert.Equal("v1", restored!.Definition.Name);
        Assert.Equal(4, (await _repo.ListVersionsAsync(created.Id, CancellationToken.None)).Count);
    }
}
