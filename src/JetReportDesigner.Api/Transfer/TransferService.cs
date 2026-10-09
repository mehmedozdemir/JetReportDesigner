using System.Reflection;
using System.Text.RegularExpressions;
using FluentValidation;
using JetReportDesigner.Core.Model;
using JetReportDesigner.Core.Serialization;
using JetReportDesigner.Storage;
using JetReportDesigner.Storage.Assets;
using JetReportDesigner.Storage.Connections;
using JetReportDesigner.Storage.Folders;
using JetReportDesigner.Storage.Repositories;
using Microsoft.EntityFrameworkCore;

namespace JetReportDesigner.Api.Transfer;

/// <summary>
/// Moves reports between environments (UAT → production) as a package. Export collects what the chosen
/// reports/folders need — subreports, images, folder structure — and import checks everything first
/// (<see cref="PreviewAsync"/>) before changing anything (<see cref="ApplyAsync"/>). Matching is by report
/// code, never by id, because ids differ between environments. Overwriting a report saves a new version,
/// so an import can always be rolled back from the version history.
/// </summary>
public sealed partial class TransferService(
    IReportRepository reports,
    IFolderRepository folders,
    IAssetRepository assets,
    IConnectionRepository connections,
    IValidator<ReportDefinition> validator,
    JetReportDbContext db,
    IConfiguration configuration,
    TimeProvider clock,
    ILogger<TransferService> logger)
{
    public const string ImportedOrigin = "imported";

    [GeneratedRegex("asset:([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})")]
    private static partial Regex AssetRef();

    [GeneratedRegex("(?i)authorization|api[-_ ]?key|token|secret|passw|cookie|credential|bearer")]
    private static partial Regex SensitiveName();

    [GeneratedRegex("(?i)([?&][^=&]*(?:key|token|secret|passw|auth)[^=&]*=)[^&]+")]
    private static partial Regex SensitiveUrlQuery();

    [GeneratedRegex("(?<=://)[^/@\\s]+@")]
    private static partial Regex UrlUserInfo();

    // =====================================================================================================
    // Export
    // =====================================================================================================

    private sealed class Gathered
    {
        public List<(PackageReportEntry Entry, ReportDefinition Definition)> Reports { get; } = [];

        public List<string> Folders { get; } = [];

        public Dictionary<Guid, (string FileName, string ContentType, byte[] Bytes)> Assets { get; } = [];

        public List<PackageConnectionEntry> Connections { get; } = [];

        public int Redacted { get; set; }

        public List<string> Warnings { get; } = [];
    }

    public async Task<ExportPlan> PlanExportAsync(ExportRequest request, CancellationToken ct)
    {
        var g = await GatherAsync(request, ct);
        return new ExportPlan(
            g.Reports.Select(r => new ExportPlanItem(r.Entry.SourceId, r.Entry.Code, r.Entry.Name, r.Entry.Role, r.Entry.Folder)).ToList(),
            g.Folders,
            g.Assets.Count,
            g.Connections,
            g.Redacted,
            g.Warnings);
    }

    public async Task<ExportedPackage> ExportAsync(ExportRequest request, string? exportedBy, CancellationToken ct)
    {
        var g = await GatherAsync(request, ct);
        if (g.Reports.Count == 0)
        {
            throw new InvalidPackageException("Nothing to export: select at least one report or folder.");
        }

        var manifest = new PackageManifest
        {
            ExportedAtUtc = clock.GetUtcNow().UtcDateTime,
            ExportedBy = exportedBy,
            SourceEnvironment = configuration["Transfer:EnvironmentName"],
            AppVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString(),
            Reports = g.Reports.Select(r => r.Entry).ToList(),
            Connections = g.Connections,
            RedactedValues = g.Redacted,
            Assets = g.Assets
                .Select(a => new PackageAssetEntry { SourceId = a.Key, FileName = a.Value.FileName, ContentType = a.Value.ContentType })
                .ToList(),
        };

        var bytes = PackageIo.Write(
            manifest,
            g.Folders,
            g.Reports.ToDictionary(r => r.Entry.Code, r => r.Definition, StringComparer.Ordinal),
            g.Assets.ToDictionary(a => a.Key, a => a.Value.Bytes));

        var single = g.Reports.Count == 1 ? g.Reports[0].Entry.Code : "reports";
        return new ExportedPackage(bytes, $"{single}-{clock.GetUtcNow():yyyyMMdd-HHmm}{PackageFormat.Extension}");
    }

    private async Task<Gathered> GatherAsync(ExportRequest request, CancellationToken ct)
    {
        var g = new Gathered();

        var allFolders = (await folders.ListAsync(ct)).ToDictionary(f => f.Id);
        var folderOf = await folders.GetReportFolderMapAsync(ct);
        var summaries = (await reports.ListAsync(ct)).ToDictionary(r => r.Id);

        string PathFrom(Guid folderId, Guid rootId)
        {
            var parts = new Stack<string>();
            for (Guid? cursor = folderId; cursor is { } id;)
            {
                var f = allFolders[id];
                parts.Push(f.Name);
                cursor = id == rootId ? null : f.ParentFolderId;
            }

            return string.Join('/', parts);
        }

        // id -> (role, folder path)
        var wanted = new Dictionary<Guid, (string Role, string? Folder)>();
        var folderPaths = new List<string>();

        foreach (var rootId in (request.FolderIds ?? []).Where(allFolders.ContainsKey).Distinct())
        {
            var stack = new Stack<Guid>([rootId]);
            while (stack.Count > 0)
            {
                var id = stack.Pop();
                var path = PathFrom(id, rootId);
                folderPaths.Add(path);
                foreach (var (reportId, folderId) in folderOf.Where(kv => kv.Value == id))
                {
                    if (summaries.ContainsKey(reportId))
                    {
                        wanted[reportId] = ("selected", path);
                    }
                }

                foreach (var child in allFolders.Values.Where(f => f.ParentFolderId == id))
                {
                    stack.Push(child.Id);
                }
            }
        }

        foreach (var id in (request.ReportIds ?? []).Distinct())
        {
            if (summaries.ContainsKey(id) && !wanted.ContainsKey(id))
            {
                wanted[id] = ("selected", null);
            }
        }

        // Pull in every report embedded as a subreport, however deep, so the package runs on its own.
        var definitions = new Dictionary<Guid, ReportDefinition>();
        var queue = new Queue<Guid>(wanted.Keys);
        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            if (definitions.ContainsKey(id))
            {
                continue;
            }

            var record = await reports.GetAsync(id, ct);
            if (record is null)
            {
                continue;
            }

            definitions[id] = record.Definition;
            foreach (var element in AllElements(record.Definition).Where(e => e.Subreport is not null))
            {
                if (!Guid.TryParse(element.Subreport!.ReportId, out var child))
                {
                    continue;
                }

                if (!summaries.ContainsKey(child))
                {
                    g.Warnings.Add($"subreportMissing|{record.Definition.Name}");
                    continue;
                }

                if (!wanted.ContainsKey(child))
                {
                    wanted[child] = ("dependency", null);
                }

                queue.Enqueue(child);
            }
        }

        // Codes must be unique inside the package (legacy file-store reports can lack one).
        var usedCodes = new HashSet<string>(StringComparer.Ordinal);
        var assetNames = (await assets.ListAsync(ct)).ToDictionary(a => a.Id);
        var connectionNames = new Dictionary<string, PackageConnectionEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (var (id, (role, folder)) in wanted.OrderBy(kv => kv.Value.Role == "selected" ? 0 : 1).ThenBy(kv => summaries[kv.Key].Name, StringComparer.OrdinalIgnoreCase))
        {
            if (!definitions.TryGetValue(id, out var original))
            {
                continue;
            }

            var definition = ReportJson.Deserialize(ReportJson.Serialize(original));
            var code = string.IsNullOrWhiteSpace(definition.Code)
                ? ReportCode.MakeUnique(ReportCode.FromName(definition.Name), usedCodes.Contains)
                : ReportCode.Normalize(definition.Code);
            usedCodes.Add(code);
            definition.Code = code;
            definition.Id = Guid.Empty;

            g.Redacted += Redact(definition);
            if (request.StripSampleData)
            {
                foreach (var source in definition.DataSources.Where(s => s.Json is not null))
                {
                    source.Json!.InlineData = "[]";
                }
            }

            foreach (var connection in definition.Connections)
            {
                connectionNames.TryAdd(connection.Name, new PackageConnectionEntry { Name = connection.Name, Provider = connection.Provider });
            }

            foreach (Match match in AssetRef().Matches(ReportJson.Serialize(definition)))
            {
                var assetId = Guid.Parse(match.Groups[1].Value);
                if (g.Assets.ContainsKey(assetId))
                {
                    continue;
                }

                var content = await assets.GetContentAsync(assetId, ct);
                if (content is null)
                {
                    g.Warnings.Add($"assetMissing|{definition.Name}");
                    continue;
                }

                assetNames.TryGetValue(assetId, out var meta);
                g.Assets[assetId] = (meta?.FileName ?? assetId.ToString("N"), content.ContentType, content.Bytes);
            }

            g.Reports.Add((
                new PackageReportEntry { Code = code, Name = definition.Name, SourceId = id, Role = role, Folder = folder },
                definition));
        }

        g.Folders.AddRange(folderPaths.Distinct(StringComparer.Ordinal).OrderBy(p => p, StringComparer.Ordinal));
        g.Connections.AddRange(connectionNames.Values.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase));
        var unique = g.Warnings.Distinct().ToList();
        g.Warnings.Clear();
        g.Warnings.AddRange(unique);
        return g;
    }

    /// <summary>Blanks values that look like credentials (Authorization, api keys, tokens, passwords…) in REST sources. Returns how many.</summary>
    private static int Redact(ReportDefinition definition)
    {
        var count = 0;
        foreach (var rest in definition.DataSources.Select(s => s.Rest).Where(r => r is not null))
        {
            foreach (var map in new[] { rest!.Headers, rest.Query })
            {
                foreach (var key in map.Keys.ToList())
                {
                    if (SensitiveName().IsMatch(key) && !string.IsNullOrEmpty(map[key]))
                    {
                        map[key] = string.Empty;
                        count++;
                    }
                }
            }

            var url = rest!.Url;
            var cleaned = UrlUserInfo().Replace(SensitiveUrlQuery().Replace(url, "$1"), string.Empty);
            if (!string.Equals(url, cleaned, StringComparison.Ordinal))
            {
                rest.Url = cleaned;
                count++;
            }
        }

        return count;
    }

    private static IEnumerable<ReportElement> AllElements(ReportDefinition definition) =>
        (definition.Body?.Elements ?? []).Concat(definition.Bands.SelectMany(b => b.Elements));

    // =====================================================================================================
    // Import
    // =====================================================================================================

    private sealed record Analysis(
        ImportPlan Plan,
        Dictionary<string, ReportDefinition> Final,
        Dictionary<string, Guid> TargetIds,
        Dictionary<string, ImportAction> Actions,
        List<(Guid SourceId, string Sha)> NewAssets);

    public async Task<ImportPlan> PreviewAsync(byte[] package, ImportOptions options, CancellationToken ct) =>
        (await AnalyzeAsync(PackageIo.Read(package), options, ct)).Plan;

    public async Task<ImportResult> ApplyAsync(byte[] package, ImportOptions options, string? importedBy, CancellationToken ct)
    {
        var loaded = PackageIo.Read(package);
        var first = await AnalyzeAsync(loaded, options, ct);

        if (first.Plan.Items.Any(i => i.Status == "invalid" && first.Actions.TryGetValue(i.Code, out var a) && a != ImportAction.Skip))
        {
            throw new InvalidPackageException("Some reports in the package are invalid; skip them or fix the package first.");
        }

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);

            // Folders and images first; then analyse again so references resolve to what now exists.
            var folderIds = await EnsureFoldersAsync(loaded, options.TargetFolderId, ct);
            foreach (var (sourceId, _) in first.NewAssets)
            {
                var entry = loaded.Manifest.Assets.First(a => a.SourceId == sourceId);
                await assets.AddAsync(loaded.Assets[sourceId], entry.ContentType, entry.FileName, ct);
            }

            var run = await AnalyzeAsync(loaded, options, ct);
            var items = new List<ImportResultItem>();
            int created = 0, updated = 0, copies = 0, skipped = 0;

            foreach (var item in run.Plan.Items)
            {
                var action = run.Actions[item.Code];
                var definition = run.Final.GetValueOrDefault(item.Code);
                if (action == ImportAction.Skip || definition is null)
                {
                    skipped++;
                    items.Add(new ImportResultItem(item.Code, item.Name, run.TargetIds.TryGetValue(item.Code, out var existing) && action == ImportAction.Skip ? existing : null, ImportAction.Skip, null));
                    continue;
                }

                var targetId = run.TargetIds[item.Code];
                ReportRecord saved;
                if (action == ImportAction.Update)
                {
                    saved = await reports.UpdateAsync(targetId, definition, expectedToken: null, ct, importedBy, ImportedOrigin)
                        ?? throw new InvalidOperationException($"Report '{item.Code}' disappeared during the import.");
                    updated++;
                }
                else
                {
                    definition.Id = targetId;
                    saved = await reports.CreateAsync(definition, ct, createdByUserId: null, importedBy, ImportedOrigin);
                    if (item.Folder is not null || options.TargetFolderId is not null)
                    {
                        await folders.SetReportFolderAsync(saved.Id, FolderFor(folderIds, options.TargetFolderId, item.Folder), ct);
                    }

                    if (action == ImportAction.Copy)
                    {
                        copies++;
                    }
                    else
                    {
                        created++;
                    }
                }

                var version = (await reports.ListVersionsAsync(saved.Id, ct)).FirstOrDefault()?.Version;
                items.Add(new ImportResultItem(item.Code, saved.Definition.Name, saved.Id, action, version));
            }

            await tx.CommitAsync(ct);

            logger.LogInformation(
                "Package import by {User} from {Environment} (exported {Exported:u}): {Created} created, {Updated} updated, {Copies} copied, {Skipped} skipped",
                importedBy, loaded.Manifest.SourceEnvironment, loaded.Manifest.ExportedAtUtc, created, updated, copies, skipped);

            return new ImportResult(created, updated, copies, skipped, items, run.Plan.Warnings);
        });
    }

    private async Task<Analysis> AnalyzeAsync(LoadedPackage package, ImportOptions options, CancellationToken ct)
    {
        var manifest = package.Manifest;
        var warnings = new List<string>();

        if (options.TargetFolderId is { } target && (await folders.ListAsync(ct)).All(f => f.Id != target))
        {
            throw new InvalidPackageException("The chosen target folder does not exist.");
        }

        // What the target environment has today.
        var existingSummaries = (await reports.ListAsync(ct)).Where(r => r.Code is not null).ToDictionary(r => r.Code!, StringComparer.Ordinal);
        var existingCodes = new HashSet<string>(existingSummaries.Keys, StringComparer.Ordinal);
        var targetConnections = await connections.ListAsync(ct);
        var requested = (options.Decisions ?? []).GroupBy(d => d.Code, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Last().Action, StringComparer.Ordinal);

        // Images: reuse an identical one if the target already has it.
        var assetTargets = new Dictionary<Guid, Guid>();
        var newAssets = new List<(Guid, string)>();
        var seenSha = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var entry in manifest.Assets)
        {
            var sha = PackageIo.Sha256(package.Assets[entry.SourceId]);
            if (await assets.FindIdBySha256Async(sha, ct) is { } existingAsset)
            {
                assetTargets[entry.SourceId] = existingAsset;
            }
            else
            {
                // Same bytes under two source ids become one new asset.
                if (!seenSha.TryGetValue(sha, out var placeholder))
                {
                    placeholder = Guid.NewGuid();
                    seenSha[sha] = placeholder;
                    newAssets.Add((entry.SourceId, sha));
                }

                assetTargets[entry.SourceId] = placeholder;
            }
        }

        var assetsReused = assetTargets.Count(kv => !newAssets.Any(n => n.Item1 == kv.Key) && manifest.Assets.Any(a => a.SourceId == kv.Key));
        var connectionChecks = manifest.Connections
            .Select(c =>
            {
                var match = targetConnections.FirstOrDefault(t => string.Equals(t.Name, c.Name, StringComparison.OrdinalIgnoreCase));
                return new ImportConnectionCheck(c.Name, c.Provider, match is null ? "missing" : match.Provider == c.Provider ? "found" : "providerMismatch");
            })
            .ToList();

        // Decide each report's fate and target id (assigned before rewriting so subreport references can point at them).
        var actions = new Dictionary<string, ImportAction>(StringComparer.Ordinal);
        var targetIds = new Dictionary<string, Guid>(StringComparer.Ordinal);
        var finalCodes = new Dictionary<string, string>(StringComparer.Ordinal);
        var usedCodes = new HashSet<string>(existingCodes, StringComparer.Ordinal);

        foreach (var entry in manifest.Reports)
        {
            var exists = existingSummaries.TryGetValue(entry.Code, out var current);
            var action = requested.TryGetValue(entry.Code, out var wanted) ? wanted : exists ? ImportAction.Update : ImportAction.Create;
            if (exists && action == ImportAction.Create)
            {
                action = ImportAction.Update;
            }

            if (!exists && action is ImportAction.Update or ImportAction.Copy)
            {
                action = ImportAction.Create;
            }

            actions[entry.Code] = action;
            switch (action)
            {
                case ImportAction.Create:
                    targetIds[entry.Code] = Guid.NewGuid();
                    finalCodes[entry.Code] = entry.Code;
                    usedCodes.Add(entry.Code);
                    break;
                case ImportAction.Copy:
                    var copyCode = ReportCode.MakeUnique(ReportCode.Normalize($"{entry.Code}-copy"), usedCodes.Contains);
                    usedCodes.Add(copyCode);
                    targetIds[entry.Code] = Guid.NewGuid();
                    finalCodes[entry.Code] = copyCode;
                    break;
                default: // Update / Skip of an existing report
                    if (exists)
                    {
                        targetIds[entry.Code] = current!.Id;
                        finalCodes[entry.Code] = entry.Code;
                    }

                    break;
            }
        }

        var sourceToCode = manifest.Reports.ToDictionary(r => r.SourceId, r => r.Code);
        var final = new Dictionary<string, ReportDefinition>(StringComparer.Ordinal);
        var items = new List<ImportPlanItem>();
        var connectionByName = targetConnections.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var entry in manifest.Reports)
        {
            var source = package.Reports[entry.Code];
            var rewritten = ReportJson.Deserialize(ReportJson.Serialize(source));
            rewritten.Code = finalCodes.GetValueOrDefault(entry.Code, entry.Code);
            if (actions[entry.Code] == ImportAction.Copy)
            {
                rewritten.Name = $"{rewritten.Name} (copy)";
            }

            // subreports -> the report with that code in the target; connections -> same-named connection; images -> target asset ids
            foreach (var element in AllElements(rewritten).Where(e => e.Subreport is not null))
            {
                var sub = element.Subreport!;
                if (Guid.TryParse(sub.ReportId, out var sourceId) && sourceToCode.TryGetValue(sourceId, out var subCode))
                {
                    if (targetIds.TryGetValue(subCode, out var mapped))
                    {
                        sub.ReportId = mapped.ToString();
                    }
                    else
                    {
                        warnings.Add($"subreportUnresolved|{rewritten.Name}");
                    }
                }
            }

            foreach (var connection in rewritten.Connections)
            {
                connection.ConnectionId = connectionByName.TryGetValue(connection.Name, out var registered) && registered.Provider == connection.Provider
                    ? registered.Id
                    : Guid.Empty;
            }

            var json = ReportJson.Serialize(rewritten);
            json = AssetRef().Replace(json, m =>
                Guid.TryParse(m.Groups[1].Value, out var id) && assetTargets.TryGetValue(id, out var mapped) ? $"asset:{mapped}" : m.Value);
            rewritten = ReportJson.Deserialize(json);
            rewritten.Id = targetIds.GetValueOrDefault(entry.Code);

            // Checks
            var errors = (await validator.ValidateAsync(rewritten, ct)).Errors.Select(e => e.ErrorMessage).Distinct().Take(6).ToList();
            var status = "new";
            var changes = (IReadOnlyList<string>)[];
            string? existingName = null;
            int? existingVersion = null;
            var exists = existingSummaries.TryGetValue(entry.Code, out var existing);
            if (exists)
            {
                var record = await reports.GetAsync(existing!.Id, ct);
                existingName = existing.Name;
                existingVersion = (await reports.ListVersionsAsync(existing.Id, ct)).FirstOrDefault()?.Version;
                var probe = ReportJson.Deserialize(ReportJson.Serialize(rewritten));
                probe.Code = record!.Definition.Code;
                probe.Id = record.Definition.Id;
                changes = ReportChanges.Summarize(record.Definition, probe);
                status = changes.Count == 0 ? "identical" : "changed";
            }

            if (errors.Count > 0)
            {
                status = "invalid";
            }

            // Default action: nothing to do for an identical report; never import an invalid one.
            var defaultAction = status switch
            {
                "invalid" => ImportAction.Skip,
                "identical" => ImportAction.Skip,
                "changed" => ImportAction.Update,
                _ => ImportAction.Create,
            };
            var allowed = status switch
            {
                "invalid" => new[] { ImportAction.Skip },
                "new" => [ImportAction.Create, ImportAction.Skip],
                _ => [ImportAction.Update, ImportAction.Copy, ImportAction.Skip],
            };

            var chosen = requested.TryGetValue(entry.Code, out var asked) && allowed.Contains(asked) ? asked : defaultAction;
            if (status == "invalid")
            {
                chosen = ImportAction.Skip;
            }

            // Re-derive what the chosen action does to ids/codes if the default for "identical"/"changed" differed from the first guess.
            if (chosen != actions[entry.Code])
            {
                actions[entry.Code] = chosen;
                if (chosen == ImportAction.Copy)
                {
                    var copyCode = ReportCode.MakeUnique(ReportCode.Normalize($"{entry.Code}-copy"), usedCodes.Contains);
                    usedCodes.Add(copyCode);
                    targetIds[entry.Code] = Guid.NewGuid();
                    finalCodes[entry.Code] = copyCode;
                    rewritten.Code = copyCode;
                    rewritten.Id = targetIds[entry.Code];
                    rewritten.Name = $"{source.Name} (copy)";
                }
                else if (exists)
                {
                    targetIds[entry.Code] = existing!.Id;
                    rewritten.Id = existing.Id;
                    rewritten.Code = entry.Code;
                    rewritten.Name = source.Name;
                }
            }

            final[entry.Code] = rewritten;
            items.Add(new ImportPlanItem(
                entry.Code, source.Name, entry.Role, entry.Folder, status, changes, existingName, existingVersion,
                errors, defaultAction, allowed, chosen));
        }

        foreach (var check in connectionChecks.Where(c => c.Status != "found"))
        {
            warnings.Add($"connection{(check.Status == "missing" ? "Missing" : "ProviderMismatch")}|{check.Name}");
        }

        if (manifest.RedactedValues > 0)
        {
            warnings.Add($"redacted|{manifest.RedactedValues}");
        }

        var plan = new ImportPlan(
            new ImportSource(manifest.SourceEnvironment, manifest.ExportedAtUtc, manifest.ExportedBy, manifest.AppVersion),
            items,
            package.Folders,
            newAssets.Count,
            assetsReused,
            connectionChecks,
            manifest.RedactedValues,
            warnings.Distinct().ToList());

        return new Analysis(plan, final, targetIds, actions, newAssets);
    }

    // ---- folders ----------------------------------------------------------------------------------------

    /// <summary>Recreates the package's folder paths under the target folder, reusing folders that already exist by name.</summary>
    private async Task<Dictionary<string, Guid?>> EnsureFoldersAsync(LoadedPackage package, Guid? targetFolderId, CancellationToken ct)
    {
        var all = (await folders.ListAsync(ct)).ToList();
        var resolved = new Dictionary<string, Guid?>(StringComparer.Ordinal) { [string.Empty] = targetFolderId };

        var paths = package.Folders
            .Concat(package.Manifest.Reports.Select(r => r.Folder).Where(f => !string.IsNullOrWhiteSpace(f)).Select(f => f!))
            .Distinct(StringComparer.Ordinal);

        foreach (var path in paths)
        {
            Guid? parent = targetFolderId;
            var built = new List<string>();
            foreach (var raw in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
            {
                var name = raw.Trim();
                if (name.Length is 0 or > 200)
                {
                    throw new InvalidPackageException("The package contains an invalid folder name.");
                }

                built.Add(name);
                var key = string.Join('/', built);
                if (!resolved.TryGetValue(key, out var id))
                {
                    var match = all.FirstOrDefault(f => f.ParentFolderId == parent && string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));
                    if (match is null)
                    {
                        match = await folders.CreateAsync(name, parent, ct)
                            ?? throw new InvalidPackageException("The target folder does not exist.");
                        all.Add(match);
                    }

                    id = match.Id;
                    resolved[key] = id;
                }

                parent = id;
            }
        }

        return resolved;
    }

    private static Guid? FolderFor(Dictionary<string, Guid?> resolved, Guid? targetFolderId, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return targetFolderId;
        }

        var key = string.Join('/', path.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()));
        return resolved.TryGetValue(key, out var id) ? id : targetFolderId;
    }
}
