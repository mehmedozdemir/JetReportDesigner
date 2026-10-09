using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using JetReportDesigner.Api.Contracts;
using JetReportDesigner.Api.Transfer;
using JetReportDesigner.Core.Model;
using JetReportDesigner.Core.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace JetReportDesigner.Api.Tests;

/// <summary>
/// Package export/import between two organizations (standing in for two environments such as UAT and
/// production) run against a real database for each provider.
/// </summary>
public abstract class TransferApiTestsBase(DatabaseFixture fixture)
{
    private static readonly JsonSerializerOptions Json = ReportJson.Apply(new JsonSerializerOptions());

    // A valid 1x1 PNG.
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==");

    private WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("Storage:Provider", fixture.Provider);
            builder.UseSetting("Storage:ConnectionString", fixture.ConnectionString);
            builder.UseSetting("Storage:MigrateOnStartup", "true");
            builder.UseSetting("Transfer:EnvironmentName", "UAT-test");
            builder.UseTestJwt();
        });

    private static ReportDefinition Simple(string name, Action<ReportDefinition>? configure = null)
    {
        var report = new ReportDefinition
        {
            Name = name,
            LayoutMode = LayoutMode.Free,
            Body = new ReportBody { Height = 200, Elements = [] },
        };
        configure?.Invoke(report);
        return report;
    }

    private static async Task<ReportResponse> CreateReport(HttpClient client, ReportDefinition definition)
    {
        var response = await client.PostAsJsonAsync("/api/reports", definition, Json);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ReportResponse>(Json))!;
    }

    private static async Task<Guid> CreateFolder(HttpClient client, string name, Guid? parent)
    {
        var response = await client.PostAsJsonAsync("/api/folders", new { name, parentFolderId = parent });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static async Task<Guid> UploadPng(HttpClient client)
    {
        using var form = new MultipartFormDataContent { { new ByteArrayContent(Png), "file", "logo.png" } };
        var response = await client.PostAsync("/api/assets", form);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static async Task<byte[]> Export(HttpClient client, object request)
    {
        var response = await client.PostAsJsonAsync("/api/transfer/export", request, Json);
        response.EnsureSuccessStatusCode();
        Assert.EndsWith(".jrdpkg", response.Content.Headers.ContentDisposition!.FileName!.Trim('"'), StringComparison.Ordinal);
        return await response.Content.ReadAsByteArrayAsync();
    }

    private static MultipartFormDataContent Upload(byte[] package, object? options = null)
    {
        var form = new MultipartFormDataContent { { new ByteArrayContent(package), "file", "x.jrdpkg" } };
        if (options is not null)
        {
            form.Add(new StringContent(JsonSerializer.Serialize(options, Json)), "options");
        }

        return form;
    }

    private static async Task<JsonElement> Read(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task A_folder_travels_to_another_environment_with_everything_it_needs()
    {
        if (!fixture.Available)
        {
            return; // Docker unavailable; exercised in CI.
        }

        await using var factory = CreateFactory();
        var uat = await factory.CreateDesignerClientAsync();
        var prod = await factory.CreateDesignerClientAsync();

        // --- UAT: Finans/Aylık holds a parent report that embeds a child as a subreport, shows a logo,
        //     reads a REST source with a secret header and uses a SQL connection.
        var finans = await CreateFolder(uat, "Finans", null);
        var aylik = await CreateFolder(uat, "Aylık", finans);
        var logo = await UploadPng(uat);

        var child = await CreateReport(uat, Simple("Alt Rapor"));
        var parent = await CreateReport(uat, Simple("Ana Rapor", r =>
        {
            r.Connections.Add(new ConnectionRef { Name = "erp", ConnectionId = Guid.NewGuid(), Provider = SqlProvider.SqlServer });
            r.DataSources.Add(new DataSourceDefinition
            {
                Name = "api",
                Kind = DataSourceKind.Rest,
                Rest = new RestSourceConfig
                {
                    Url = "https://example.com/data?api_key=SECRET123&x=1",
                    Headers = new Dictionary<string, string> { ["Authorization"] = "Bearer TOP-SECRET", ["Accept"] = "application/json" },
                },
            });
            r.Body!.Elements.Add(new ReportElement
            {
                Id = "sub", Type = ElementType.Subreport, Bounds = new Bounds { X = 0, Y = 0, Width = 100, Height = 50 },
                Subreport = new SubreportSpec { ReportId = child.Id.ToString() },
            });
            r.Body.Elements.Add(new ReportElement
            {
                Id = "img", Type = ElementType.Image, Bounds = new Bounds { X = 0, Y = 60, Width = 40, Height = 40 },
                Image = new ImageSpec { Source = $"asset:{logo}" },
            });
        }));
        await uat.PutAsJsonAsync($"/api/reports/{parent.Id}/folder", new { folderId = aylik });

        // The plan says what will be in the package, before anything is made.
        var plan = await Read(await uat.PostAsJsonAsync("/api/transfer/export/plan", new { folderIds = new[] { finans } }, Json));
        Assert.Equal(1, plan.GetProperty("assets").GetInt32());
        Assert.Equal(2, plan.GetProperty("redactedValues").GetInt32()); // the Authorization header and the api_key in the URL
        var planned = plan.GetProperty("reports").EnumerateArray().ToDictionary(r => r.GetProperty("name").GetString()!, r => r.GetProperty("role").GetString());
        Assert.Equal("selected", planned["Ana Rapor"]);
        Assert.Equal("dependency", planned["Alt Rapor"]); // pulled in automatically as the subreport

        var package = await Export(uat, new { folderIds = new[] { finans } });

        // --- the package carries no secrets
        using (var zip = new ZipArchive(new MemoryStream(package)))
        {
            var text = string.Concat(zip.Entries
                .Where(e => !e.FullName.StartsWith("assets/", StringComparison.Ordinal))
                .Select(e =>
                {
                    using var reader = new StreamReader(e.Open());
                    return reader.ReadToEnd();
                }));
            Assert.DoesNotContain("TOP-SECRET", text, StringComparison.Ordinal);
            Assert.DoesNotContain("SECRET123", text, StringComparison.Ordinal);
            Assert.Contains("application/json", text, StringComparison.Ordinal); // harmless header kept
        }

        // --- Production: preview changes nothing
        var preview = await Read(await prod.PostAsync("/api/transfer/import/preview", Upload(package)));
        Assert.Equal("UAT-test", preview.GetProperty("source").GetProperty("environment").GetString());
        Assert.All(preview.GetProperty("items").EnumerateArray(), i => Assert.Equal("new", i.GetProperty("status").GetString()));
        Assert.Equal("missing", preview.GetProperty("connections")[0].GetProperty("status").GetString()); // 'erp' is not defined in prod
        Assert.Empty((await prod.GetFromJsonAsync<List<ReportSummaryResponse>>("/api/reports", Json))!);

        // --- Production: import
        var result = await Read(await prod.PostAsync("/api/transfer/import", Upload(package)));
        Assert.Equal(2, result.GetProperty("created").GetInt32());

        var prodReports = (await prod.GetFromJsonAsync<List<ReportSummaryResponse>>("/api/reports", Json))!;
        Assert.Equal(2, prodReports.Count);
        var prodParent = prodReports.Single(r => r.Name == "Ana Rapor");
        var prodChild = prodReports.Single(r => r.Name == "Alt Rapor");
        Assert.NotEqual(parent.Id, prodParent.Id);                      // ids are new here ...
        Assert.Equal(parent.Definition.Code, prodParent.Code);           // ... the code is the stable name

        // folders were recreated and the parent filed in Finans/Aylık
        var prodFolders = await prod.GetFromJsonAsync<List<FolderResponse>>("/api/folders", Json);
        var prodAylik = prodFolders!.Single(f => f.Name == "Aylık");
        Assert.Equal("Finans", prodFolders!.Single(f => f.Id == prodAylik.ParentFolderId).Name);
        Assert.Equal(prodAylik.Id, prodParent.FolderId);

        // references were rewritten to this environment's ids
        var saved = (await prod.GetFromJsonAsync<ReportResponse>($"/api/reports/{prodParent.Id}", Json))!.Definition;
        Assert.Equal(prodChild.Id.ToString(), saved.Body!.Elements.Single(e => e.Id == "sub").Subreport!.ReportId);
        var prodAsset = saved.Body.Elements.Single(e => e.Id == "img").Image!.Source;
        Assert.NotEqual($"asset:{logo}", prodAsset);
        var assetBytes = await prod.GetByteArrayAsync($"/api/assets/{prodAsset["asset:".Length..]}");
        Assert.Equal(Png, assetBytes);
        Assert.Equal(Guid.Empty, saved.Connections.Single().ConnectionId);       // unresolved, not copied from UAT
        Assert.Equal(string.Empty, saved.DataSources.Single().Rest!.Headers["Authorization"]);
        Assert.Equal("application/json", saved.DataSources.Single().Rest!.Headers["Accept"]);
        Assert.DoesNotContain("SECRET123", saved.DataSources.Single().Rest!.Url, StringComparison.Ordinal);

        // the import is a version in the history, marked as imported
        var versions = await prod.GetFromJsonAsync<JsonElement>($"/api/reports/{prodParent.Id}/versions");
        Assert.Equal("imported", versions[0].GetProperty("changes")[0].GetString());

        // --- Running it again changes nothing
        var again = await Read(await prod.PostAsync("/api/transfer/import/preview", Upload(package)));
        Assert.All(again.GetProperty("items").EnumerateArray(), i => Assert.Equal("identical", i.GetProperty("status").GetString()));
        var rerun = await Read(await prod.PostAsync("/api/transfer/import", Upload(package)));
        Assert.Equal(0, rerun.GetProperty("created").GetInt32() + rerun.GetProperty("updated").GetInt32());
        Assert.Equal(2, rerun.GetProperty("skipped").GetInt32());
        Assert.Equal(1, (await prod.GetFromJsonAsync<JsonElement>($"/api/reports/{prodChild.Id}/versions")).GetArrayLength());

        // --- UAT changes the child; the next package updates prod as a new version, rollback-able from history
        var current = (await uat.GetFromJsonAsync<ReportResponse>($"/api/reports/{child.Id}", Json))!;
        current.Definition.Body!.Elements.Add(new ReportElement
        {
            Id = "t", Type = ElementType.Label, Text = "v2", Bounds = new Bounds { X = 0, Y = 0, Width = 10, Height = 10 },
        });
        using (var put = new HttpRequestMessage(HttpMethod.Put, $"/api/reports/{child.Id}") { Content = JsonContent.Create(current.Definition, options: Json) })
        {
            put.Headers.TryAddWithoutValidation("If-Match", $"\"{current.ConcurrencyToken}\"");
            (await uat.SendAsync(put)).EnsureSuccessStatusCode();
        }

        var package2 = await Export(uat, new { folderIds = new[] { finans } });
        var preview2 = await Read(await prod.PostAsync("/api/transfer/import/preview", Upload(package2)));
        var childItem = preview2.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("name").GetString() == "Alt Rapor");
        Assert.Equal("changed", childItem.GetProperty("status").GetString());
        Assert.Equal("added:1", childItem.GetProperty("changes")[0].GetString());
        Assert.Equal(1, childItem.GetProperty("existingVersion").GetInt32());

        var applied = await Read(await prod.PostAsync("/api/transfer/import", Upload(package2)));
        Assert.Equal(1, applied.GetProperty("updated").GetInt32());
        var childVersions = await prod.GetFromJsonAsync<JsonElement>($"/api/reports/{prodChild.Id}/versions");
        Assert.Equal(2, childVersions.GetArrayLength());
        Assert.Equal("imported", childVersions[0].GetProperty("changes")[0].GetString());
        // ... and it can be rolled back
        (await prod.PostAsync($"/api/reports/{prodChild.Id}/versions/1/restore", null)).EnsureSuccessStatusCode();
        Assert.Empty((await prod.GetFromJsonAsync<ReportResponse>($"/api/reports/{prodChild.Id}", Json))!.Definition.Body!.Elements);

        // --- "Copy" keeps the existing report and adds the package's next to it under a new code
        var copied = await Read(await prod.PostAsync(
            "/api/transfer/import",
            Upload(package2, new { decisions = new[] { new { code = child.Definition.Code, action = "copy" } } })));
        Assert.Equal(1, copied.GetProperty("copies").GetInt32());
        var afterCopy = (await prod.GetFromJsonAsync<List<ReportSummaryResponse>>("/api/reports", Json))!;
        Assert.Contains(afterCopy, r => r.Code == $"{child.Definition.Code}-copy");
    }

    [Fact]
    public async Task Bad_packages_and_wrong_roles_are_refused()
    {
        if (!fixture.Available)
        {
            return; // Docker unavailable; exercised in CI.
        }

        await using var factory = CreateFactory();
        var designer = await factory.CreateDesignerClientAsync();
        var other = await factory.CreateDesignerClientAsync();
        var report = await CreateReport(designer, Simple("Pkg"));
        var package = await Export(designer, new { reportIds = new[] { report.Id } });

        // not a package
        var garbage = await other.PostAsync("/api/transfer/import/preview", Upload(Encoding.UTF8.GetBytes("hello")));
        Assert.Equal(HttpStatusCode.BadRequest, garbage.StatusCode);

        // modified after export: the checksum no longer matches, nothing is imported
        byte[] tamperedPackage;
        {
            var entries = new List<(string Name, byte[] Bytes)>();
            using (var zip = new ZipArchive(new MemoryStream(package)))
            {
                foreach (var e in zip.Entries)
                {
                    using var ms = new MemoryStream();
                    using (var s = e.Open())
                    {
                        s.CopyTo(ms);
                    }

                    var bytes = ms.ToArray();
                    if (e.FullName.StartsWith("reports/", StringComparison.Ordinal))
                    {
                        bytes = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(bytes).Replace("Pkg", "Hacked", StringComparison.Ordinal));
                    }

                    entries.Add((e.FullName, bytes));
                }
            }

            using var output = new MemoryStream();
            using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var (name, bytes) in entries)
                {
                    using var s = zip.CreateEntry(name).Open();
                    s.Write(bytes);
                }
            }

            tamperedPackage = output.ToArray();
        }

        var tampered = await other.PostAsync("/api/transfer/import", Upload(tamperedPackage));
        Assert.Equal(HttpStatusCode.BadRequest, tampered.StatusCode);
        Assert.Contains("checksum", await tampered.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        Assert.Empty((await other.GetFromJsonAsync<List<ReportSummaryResponse>>("/api/reports", Json))!);

        // a read-only API key can neither export nor import
        var key = (await Read(await designer.PostAsJsonAsync("/api/api-keys", new { name = "ro" }))).GetProperty("secret").GetString()!;
        var ro = factory.CreateClient();
        ro.DefaultRequestHeaders.Add("X-Api-Key", key);
        Assert.Equal(HttpStatusCode.Forbidden, (await ro.PostAsJsonAsync("/api/transfer/export", new { reportIds = new[] { report.Id } }, Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ro.PostAsync("/api/transfer/import", Upload(package))).StatusCode);

        // nothing selected
        Assert.Equal(HttpStatusCode.BadRequest, (await designer.PostAsJsonAsync("/api/transfer/export", new { }, Json)).StatusCode);
    }
}

public sealed class SqlServerTransferApiTests(SqlServerDatabaseFixture fixture)
    : TransferApiTestsBase(fixture), IClassFixture<SqlServerDatabaseFixture>
{
}

public sealed class PostgreSqlTransferApiTests(PostgreSqlDatabaseFixture fixture)
    : TransferApiTestsBase(fixture), IClassFixture<PostgreSqlDatabaseFixture>
{
}
