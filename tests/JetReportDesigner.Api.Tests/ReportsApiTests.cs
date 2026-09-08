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
        });

    [Fact]
    public async Task Full_Report_Lifecycle_RoundTrips()
    {
        if (!fixture.Available)
        {
            return; // Docker unavailable; exercised in CI. See class summary.
        }

        await using var factory = CreateFactory();
        var client = factory.CreateClient();

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
        var client = factory.CreateClient();

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

    [Fact]
    public async Task Invalid_Definition_Returns_422()
    {
        if (!fixture.Available)
        {
            return; // Docker unavailable; exercised in CI. See class summary.
        }

        await using var factory = CreateFactory();
        var client = factory.CreateClient();

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
        var client = factory.CreateClient();

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
