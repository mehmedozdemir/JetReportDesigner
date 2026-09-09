using System.Text;
using JetReportDesigner.Core.Model;
using JetReportDesigner.DataSources;
using JetReportDesigner.DataSources.Json;
using JetReportDesigner.Rendering;
using JetReportDesigner.Rendering.Engines;
using JetReportDesigner.Rendering.Layout;

namespace JetReportDesigner.Rendering.Tests;

public class ChartTests
{
    private const string SalesJson =
        """{ "data": [ {"m":"Jan","v":10}, {"m":"Feb","v":25}, {"m":"Mar","v":18} ] }""";

    private static ReportDefinition Report(string chartType) => new()
    {
        Name = "chart",
        LayoutMode = LayoutMode.Free,
        DataSources =
        [
            new DataSourceDefinition
            {
                Name = "sales",
                Kind = DataSourceKind.Json,
                Json = new JsonSourceConfig { InlineData = SalesJson, ResultPath = "$.data" },
            },
        ],
        Body = new ReportBody
        {
            Elements =
            [
                new ReportElement
                {
                    Id = "c", Type = ElementType.Chart,
                    Bounds = new Bounds { X = 20, Y = 20, Width = 320, Height = 200 },
                    Chart = new ChartSpec
                    {
                        Type = chartType,
                        DataSource = "sales",
                        Category = "{sales.m}",
                        Title = "Sales",
                        Series = [new ChartSeries { Name = "Volume", Value = "{sales.v}" }],
                    },
                },
            ],
        },
    };

    private static ReportData Data(ReportDefinition r) =>
        new ReportDataResolver([new JsonDataSourceReader()])
            .ResolveAsync(r, new Dictionary<string, object?>(), CancellationToken.None)
            .GetAwaiter().GetResult();

    [Fact]
    public void Column_chart_emits_a_bar_rectangle_per_category()
    {
        var report = Report("column");
        report.Body!.Elements[0].Chart!.ShowLegend = false;
        var doc = new FreeLayoutBuilder().Build(report, Data(report), new Dictionary<string, object?>());
        var fills = doc.Pages.Single().Primitives
            .OfType<RectanglePrimitive>()
            .Where(r => r.FillColorHex is not null && r.BorderThicknessPx == 0)
            .ToList();

        Assert.Equal(3, fills.Count);
        Assert.Contains(doc.Pages.Single().Primitives.OfType<TextPrimitive>(), t => t.Text == "Sales");
        Assert.Contains(doc.Pages.Single().Primitives.OfType<TextPrimitive>(), t => t.Text == "Feb");
    }

    [Fact]
    public void Pie_chart_emits_a_wedge_per_slice_summing_to_360()
    {
        var report = Report("pie");
        var doc = new FreeLayoutBuilder().Build(report, Data(report), new Dictionary<string, object?>());
        var wedges = doc.Pages.Single().Primitives.OfType<WedgePrimitive>().ToList();

        Assert.Equal(3, wedges.Count);
        Assert.Equal(360, wedges.Sum(w => w.SweepAngleDeg), 1);
    }

    [Theory]
    [InlineData("column")]
    [InlineData("bar")]
    [InlineData("line")]
    [InlineData("area")]
    [InlineData("pie")]
    public void Every_chart_type_renders_to_pdf_and_html(string type)
    {
        var report = Report(type);
        var doc = new FreeLayoutBuilder().Build(report, Data(report), new Dictionary<string, object?>());

        var pdf = new MigraDocPdfRenderer().Render(doc);
        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));

        var html = new HtmlReportRenderer().Render(doc);
        Assert.Contains("Sales", html, StringComparison.Ordinal);
        if (type is "area" or "pie")
        {
            Assert.Contains("<svg", html, StringComparison.Ordinal);
        }
    }
}
