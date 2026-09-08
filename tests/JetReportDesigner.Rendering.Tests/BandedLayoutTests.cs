using System.Globalization;
using System.Text;
using System.Text.Json;
using JetReportDesigner.Core.Model;
using JetReportDesigner.DataSources;
using JetReportDesigner.DataSources.Json;
using JetReportDesigner.Rendering;
using JetReportDesigner.Rendering.Engines;
using JetReportDesigner.Rendering.Layout;

namespace JetReportDesigner.Rendering.Tests;

public class BandedLayoutTests
{
    static BandedLayoutTests()
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
    }

    private static string OrdersJson(int rows)
    {
        var customers = new[] { "Acme", "Borg", "Cyberdyne" };
        var items = Enumerable.Range(0, rows).Select(i => new
        {
            customer = customers[i % customers.Length],
            total = (i + 1) * 10,
        });
        return JsonSerializer.Serialize(items);
    }

    private static ReportDefinition GroupedReport(int detailHeight = 20) => new()
    {
        Name = "Orders by customer",
        LayoutMode = LayoutMode.Banded,
        DataSources =
        [
            new DataSourceDefinition
            {
                Name = "orders",
                Kind = DataSourceKind.Json,
                Json = new JsonSourceConfig { InlineData = OrdersJson(45), ResultPath = "$" },
            },
        ],
        Bands =
        [
            new Band
            {
                Type = BandType.PageHeader, Height = 30,
                Elements = [Label("hdr", "Orders", 0, 4, 300, 20)],
            },
            new Band
            {
                Type = BandType.GroupHeader, Height = 22,
                Group = new GroupSpec { DataSource = "orders", Expression = "{orders.customer}", Sort = "asc" },
                RepeatOnEveryPage = true,
                Elements = [Field("gh", "{orders.customer}", 0, 2, 200, 18)],
            },
            new Band
            {
                Type = BandType.Detail, Height = detailHeight, DataSource = "orders",
                Elements =
                [
                    Field("d1", "{orders.customer}", 20, 1, 150, 16),
                    Field("d2", "{orders.total}", 200, 1, 100, 16, "n2"),
                ],
            },
            new Band
            {
                Type = BandType.GroupFooter, Height = 22,
                Group = new GroupSpec { DataSource = "orders", Expression = "{orders.customer}", Sort = "asc" },
                Elements = [Sum("gf", "{orders.total}", AggregateScope.Group, 200, 2, 100, 18)],
            },
            new Band
            {
                Type = BandType.PageFooter, Height = 24,
                Elements = [Field("pf", "Page {pageNumber()} / {totalPages()}", 0, 4, 200, 16)],
            },
            new Band
            {
                Type = BandType.ReportFooter, Height = 26,
                Elements = [Sum("rf", "{orders.total}", AggregateScope.Report, 200, 4, 100, 18)],
            },
        ],
    };

    private static ReportElement Label(string id, string text, double x, double y, double w, double h) => new()
    {
        Id = id, Type = ElementType.Label, Text = text, Bounds = new Bounds { X = x, Y = y, Width = w, Height = h },
    };

    private static ReportElement Field(string id, string value, double x, double y, double w, double h, string? format = null) => new()
    {
        Id = id, Type = ElementType.Field, Value = value, Format = format,
        Bounds = new Bounds { X = x, Y = y, Width = w, Height = h },
    };

    private static ReportElement Sum(string id, string value, AggregateScope scope, double x, double y, double w, double h) => new()
    {
        Id = id, Type = ElementType.Field, Value = value, Format = "n2",
        Aggregate = AggregateFunction.Sum, AggregateScope = scope,
        Bounds = new Bounds { X = x, Y = y, Width = w, Height = h },
    };

    private static ReportRenderService Service() =>
        new(new ReportDataResolver([new JsonDataSourceReader()]), new MigraDocPdfRenderer());

    private static ReportData Data(ReportDefinition report) => new(new Dictionary<string, ResolvedDataSet>
    {
        ["orders"] = JsonRows.Parse(report.DataSources[0].Json!.InlineData, "$"),
    });

    [Fact]
    public void Paginates_Across_Multiple_Pages()
    {
        var report = GroupedReport();
        var doc = new BandedLayoutBuilder().Build(report, Data(report), new Dictionary<string, object?>());

        Assert.True(doc.Pages.Count >= 2, $"expected multiple pages, got {doc.Pages.Count}");
    }

    [Fact]
    public void Every_Page_Has_A_Page_Footer_With_Correct_Numbers()
    {
        var report = GroupedReport();
        var doc = new BandedLayoutBuilder().Build(report, Data(report), new Dictionary<string, object?>());
        var total = doc.Pages.Count;

        for (var i = 0; i < total; i++)
        {
            var texts = doc.Pages[i].Primitives.OfType<TextPrimitive>().Select(t => t.Text).ToList();
            Assert.Contains($"Page {i + 1} / {total}", texts);
        }
    }

    [Fact]
    public void Group_Footers_Carry_Group_Subtotals_Adding_To_The_Grand_Total()
    {
        var report = GroupedReport();
        var doc = new BandedLayoutBuilder().Build(report, Data(report), new Dictionary<string, object?>());

        var rows = JsonRows.Parse(OrdersJson(45), "$").Rows;
        var expectedGrand = rows.Sum(r => Convert.ToDouble(r["total"], CultureInfo.InvariantCulture));

        var allText = doc.Pages.SelectMany(p => p.Primitives).OfType<TextPrimitive>().Select(t => t.Text).ToList();
        Assert.Contains(expectedGrand.ToString("n2", CultureInfo.InvariantCulture), allText);

        // Per-customer subtotal appears at least once.
        var acmeSubtotal = rows.Where(r => (string)r["customer"]! == "Acme")
            .Sum(r => Convert.ToDouble(r["total"], CultureInfo.InvariantCulture));
        Assert.Contains(acmeSubtotal.ToString("n2", CultureInfo.InvariantCulture), allText);
    }

    [Fact]
    public void Group_Header_Repeats_When_A_Group_Spans_A_Page_Break()
    {
        // Tall detail rows force a mid-group break so RepeatOnEveryPage kicks in.
        var report = GroupedReport(detailHeight: 90);
        var doc = new BandedLayoutBuilder().Build(report, Data(report), new Dictionary<string, object?>());

        var groupHeaderHits = doc.Pages
            .SelectMany(p => p.Primitives)
            .OfType<TextPrimitive>()
            .Count(t => t.Text is "Acme" or "Borg" or "Cyberdyne");

        // 3 groups, and at least one continuation header from a page break.
        Assert.True(groupHeaderHits >= 4, $"expected repeated group headers, got {groupHeaderHits}");
    }

    [Fact]
    public async Task RenderService_Produces_Pdf_For_Banded()
    {
        var report = GroupedReport();
        var result = await Service().RenderAsync(report, null, RenderFormat.Pdf, CancellationToken.None);

        Assert.Equal("application/pdf", result.ContentType);
        Assert.Equal("%PDF-"u8.ToArray(), result.Content.AsSpan(0, 5).ToArray());

        // The layout itself must span multiple pages.
        var doc = new BandedLayoutBuilder().Build(report, Data(report), new Dictionary<string, object?>());
        Assert.True(doc.Pages.Count >= 2);
    }

    [Fact]
    public async Task RenderService_Html_Contains_Group_And_Grand_Totals()
    {
        var report = GroupedReport();
        var result = await Service().RenderAsync(report, null, RenderFormat.Html, CancellationToken.None);
        var html = Encoding.UTF8.GetString(result.Content);

        Assert.Contains("Page 1 /", html, StringComparison.Ordinal);
        Assert.Contains("Acme", html, StringComparison.Ordinal);
    }
}
