using System.Globalization;
using System.Text;
using JetReportDesigner.Core.Model;
using JetReportDesigner.DataSources;
using JetReportDesigner.DataSources.Json;
using JetReportDesigner.Rendering;
using JetReportDesigner.Rendering.Engines;
using JetReportDesigner.Rendering.Layout;

namespace JetReportDesigner.Rendering.Tests;

public class FreeLayoutPipelineTests
{
    static FreeLayoutPipelineTests()
    {
        // Number/date format assertions below assume the invariant culture.
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
    }

    private const string OrdersJson = """
        { "data": [
          { "customer": "Acme Ltd", "total": 1250.5, "orderDate": "2026-03-09" },
          { "customer": "Globex",   "total": 90,     "orderDate": "2026-03-10" }
        ]}
        """;

    private static ReportDefinition InvoiceReport() => new()
    {
        Name = "Invoice",
        LayoutMode = LayoutMode.Free,
        DataSources =
        [
            new DataSourceDefinition
            {
                Name = "orders",
                Kind = DataSourceKind.Json,
                Json = new JsonSourceConfig { InlineData = OrdersJson, ResultPath = "$.data" },
            },
        ],
        Body = new ReportBody
        {
            Height = 1000,
            Elements =
            [
                new ReportElement
                {
                    Id = "title", Type = ElementType.Label, Text = "INVOICE",
                    Bounds = new Bounds { X = 40, Y = 40, Width = 300, Height = 30 },
                    Style = new ReportStyle { Font = new FontSpec { Size = 20, Bold = true } },
                },
                new ReportElement
                {
                    Id = "cust", Type = ElementType.Field, Value = "{orders.customer}",
                    Bounds = new Bounds { X = 40, Y = 80, Width = 300, Height = 18 },
                },
                new ReportElement
                {
                    Id = "total", Type = ElementType.Field, Value = "{orders.total}", Format = "n2",
                    Bounds = new Bounds { X = 40, Y = 100, Width = 120, Height = 18 },
                    Style = new ReportStyle { Align = TextAlign.Right },
                },
                new ReportElement
                {
                    Id = "rule", Type = ElementType.Line,
                    Bounds = new Bounds { X = 40, Y = 74, Width = 515, Height = 0 },
                },
            ],
        },
    };

    private static ReportRenderService Service() =>
        new(new ReportDataResolver([new JsonDataSourceReader()]), new MigraDocPdfRenderer());

    [Fact]
    public void JsonRows_Parses_ResultPath_And_Infers_Types()
    {
        var set = JsonRows.Parse(OrdersJson, "$.data");

        Assert.Equal(2, set.Rows.Count);
        Assert.Equal("Acme Ltd", set.Rows[0]["customer"]);
        Assert.Equal(FieldType.Number, set.Fields.Single(f => f.Name == "total").Type);
        Assert.Equal(FieldType.Date, set.Fields.Single(f => f.Name == "orderDate").Type);
    }

    [Fact]
    public void FreeLayoutBuilder_Binds_First_Row_And_Emits_Primitives()
    {
        var data = new ReportData(new Dictionary<string, ResolvedDataSet>
        {
            ["orders"] = JsonRows.Parse(OrdersJson, "$.data"),
        });

        var doc = new FreeLayoutBuilder().Build(InvoiceReport(), data, new Dictionary<string, object?>());

        var page = Assert.Single(doc.Pages);
        var texts = page.Primitives.OfType<TextPrimitive>().ToList();
        Assert.Contains(texts, t => t.Text == "INVOICE" && t.Bold);
        Assert.Contains(texts, t => t.Text == "Acme Ltd");
        Assert.Contains(texts, t => t.Text == "1,250.50"); // n2, invariant test culture
        Assert.Contains(page.Primitives, p => p is LinePrimitive);
        Assert.Equal(794, doc.PageWidthPx); // A4 portrait
    }

    [Fact]
    public async Task RenderService_Produces_Pdf()
    {
        var result = await Service().RenderAsync(InvoiceReport(), null, RenderFormat.Pdf, CancellationToken.None);

        Assert.Equal("application/pdf", result.ContentType);
        Assert.Equal("%PDF-"u8.ToArray(), result.Content.AsSpan(0, 5).ToArray());
        Assert.EndsWith(".pdf", result.FileName);
    }

    [Fact]
    public async Task RenderService_Produces_Html_With_Bound_Values()
    {
        var result = await Service().RenderAsync(InvoiceReport(), null, RenderFormat.Html, CancellationToken.None);
        var html = Encoding.UTF8.GetString(result.Content);

        Assert.StartsWith("text/html", result.ContentType);
        Assert.Contains("INVOICE", html, StringComparison.Ordinal);
        Assert.Contains("Acme Ltd", html, StringComparison.Ordinal);
        Assert.Contains("1,250.50", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RenderService_Handles_Banded_Reports()
    {
        var banded = new ReportDefinition
        {
            Name = "b",
            LayoutMode = LayoutMode.Banded,
            Bands = [new Band { Type = BandType.ReportHeader, Height = 30, Elements = [] }],
        };

        var result = await Service().RenderAsync(banded, null, RenderFormat.Html, CancellationToken.None);

        Assert.StartsWith("text/html", result.ContentType);
    }
}
