using System.Globalization;
using System.Text;
using JetReportDesigner.Core.Model;
using JetReportDesigner.DataSources;
using JetReportDesigner.DataSources.Json;
using JetReportDesigner.Rendering;
using JetReportDesigner.Rendering.Engines;

namespace JetReportDesigner.Rendering.Tests;

public class TableElementTests
{
    static TableElementTests()
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
    }

    private const string Json = """
        [ { "customer": "Acme", "total": 100 },
          { "customer": "Borg", "total": 250 },
          { "customer": "Cyberdyne", "total": 75 } ]
        """;

    private static ReportElement Table() => new()
    {
        Id = "t",
        Type = ElementType.Table,
        Bounds = new Bounds { X = 40, Y = 40, Width = 400, Height = 120 },
        Style = new ReportStyle { Border = new BorderSpec { Top = 1, Right = 1, Bottom = 1, Left = 1, Color = "#cccccc" } },
        Table = new TableSpec
        {
            DataSource = "orders",
            ShowHeader = true,
            Columns =
            [
                new TableColumn { Header = "Customer", Value = "{orders.customer}", Width = 220, Align = TextAlign.Left },
                new TableColumn { Header = "Total", Value = "{orders.total}", Width = 120, Align = TextAlign.Right, Format = "n2" },
            ],
        },
    };

    private static ReportDefinition FreeReportWithTable() => new()
    {
        Name = "Table",
        LayoutMode = LayoutMode.Free,
        DataSources =
        [
            new DataSourceDefinition
            {
                Name = "orders",
                Kind = DataSourceKind.Json,
                Json = new JsonSourceConfig { InlineData = Json, ResultPath = "$" },
            },
        ],
        Body = new ReportBody { Height = 1000, Elements = [Table()] },
    };

    private static ReportRenderService Service() =>
        new(new ReportDataResolver([new JsonDataSourceReader()]), new MigraDocPdfRenderer());

    [Fact]
    public async Task Table_Renders_Header_Plus_One_Row_Per_Data_Row()
    {
        var result = await Service().RenderAsync(FreeReportWithTable(), null, RenderFormat.Html, CancellationToken.None);
        var html = Encoding.UTF8.GetString(result.Content);

        Assert.Contains("Customer", html, StringComparison.Ordinal);
        Assert.Contains("Acme", html, StringComparison.Ordinal);
        Assert.Contains("Borg", html, StringComparison.Ordinal);
        Assert.Contains("Cyberdyne", html, StringComparison.Ordinal);
        Assert.Contains("250.00", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Table_Works_In_A_Banded_Header_Band()
    {
        var report = new ReportDefinition
        {
            Name = "Banded table",
            LayoutMode = LayoutMode.Banded,
            DataSources = FreeReportWithTable().DataSources,
            Bands =
            [
                new Band { Type = BandType.ReportHeader, Height = 200, Elements = [Table()] },
                new Band { Type = BandType.PageFooter, Height = 20, Elements = [] },
            ],
        };

        var result = await Service().RenderAsync(report, null, RenderFormat.Pdf, CancellationToken.None);
        Assert.Equal("%PDF-"u8.ToArray(), result.Content.AsSpan(0, 5).ToArray());
    }
}
