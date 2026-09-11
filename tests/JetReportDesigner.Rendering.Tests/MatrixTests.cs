using JetReportDesigner.Core.Model;
using JetReportDesigner.DataSources;
using JetReportDesigner.DataSources.Json;
using JetReportDesigner.Rendering.Layout;

namespace JetReportDesigner.Rendering.Tests;

public class MatrixTests
{
    private const string SalesJson =
        """
        { "data": [
          { "region": "West",  "quarter": "Q1", "amount": 100 },
          { "region": "West",  "quarter": "Q2", "amount": 50 },
          { "region": "East",  "quarter": "Q1", "amount": 30 },
          { "region": "East",  "quarter": "Q1", "amount": 20 },
          { "region": "East",  "quarter": "Q2", "amount": 10 }
        ]}
        """;

    private static ReportDefinition Report() => new()
    {
        Name = "pivot",
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
                    Id = "m", Type = ElementType.Matrix,
                    Bounds = new Bounds { X = 0, Y = 0, Width = 400, Height = 200 },
                    Matrix = new MatrixSpec
                    {
                        DataSource = "sales",
                        RowField = "{sales.region}",
                        ColumnField = "{sales.quarter}",
                        ValueField = "{sales.amount}",
                        Aggregate = AggregateFunction.Sum,
                    },
                },
            ],
        },
    };

    private static ReportData Data(ReportDefinition report) =>
        new ReportDataResolver([new JsonDataSourceReader()])
            .ResolveAsync(report, new Dictionary<string, object?>(), CancellationToken.None)
            .GetAwaiter().GetResult();

    [Fact]
    public void Pivots_rows_by_region_and_columns_by_quarter_summing_amount()
    {
        var report = Report();
        var doc = new FreeLayoutBuilder().Build(report, Data(report), new Dictionary<string, object?>());
        var texts = doc.Pages.Single().Primitives.OfType<TextPrimitive>().Select(t => t.Text).ToList();

        // Header row: row-header label, "Q1", "Q2", "Total"
        Assert.Contains("Q1", texts);
        Assert.Contains("Q2", texts);
        // Row headers
        Assert.Contains("East", texts);
        Assert.Contains("West", texts);
        // East/Q1 = 30 + 20 = 50; West/Q1 = 100; West/Q2 = 50; East total = 60
        Assert.Contains("50", texts);
        Assert.Contains("100", texts);
        Assert.Contains("60", texts);
        // Grand total across everything = 210
        Assert.Contains("210", texts);
    }

    [Fact]
    public void Empty_data_emits_nothing()
    {
        var report = Report();
        report.DataSources[0].Json!.InlineData = "{\"data\":[]}";
        var doc = new FreeLayoutBuilder().Build(report, Data(report), new Dictionary<string, object?>());

        Assert.Empty(doc.Pages.Single().Primitives);
    }
}
