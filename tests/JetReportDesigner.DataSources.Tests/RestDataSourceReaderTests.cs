using System.Net;
using JetReportDesigner.Core.Model;
using JetReportDesigner.DataSources;
using JetReportDesigner.DataSources.Http;

namespace JetReportDesigner.DataSources.Tests;

public class RestDataSourceReaderTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(responder(request));
        }
    }

    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json"),
    };

    private static DataSourceDefinition RestSource(string url, Dictionary<string, string>? query = null, string resultPath = "$") => new()
    {
        Name = "orders",
        Kind = DataSourceKind.Rest,
        Rest = new RestSourceConfig
        {
            Url = url,
            Method = "GET",
            Query = query ?? [],
            ResultPath = resultPath,
        },
    };

    [Fact]
    public async Task Reads_Rows_From_ResultPath_And_Infers_Schema()
    {
        var stub = new StubHandler(_ => Json("""{ "data": { "items": [ { "id": 1, "name": "a" }, { "id": 2, "name": "b" } ] } }"""));
        var reader = new RestDataSourceReader(new HttpClient(stub), new SsrfGuard());

        var set = await reader.ReadAsync(
            RestSource("https://api.example.com/orders", resultPath: "$.data.items"),
            new Dictionary<string, object?>(),
            TestContext());

        Assert.Equal(2, set.Rows.Count);
        Assert.Equal("a", set.Rows[0]["name"]);
        Assert.Equal(FieldType.Number, set.Fields.Single(f => f.Name == "id").Type);
    }

    [Fact]
    public async Task Substitutes_Parameters_In_Url_And_Query()
    {
        var stub = new StubHandler(_ => Json("[]"));
        var reader = new RestDataSourceReader(new HttpClient(stub), new SsrfGuard());

        await reader.ReadAsync(
            RestSource("https://api.example.com/{param:tenant}/orders", new Dictionary<string, string> { ["from"] = "{param:from}" }),
            new Dictionary<string, object?> { ["tenant"] = "acme", ["from"] = "2026-01-01" },
            TestContext());

        var uri = stub.LastRequest!.RequestUri!;
        Assert.Equal("/acme/orders", uri.AbsolutePath);
        Assert.Contains("from=2026-01-01", uri.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rejects_NonHttp_Url_Before_Sending()
    {
        var stub = new StubHandler(_ => Json("[]"));
        var reader = new RestDataSourceReader(new HttpClient(stub), new SsrfGuard());

        await Assert.ThrowsAsync<SsrfBlockedException>(() =>
            reader.ReadAsync(RestSource("file:///etc/passwd"), new Dictionary<string, object?>(), TestContext()));
        Assert.Null(stub.LastRequest);
    }

    [Fact]
    public async Task Throws_On_Http_Error_Status()
    {
        var stub = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var reader = new RestDataSourceReader(new HttpClient(stub), new SsrfGuard());

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            reader.ReadAsync(RestSource("https://api.example.com/x"), new Dictionary<string, object?>(), TestContext()));
    }

    private static CancellationToken TestContext() => CancellationToken.None;
}
