using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using JetReportDesigner.Client;

namespace JetReportDesigner.Client.Tests;

public class JetReportClientTests
{
    private static readonly Guid BarkodId = Guid.Parse("63a34fb2-9bc4-4d4b-b4e8-300bfaa36830");

    private sealed class FakeServer : HttpMessageHandler
    {
        public List<(HttpRequestMessage Request, string Body)> Calls { get; } = [];

        public int ListCalls => Calls.Count(c => c.Request.RequestUri!.AbsolutePath == "/api/reports");

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(ct);
            Calls.Add((request, body));

            if (request.RequestUri!.AbsolutePath == "/api/reports")
            {
                return Json($"[{{\"id\":\"{BarkodId}\",\"code\":\"barkod-rapor\",\"name\":\"Barkod Rapor\"}},{{\"id\":\"{Guid.NewGuid()}\",\"name\":\"Dup\"}},{{\"id\":\"{Guid.NewGuid()}\",\"name\":\"dup\"}}]");
            }

            if (request.RequestUri.Query.Contains("page=9", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent("{\"title\":\"Page 9 is out of range\"}", Encoding.UTF8, "application/problem+json"),
                };
            }

            var ok = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3]) };
            ok.Content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            ok.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment") { FileName = "r-1.png" };
            ok.Headers.Add("X-Page-Count", "3");
            return ok;
        }

        private static HttpResponseMessage Json(string json) =>
            new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }

    private static (JetReportClient Client, FakeServer Server) Make()
    {
        var server = new FakeServer();
        var client = new JetReportClient(
            new JetReportClientOptions { BaseUrl = "http://reports.local:8081/", ApiKey = "k" },
            new HttpClient(server));
        return (client, server);
    }

    [Fact]
    public async Task Renders_by_name_sending_the_key_and_the_rows_exactly_as_named()
    {
        var (client, server) = Make();
        var data = new ReportData().Add("data", new[] { new { Adi = "Ahmet", Barkod = "2750365698456" } });

        var result = await client.RenderAsync("barkod rapor", data, format: ReportFormat.Png, page: 2, dpi: 300);

        var render = server.Calls.Last();
        Assert.Equal($"/api/reports/{BarkodId}/render", render.Request.RequestUri!.AbsolutePath);
        Assert.Equal("?format=png&page=2&dpi=300", render.Request.RequestUri.Query);
        Assert.Equal("k", render.Request.Headers.GetValues("X-Api-Key").Single());
        using var doc = JsonDocument.Parse(render.Body);
        Assert.Equal("Ahmet", doc.RootElement.GetProperty("data").GetProperty("data")[0].GetProperty("Adi").GetString());
        Assert.Equal(3, result.PageCount);
        Assert.Equal("image/png", result.ContentType);
        Assert.Equal("r-1.png", result.FileName);
    }

    [Theory]
    [InlineData("barkod-rapor")]
    [InlineData("  Barkod-Rapor ")]
    [InlineData("barkod rapor")]
    public async Task Code_is_the_preferred_handle_and_the_display_name_still_works(string handle)
    {
        var (client, server) = Make();

        await client.RenderAsync(handle);

        Assert.Equal($"/api/reports/{BarkodId}/render", server.Calls.Last().Request.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task Report_list_is_fetched_once_and_a_guid_skips_it_entirely()
    {
        var (client, server) = Make();

        await client.RenderAsync("Barkod Rapor");
        await client.RenderAsync("Barkod Rapor");
        await client.RenderAsync(BarkodId.ToString());

        Assert.Equal(1, server.ListCalls);
    }

    [Fact]
    public async Task Unknown_and_ambiguous_names_explain_themselves()
    {
        var (client, _) = Make();

        var missing = await Assert.ThrowsAsync<ReportClientException>(() => client.RenderAsync("Yok"));
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        var dup = await Assert.ThrowsAsync<ReportClientException>(() => client.RenderAsync("Dup"));
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);
    }

    [Fact]
    public async Task Server_problem_titles_become_the_exception_message()
    {
        var (client, _) = Make();

        var ex = await Assert.ThrowsAsync<ReportClientException>(() => client.RenderAsync("Barkod Rapor", page: 9));

        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
        Assert.Contains("out of range", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Render_pages_fetches_every_page()
    {
        var (client, server) = Make();

        var pages = await client.RenderPagesAsync("Barkod Rapor");

        Assert.Equal(3, pages.Count);
        Assert.Equal(3, server.Calls.Count(c => c.Request.RequestUri!.AbsolutePath.EndsWith("/render", StringComparison.Ordinal)));
    }

    [Fact]
    public void DataTable_rows_keep_column_names_and_turn_dbnull_into_null()
    {
        var table = new System.Data.DataTable();
        table.Columns.Add("TeslimIl", typeof(string));
        table.Columns.Add("Adet", typeof(int));
        table.Rows.Add(DBNull.Value, 3);

        var data = new ReportData().Add("data", table);

        var row = data.Sources["data"][0];
        Assert.Equal(JsonValueKind.Null, row.GetProperty("TeslimIl").ValueKind);
        Assert.Equal(3, row.GetProperty("Adet").GetInt32());
    }
}
