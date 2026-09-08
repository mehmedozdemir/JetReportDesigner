using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JetReportDesigner.Core.Model;
using JetReportDesigner.Core.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace JetReportDesigner.Api.Tests;

public sealed class RenderConcurrencyTests(SqlServerDatabaseFixture fixture) : IClassFixture<SqlServerDatabaseFixture>
{
    private static readonly JsonSerializerOptions Json = ReportJson.Apply(new JsonSerializerOptions());

    [Fact]
    public async Task Parallel_Renders_All_Succeed()
    {
        if (!fixture.Available)
        {
            return;
        }

        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Production");
            b.UseSetting("Storage:Provider", fixture.Provider);
            b.UseSetting("Storage:ConnectionString", fixture.ConnectionString);
            b.UseSetting("Storage:MigrateOnStartup", "true");
        });
        var client = factory.CreateClient();

        var definition = new ReportDefinition
        {
            Name = "Concurrency",
            LayoutMode = LayoutMode.Free,
            DataSources =
            [
                new DataSourceDefinition
                {
                    Name = "d",
                    Kind = DataSourceKind.Json,
                    Json = new JsonSourceConfig { InlineData = "[{\"n\":1},{\"n\":2},{\"n\":3}]", ResultPath = "$" },
                },
            ],
            Body = new ReportBody
            {
                Height = 800,
                Elements =
                [
                    new ReportElement { Id = "t", Type = ElementType.Label, Text = "Load", Bounds = new Bounds { X = 40, Y = 40, Width = 200, Height = 20 } },
                    new ReportElement { Id = "f", Type = ElementType.Field, Value = "{d.n}", Bounds = new Bounds { X = 40, Y = 70, Width = 200, Height = 16 } },
                ],
            },
        };

        var body = new { definition, parameters = new Dictionary<string, object?>() };

        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(async _ =>
        {
            var response = await client.PostAsJsonAsync("/api/render?format=pdf", body, Json);
            var bytes = await response.Content.ReadAsByteArrayAsync();
            return (response.StatusCode, IsPdf: bytes.Length > 400 && bytes[0] == (byte)'%');
        }));

        Assert.All(results, r =>
        {
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
            Assert.True(r.IsPdf);
        });
    }
}
