using ClosedXML.Excel;
using JetReportDesigner.Core.Model;
using JetReportDesigner.DataSources;
using JetReportDesigner.DataSources.Json;
using JetReportDesigner.Rendering;
using JetReportDesigner.Rendering.Engines;

namespace JetReportDesigner.Rendering.Tests;

public class XlsxExportTests
{
    private const string OrdersJson =
        """{ "data": [ { "customer": "Acme", "total": 1250.5 }, { "customer": "Globex", "total": 90 } ] }""";

    private static ReportData Data(ReportDefinition report) =>
        new ReportDataResolver([new JsonDataSourceReader()])
            .ResolveAsync(report, new Dictionary<string, object?>(), CancellationToken.None)
            .GetAwaiter().GetResult();

    private static DataSourceDefinition OrdersSource() => new()
    {
        Name = "orders",
        Kind = DataSourceKind.Json,
        Json = new JsonSourceConfig { InlineData = OrdersJson, ResultPath = "$.data" },
    };

    [Fact]
    public void Table_element_becomes_a_sheet_with_typed_numbers()
    {
        var report = new ReportDefinition
        {
            Name = "orders report",
            LayoutMode = LayoutMode.Free,
            DataSources = [OrdersSource()],
            Body = new ReportBody
            {
                Elements =
                [
                    new ReportElement
                    {
                        Id = "t", Type = ElementType.Table,
                        Bounds = new Bounds { Width = 300, Height = 100 },
                        Table = new TableSpec
                        {
                            DataSource = "orders",
                            ShowHeader = true,
                            Columns =
                            [
                                new TableColumn { Header = "Customer", Value = "{orders.customer}", Width = 160 },
                                new TableColumn { Header = "Total", Value = "{orders.total}", Width = 100, Format = "n2" },
                            ],
                        },
                    },
                ],
            },
        };

        using var wb = new XLWorkbook(new MemoryStream(
            XlsxReportBuilder.Build(report, Data(report), new Dictionary<string, object?>())));

        var ws = wb.Worksheet("orders");
        Assert.Equal("Customer", ws.Cell(1, 1).GetString());
        Assert.Equal("Total", ws.Cell(1, 2).GetString());
        Assert.Equal("Acme", ws.Cell(2, 1).GetString());
        Assert.Equal(1250.5, ws.Cell(2, 2).GetDouble(), 3);
        Assert.Equal("#,##0.00", ws.Cell(2, 2).Style.NumberFormat.Format);
        Assert.Equal(3, ws.LastRowUsed()!.RowNumber());
    }

    [Fact]
    public void Free_layout_without_a_table_falls_back_to_a_field_value_sheet()
    {
        var report = new ReportDefinition
        {
            Name = "card",
            LayoutMode = LayoutMode.Free,
            Body = new ReportBody
            {
                Elements =
                [
                    new ReportElement
                    {
                        Id = "l", Type = ElementType.Label, Text = "CITY TRANSIT",
                        Bounds = new Bounds { X = 10, Y = 10, Width = 100, Height = 20 },
                    },
                ],
            },
        };

        using var wb = new XLWorkbook(new MemoryStream(
            XlsxReportBuilder.Build(report, Data(report), new Dictionary<string, object?>())));

        var ws = wb.Worksheet("Report");
        Assert.Equal("Field", ws.Cell(1, 1).GetString());
        Assert.Equal("CITY TRANSIT", ws.Cell(2, 2).GetString());
    }
}
