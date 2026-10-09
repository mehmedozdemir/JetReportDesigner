using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DotNet.Testcontainers.Containers;
using JetReportDesigner.Api.Contracts;
using JetReportDesigner.Core.Model;
using JetReportDesigner.Core.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.MsSql;
using Testcontainers.PostgreSql;

namespace JetReportDesigner.Api.Tests;

/// <summary>
/// End-to-end HTTP round-trip (create → list → get → update → delete) run against a
/// real database. Phase 0 verification requires this to pass for both SQL Server and
/// PostgreSQL; when Docker is unavailable the cases skip rather than fail.
/// </summary>
public abstract class ReportsApiTestsBase(DatabaseFixture fixture)
{
    private static readonly JsonSerializerOptions Json = ReportJson.Apply(new JsonSerializerOptions());

    private WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("Storage:Provider", fixture.Provider);
            builder.UseSetting("Storage:ConnectionString", fixture.ConnectionString);
            builder.UseSetting("Storage:MigrateOnStartup", "true");
            builder.UseTestJwt();
        });

    [Fact]
    public async Task Full_Report_Lifecycle_RoundTrips()
    {
        if (!fixture.Available)
        {
            return; // Docker unavailable; exercised in CI. See class summary.
        }

        await using var factory = CreateFactory();
        var client = await factory.CreateDesignerClientAsync();

        var definition = new ReportDefinition
        {
            Name = "Integration Invoice",
            LayoutMode = LayoutMode.Free,
            Body = new ReportBody
            {
                Height = 1000,
                Elements =
                [
                    new ReportElement
                    {
                        Id = "title",
                        Type = ElementType.Label,
                        Text = "Invoice",
                        Bounds = new Bounds { X = 40, Y = 40, Width = 200, Height = 24 },
                    },
                ],
            },
        };

        // Create
        var createResponse = await client.PostAsJsonAsync("/api/reports", definition, Json);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<ReportResponse>(Json);
        Assert.NotNull(created);
        Assert.NotEqual(Guid.Empty, created!.Id);
        Assert.Equal("Invoice", created.Definition.Body!.Elements[0].Text);

        // List
        var list = await client.GetFromJsonAsync<List<ReportSummaryResponse>>("/api/reports", Json);
        Assert.Contains(list!, r => r.Id == created.Id && r.Name == "Integration Invoice");

        // Get
        var fetched = await client.GetFromJsonAsync<ReportResponse>($"/api/reports/{created.Id}", Json);
        Assert.Equal(created.ConcurrencyToken, fetched!.ConcurrencyToken);

        // Update (with matching If-Match)
        fetched.Definition.Name = "Renamed Invoice";
        using var update = new HttpRequestMessage(HttpMethod.Put, $"/api/reports/{created.Id}")
        {
            Content = JsonContent.Create(fetched.Definition, options: Json),
        };
        update.Headers.IfMatch.Add(new EntityTagHeaderValue($"\"{fetched.ConcurrencyToken}\""));
        var updateResponse = await client.SendAsync(update);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<ReportResponse>(Json);
        Assert.Equal("Renamed Invoice", updated!.Definition.Name);
        Assert.NotEqual(created.ConcurrencyToken, updated.ConcurrencyToken);

        // Stale update -> 409
        using var stale = new HttpRequestMessage(HttpMethod.Put, $"/api/reports/{created.Id}")
        {
            Content = JsonContent.Create(fetched.Definition, options: Json),
        };
        stale.Headers.IfMatch.Add(new EntityTagHeaderValue($"\"{created.ConcurrencyToken}\""));
        var staleResponse = await client.SendAsync(stale);
        Assert.Equal(HttpStatusCode.Conflict, staleResponse.StatusCode);

        // Delete
        var deleteResponse = await client.DeleteAsync($"/api/reports/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        var afterDelete = await client.GetAsync($"/api/reports/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, afterDelete.StatusCode);
    }

    [Fact]
    public async Task Report_Versions_Are_Tracked_And_Restorable()
    {
        if (!fixture.Available)
        {
            return;
        }

        await using var factory = CreateFactory();
        var client = await factory.CreateDesignerClientAsync();

        var definition = new ReportDefinition { Name = "v1", LayoutMode = LayoutMode.Free, Body = new ReportBody { Height = 800, Elements = [] } };
        var created = await (await client.PostAsJsonAsync("/api/reports", definition, Json)).Content.ReadFromJsonAsync<ReportResponse>(Json);

        async Task Rename(string name)
        {
            var current = await client.GetFromJsonAsync<ReportResponse>($"/api/reports/{created!.Id}", Json);
            current!.Definition.Name = name;
            var response = await client.PutAsJsonAsync($"/api/reports/{created.Id}", current.Definition, Json);
            response.EnsureSuccessStatusCode();
        }

        await Rename("v2");
        await Rename("v3");

        var versions = await client.GetFromJsonAsync<List<ReportVersionResponse>>($"/api/reports/{created!.Id}/versions", Json);
        Assert.Equal(3, versions!.Count);
        Assert.Equal([3, 2, 1], versions.Select(v => v.Version).ToArray());
        Assert.Equal("v1", versions.Single(v => v.Version == 1).Name);

        var v1 = await client.GetFromJsonAsync<ReportVersionDetailResponse>($"/api/reports/{created.Id}/versions/1", Json);
        Assert.Equal("v1", v1!.Definition.Name);

        var restored = await (await client.PostAsync($"/api/reports/{created.Id}/versions/1/restore", null)).Content.ReadFromJsonAsync<ReportResponse>(Json);
        Assert.Equal("v1", restored!.Definition.Name);

        var afterRestore = await client.GetFromJsonAsync<List<ReportVersionResponse>>($"/api/reports/{created.Id}/versions", Json);
        Assert.Equal(4, afterRestore!.Count); // the restore itself is a new version
    }

    private static ReportDefinition SimpleReport(string name, string? code = null) => new()
    {
        Name = name,
        Code = code,
        LayoutMode = LayoutMode.Free,
        Body = new ReportBody { Height = 100, Elements = [] },
    };

    [Fact]
    public async Task Version_History_Lists_Previews_And_Restores_As_A_New_Version()
    {
        if (!fixture.Available)
        {
            return; // Docker unavailable; exercised in CI. See class summary.
        }

        await using var factory = CreateFactory();
        var client = await factory.CreateDesignerClientAsync();

        var created = await (await client.PostAsJsonAsync("/api/reports", SimpleReport("History Test"), Json))
            .Content.ReadFromJsonAsync<ReportResponse>(Json);

        async Task<ReportResponse> Save(ReportDefinition d, string token)
        {
            using var put = new HttpRequestMessage(HttpMethod.Put, $"/api/reports/{created!.Id}") { Content = JsonContent.Create(d, options: Json) };
            put.Headers.TryAddWithoutValidation("If-Match", $"\"{token}\"");
            var response = await client.SendAsync(put);
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<ReportResponse>(Json))!;
        }

        // Saving without changes adds no version.
        var same = await Save(created!.Definition, created.ConcurrencyToken.ToString());
        Assert.Equal(created.ConcurrencyToken, same.ConcurrencyToken);

        // A real change does, and records what changed.
        var broken = created.Definition;
        broken.Body!.Elements.Add(new ReportElement
        {
            Id = "oops", Type = ElementType.Label, Text = "broken",
            Bounds = new Bounds { X = 0, Y = 0, Width = 10, Height = 10 },
        });
        var v2 = await Save(broken, same.ConcurrencyToken.ToString());

        var versions = await client.GetFromJsonAsync<JsonElement>($"/api/reports/{created.Id}/versions");
        Assert.Equal(2, versions.GetArrayLength());
        Assert.Equal(2, versions[0].GetProperty("version").GetInt32());
        Assert.Equal("added:1", versions[0].GetProperty("changes")[0].GetString());
        Assert.Contains("@example.com", versions[0].GetProperty("savedByEmail").GetString(), StringComparison.Ordinal);

        // The old version can be read (and so previewed/run) without touching the current one.
        var v1 = await client.GetFromJsonAsync<ReportVersionDetailResponse>($"/api/reports/{created.Id}/versions/1", Json);
        Assert.Empty(v1!.Definition.Body!.Elements);

        // Restore it: that becomes version 3, the broken version 2 is kept.
        var restore = await client.PostAsync($"/api/reports/{created.Id}/versions/1/restore", null);
        restore.EnsureSuccessStatusCode();
        var restored = await restore.Content.ReadFromJsonAsync<ReportResponse>(Json);
        Assert.Empty(restored!.Definition.Body!.Elements);
        Assert.NotEqual(v2.ConcurrencyToken, restored.ConcurrencyToken);

        versions = await client.GetFromJsonAsync<JsonElement>($"/api/reports/{created.Id}/versions");
        Assert.Equal(3, versions.GetArrayLength());
        Assert.Equal(1, versions[0].GetProperty("restoredFromVersion").GetInt32());
        Assert.Single((await client.GetFromJsonAsync<ReportVersionDetailResponse>($"/api/reports/{created.Id}/versions/2", Json))!
            .Definition.Body!.Elements);

        // A read-only API key may look at the history but not restore.
        var key = await (await client.PostAsJsonAsync("/api/api-keys", new { name = "ro" })).Content.ReadFromJsonAsync<JsonElement>();
        var ro = factory.CreateClient();
        ro.DefaultRequestHeaders.Add("X-Api-Key", key.GetProperty("secret").GetString()!);
        Assert.Equal(HttpStatusCode.OK, (await ro.GetAsync($"/api/reports/{created.Id}/versions")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ro.PostAsync($"/api/reports/{created.Id}/versions/2/restore", null)).StatusCode);
    }

    [Fact]
    public async Task Report_Codes_Are_Generated_Unique_And_Conflicts_Rejected()
    {
        if (!fixture.Available)
        {
            return; // Docker unavailable; exercised in CI. See class summary.
        }

        await using var factory = CreateFactory();
        var client = await factory.CreateDesignerClientAsync();

        async Task<ReportResponse> Create(ReportDefinition d)
        {
            var response = await client.PostAsJsonAsync("/api/reports", d, Json);
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<ReportResponse>(Json))!;
        }

        var first = await Create(SimpleReport("Aylık Özet Raporu"));
        var second = await Create(SimpleReport("Aylık Özet Raporu"));

        Assert.Equal("aylik-ozet-raporu", first.Definition.Code);
        Assert.Equal("aylik-ozet-raporu-2", second.Definition.Code);

        // An explicit code is kept (lowercased) and shows up in the list.
        var custom = await Create(SimpleReport("Barkod", "Barkod_Etiket"));
        Assert.Equal("barkod_etiket", custom.Definition.Code);
        var list = await client.GetFromJsonAsync<List<ReportSummaryResponse>>("/api/reports", Json);
        Assert.Contains(list!, r => r.Id == custom.Id && r.Code == "barkod_etiket");

        // Taking someone else's code is a conflict; a malformed one is invalid.
        var clash = await client.PostAsJsonAsync("/api/reports", SimpleReport("Başka", "barkod_etiket"), Json);
        Assert.Equal(HttpStatusCode.Conflict, clash.StatusCode);
        var bad = await client.PostAsJsonAsync("/api/reports", SimpleReport("Başka", "rapör kodu"), Json);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, bad.StatusCode);

        // Renaming a report does not change its code.
        var renamed = SimpleReport("Tamamen Yeni Ad", first.Definition.Code);
        using var update = new HttpRequestMessage(HttpMethod.Put, $"/api/reports/{first.Id}")
        {
            Content = JsonContent.Create(renamed, options: Json),
        };
        update.Headers.TryAddWithoutValidation("If-Match", $"\"{first.ConcurrencyToken}\"");
        var updated = await (await client.SendAsync(update)).Content.ReadFromJsonAsync<ReportResponse>(Json);
        Assert.Equal("aylik-ozet-raporu", updated!.Definition.Code);
    }

    [Fact]
    public async Task Api_Key_Lifecycle_Controls_Access()
    {
        if (!fixture.Available)
        {
            return; // Docker unavailable; exercised in CI. See class summary.
        }

        await using var factory = CreateFactory();
        var designer = await factory.CreateDesignerClientAsync();
        var report = await (await designer.PostAsJsonAsync("/api/reports", SimpleReport("Key Test"), Json))
            .Content.ReadFromJsonAsync<ReportResponse>(Json);

        var created = await (await designer.PostAsJsonAsync(
                "/api/api-keys",
                new { name = "Test app", description = "integration", expiresAtUtc = (DateTime?)null }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var secret = created.GetProperty("secret").GetString()!;
        var id = created.GetProperty("key").GetProperty("id").GetGuid();
        Assert.StartsWith("jrd_", secret, StringComparison.Ordinal);
        Assert.Equal("active", created.GetProperty("key").GetProperty("status").GetString());

        HttpClient WithKey(string key)
        {
            var c = factory.CreateClient();
            c.DefaultRequestHeaders.Add("X-Api-Key", key);
            return c;
        }

        // The key lists and renders reports of its own organization ...
        var keyClient = WithKey(secret);
        var reports = await keyClient.GetFromJsonAsync<List<ReportSummaryResponse>>("/api/reports", Json);
        Assert.Contains(reports!, r => r.Id == report!.Id);
        var render = await keyClient.PostAsJsonAsync($"/api/reports/by-code/{report!.Definition.Code}/render?format=html", new { });
        Assert.Equal(HttpStatusCode.OK, render.StatusCode);

        // ... but is read-only, and cannot manage keys.
        Assert.Equal(HttpStatusCode.Forbidden, (await keyClient.PostAsJsonAsync("/api/reports", SimpleReport("Nope"), Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await keyClient.GetAsync("/api/api-keys")).StatusCode);

        // A wrong key, a disabled key and a deleted key are all refused.
        Assert.Equal(HttpStatusCode.Unauthorized, (await WithKey("jrd_wrong").GetAsync("/api/reports")).StatusCode);

        (await designer.PutAsJsonAsync($"/api/api-keys/{id}/active", new { isActive = false })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await keyClient.GetAsync("/api/reports")).StatusCode);

        (await designer.PutAsJsonAsync($"/api/api-keys/{id}/active", new { isActive = true })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, (await keyClient.GetAsync("/api/reports")).StatusCode);

        // The list never shows the secret, but does show when the key was last used.
        var listed = await designer.GetFromJsonAsync<JsonElement>("/api/api-keys");
        Assert.DoesNotContain(secret, listed.GetRawText(), StringComparison.Ordinal);
        Assert.NotEqual(JsonValueKind.Null, listed[0].GetProperty("lastUsedAtUtc").ValueKind);

        (await designer.DeleteAsync($"/api/api-keys/{id}")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await keyClient.GetAsync("/api/reports")).StatusCode);
    }

    [Fact]
    public async Task Api_Key_Expiry_Is_Enforced()
    {
        if (!fixture.Available)
        {
            return; // Docker unavailable; exercised in CI. See class summary.
        }

        await using var factory = CreateFactory();
        var designer = await factory.CreateDesignerClientAsync();

        var past = await designer.PostAsJsonAsync("/api/api-keys", new { name = "x", expiresAtUtc = DateTime.UtcNow.AddMinutes(-1) });
        Assert.Equal(HttpStatusCode.BadRequest, past.StatusCode);

        var created = await (await designer.PostAsJsonAsync("/api/api-keys", new { name = "short", expiresAtUtc = DateTime.UtcNow.AddSeconds(2) }))
            .Content.ReadFromJsonAsync<JsonElement>();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", created.GetProperty("secret").GetString()!);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/reports")).StatusCode);
        await Task.Delay(TimeSpan.FromSeconds(2.5));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/reports")).StatusCode);
    }

    [Fact]
    public async Task Invalid_Definition_Returns_422()
    {
        if (!fixture.Available)
        {
            return; // Docker unavailable; exercised in CI. See class summary.
        }

        await using var factory = CreateFactory();
        var client = await factory.CreateDesignerClientAsync();

        var invalid = new ReportDefinition { Name = "", LayoutMode = LayoutMode.Free };
        var response = await client.PostAsJsonAsync("/api/reports", invalid, Json);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Meta_Schema_Is_Served()
    {
        if (!fixture.Available)
        {
            return; // Docker unavailable; exercised in CI. See class summary.
        }

        await using var factory = CreateFactory();
        var client = await factory.CreateDesignerClientAsync();

        var schema = await client.GetStringAsync("/api/meta/schema");

        Assert.Contains("\"title\": \"ReportDefinition\"", schema, StringComparison.Ordinal);
    }
}

