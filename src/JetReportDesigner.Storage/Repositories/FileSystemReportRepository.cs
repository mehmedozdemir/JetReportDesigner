using System.Text.Json;
using System.Text.Json.Serialization;
using JetReportDesigner.Core.Model;
using JetReportDesigner.Core.Serialization;
using JetReportDesigner.Storage.Tenancy;

namespace JetReportDesigner.Storage.Repositories;

/// <summary>
/// Stores reports as JSON files under a per-tenant subdirectory:
/// <c>{root}/{tenantId}/{id}.json</c> for the current definition (with metadata) and
/// <c>{root}/{tenantId}/{id}.versions/v{n}.json</c> for snapshots. For "reports as files"
/// workflows; registered connections still live in the database.
/// </summary>
internal sealed class FileSystemReportRepository : IReportRepository
{
    private readonly string _rootBase;
    private readonly ICurrentTenant _tenant;
    private readonly TimeProvider _clock;
    private readonly JsonSerializerOptions _json;

    public FileSystemReportRepository(string root, TimeProvider clock, ICurrentTenant tenant)
    {
        _rootBase = root;
        _tenant = tenant;
        _clock = clock;
        _json = ReportJson.Apply(new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>The current signed-in tenant's directory. Lazy — <see cref="ICurrentTenant.TenantId"/>
    /// throws for an anonymous caller (the share/render path), which must go through
    /// <see cref="GetForTenantAsync"/> instead and never touch this.</summary>
    private string Root
    {
        get
        {
            var dir = RootFor(_tenant.TenantId);
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    private string RootFor(Guid tenantId) => Path.Combine(_rootBase, tenantId.ToString());

    private sealed record Envelope(
        [property: JsonPropertyName("definition")] ReportDefinition Definition,
        [property: JsonPropertyName("createdAtUtc")] DateTime CreatedAtUtc,
        [property: JsonPropertyName("updatedAtUtc")] DateTime UpdatedAtUtc,
        [property: JsonPropertyName("concurrencyToken")] Guid ConcurrencyToken,
        [property: JsonPropertyName("createdByEmail")] string? CreatedByEmail = null);

    private sealed record VersionFile(
        [property: JsonPropertyName("version")] int Version,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("savedAtUtc")] DateTime SavedAtUtc,
        [property: JsonPropertyName("definition")] ReportDefinition Definition);

    private string ReportPath(Guid id) => Path.Combine(Root, $"{id}.json");

    private string VersionsDir(Guid id) => Path.Combine(Root, $"{id}.versions");

    public async Task<IReadOnlyList<ReportSummary>> ListAsync(CancellationToken cancellationToken)
    {
        var list = new List<ReportSummary>();
        foreach (var file in Directory.EnumerateFiles(Root, "*.json"))
        {
            var envelope = await ReadAsync<Envelope>(file, cancellationToken);
            if (envelope is not null)
            {
                list.Add(new ReportSummary(
                    envelope.Definition.Id,
                    envelope.Definition.Name,
                    envelope.Definition.LayoutMode,
                    envelope.CreatedAtUtc,
                    envelope.UpdatedAtUtc,
                    envelope.CreatedByEmail));
            }
        }

        return list.OrderByDescending(r => r.UpdatedAtUtc).ToList();
    }

    public async Task<ReportRecord?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var envelope = await ReadAsync<Envelope>(ReportPath(id), cancellationToken);
        return envelope is null
            ? null
            : new ReportRecord(id, envelope.Definition, envelope.CreatedAtUtc, envelope.UpdatedAtUtc, envelope.ConcurrencyToken, envelope.CreatedByEmail);
    }

    public async Task<ReportRecord?> GetForTenantAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
    {
        var path = Path.Combine(RootFor(tenantId), $"{id}.json");
        var envelope = await ReadAsync<Envelope>(path, cancellationToken);
        return envelope is null
            ? null
            : new ReportRecord(id, envelope.Definition, envelope.CreatedAtUtc, envelope.UpdatedAtUtc, envelope.ConcurrencyToken, envelope.CreatedByEmail);
    }

    public async Task<ReportRecord> CreateAsync(
        ReportDefinition definition,
        CancellationToken cancellationToken,
        Guid? createdByUserId = null,
        string? createdByEmail = null)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var id = definition.Id == Guid.Empty ? Guid.NewGuid() : definition.Id;
        definition.Id = id;

        var envelope = new Envelope(definition, now, now, Guid.NewGuid(), createdByEmail);
        await WriteAsync(ReportPath(id), envelope, cancellationToken);
        await WriteVersionAsync(id, 1, definition, now, cancellationToken);

        return new ReportRecord(id, definition, now, now, envelope.ConcurrencyToken, createdByEmail);
    }

    public async Task<ReportRecord?> UpdateAsync(
        Guid id,
        ReportDefinition definition,
        Guid? expectedToken,
        CancellationToken cancellationToken)
    {
        var existing = await ReadAsync<Envelope>(ReportPath(id), cancellationToken);
        if (existing is null)
        {
            return null;
        }

        if (expectedToken is { } token && existing.ConcurrencyToken != token)
        {
            throw new ReportConcurrencyException(id);
        }

        var now = _clock.GetUtcNow().UtcDateTime;
        definition.Id = id;
        var envelope = new Envelope(definition, existing.CreatedAtUtc, now, Guid.NewGuid(), existing.CreatedByEmail);
        await WriteAsync(ReportPath(id), envelope, cancellationToken);

        var nextVersion = NextVersionNumber(id);
        await WriteVersionAsync(id, nextVersion, definition, now, cancellationToken);

        return new ReportRecord(id, definition, existing.CreatedAtUtc, now, envelope.ConcurrencyToken, existing.CreatedByEmail);
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var path = ReportPath(id);
        if (!File.Exists(path))
        {
            return Task.FromResult(false);
        }

        File.Delete(path);
        if (Directory.Exists(VersionsDir(id)))
        {
            Directory.Delete(VersionsDir(id), recursive: true);
        }

        return Task.FromResult(true);
    }

    public Task<IReadOnlyList<ReportVersionInfo>> ListVersionsAsync(Guid id, CancellationToken cancellationToken)
    {
        var dir = VersionsDir(id);
        if (!Directory.Exists(dir))
        {
            return Task.FromResult<IReadOnlyList<ReportVersionInfo>>([]);
        }

        var infos = new List<ReportVersionInfo>();
        foreach (var file in Directory.EnumerateFiles(dir, "v*.json"))
        {
            var v = JsonSerializer.Deserialize<VersionFile>(File.ReadAllText(file), _json);
            if (v is not null)
            {
                infos.Add(new ReportVersionInfo(v.Version, v.Name, v.SavedAtUtc));
            }
        }

        return Task.FromResult<IReadOnlyList<ReportVersionInfo>>(infos.OrderByDescending(i => i.Version).ToList());
    }

    public async Task<ReportVersionRecord?> GetVersionAsync(Guid id, int version, CancellationToken cancellationToken)
    {
        var path = Path.Combine(VersionsDir(id), $"v{version}.json");
        var v = await ReadAsync<VersionFile>(path, cancellationToken);
        return v is null ? null : new ReportVersionRecord(v.Version, v.Name, v.SavedAtUtc, v.Definition);
    }

    public async Task<ReportRecord?> RestoreVersionAsync(Guid id, int version, CancellationToken cancellationToken)
    {
        var snapshot = await GetVersionAsync(id, version, cancellationToken);
        return snapshot is null
            ? null
            : await UpdateAsync(id, snapshot.Definition, expectedToken: null, cancellationToken);
    }

    private int NextVersionNumber(Guid id)
    {
        var dir = VersionsDir(id);
        if (!Directory.Exists(dir))
        {
            return 1;
        }

        var max = Directory.EnumerateFiles(dir, "v*.json")
            .Select(f => int.TryParse(Path.GetFileNameWithoutExtension(f)[1..], out var n) ? n : 0)
            .DefaultIfEmpty(0)
            .Max();
        return max + 1;
    }

    private async Task WriteVersionAsync(Guid id, int version, ReportDefinition definition, DateTime savedAt, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(VersionsDir(id));
        var file = Path.Combine(VersionsDir(id), $"v{version}.json");
        await WriteAsync(file, new VersionFile(version, definition.Name, savedAt, definition), cancellationToken);
    }

    private async Task<T?> ReadAsync<T>(string path, CancellationToken cancellationToken) where T : class
    {
        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<T>(stream, _json, cancellationToken);
    }

    private async Task WriteAsync<T>(string path, T value, CancellationToken cancellationToken)
    {
        var temp = path + ".tmp";
        await using (var stream = File.Create(temp))
        {
            await JsonSerializer.SerializeAsync(stream, value, _json, cancellationToken);
        }

        File.Move(temp, path, overwrite: true);
    }
}