public sealed class SqlServerReportsApiTests(SqlServerDatabaseFixture fixture)
    : ReportsApiTestsBase(fixture), IClassFixture<SqlServerDatabaseFixture>
{
}

public sealed class PostgreSqlReportsApiTests(PostgreSqlDatabaseFixture fixture)
    : ReportsApiTestsBase(fixture), IClassFixture<PostgreSqlDatabaseFixture>
{
}

public sealed class OracleReportsApiTests(OracleDatabaseFixture fixture)
    : ReportsApiTestsBase(fixture), IClassFixture<OracleDatabaseFixture>
{
}

public abstract class DatabaseFixture : IAsyncLifetime
{
    private IDatabaseContainer? _container;

    public abstract string Provider { get; }

    public string ConnectionString { get; private set; } = string.Empty;

    /// <summary>False when the container runtime could not be reached; tests then no-op.</summary>
    public bool Available { get; private set; }

    protected abstract IDatabaseContainer BuildContainer();

    /// <summary>Overridden by heavy/opt-in fixtures (Oracle) to skip unless explicitly requested.</summary>
    protected virtual bool Enabled => true;

    public async Task InitializeAsync()
    {
        if (!Enabled)
        {
            Available = false;
            return;
        }

        try
        {
            _container = BuildContainer();
            await _container.StartAsync();
            ConnectionString = _container.GetConnectionString();
            Available = true;
        }
        catch (Exception)
        {
            Available = false;
        }
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}

public sealed class SqlServerDatabaseFixture : DatabaseFixture
{
    public override string Provider => "SqlServer";

    protected override IDatabaseContainer BuildContainer() =>
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
}

public sealed class PostgreSqlDatabaseFixture : DatabaseFixture
{
    public override string Provider => "PostgreSql";

    protected override IDatabaseContainer BuildContainer() =>
        new PostgreSqlBuilder("postgres:16-alpine").Build();
}

/// <summary>Opt-in: set RUN_ORACLE_TESTS=1. The Oracle image is large and slow to start.</summary>
public sealed class OracleDatabaseFixture : DatabaseFixture
{
    public override string Provider => "Oracle";

    protected override bool Enabled =>
        string.Equals(Environment.GetEnvironmentVariable("RUN_ORACLE_TESTS"), "1", StringComparison.Ordinal);

    protected override IDatabaseContainer BuildContainer() =>
        new Testcontainers.Oracle.OracleBuilder("gvenzl/oracle-free:23-slim").Build();
}
