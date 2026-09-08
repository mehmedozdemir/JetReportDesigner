using JetReportDesigner.Core.Model;
using JetReportDesigner.Core.Validation;

namespace JetReportDesigner.Core.Tests;

public class ReportInspectorTests
{
    private static ReportDefinition FreeReport(params ReportElement[] elements) => new()
    {
        Name = "r",
        LayoutMode = LayoutMode.Free,
        DataSources = [new DataSourceDefinition { Name = "orders", Kind = DataSourceKind.Json }],
        Parameters = [new ReportParameter { Name = "from", Type = ParameterType.Date }],
        Body = new ReportBody { Height = 800, Elements = [.. elements] },
    };

    private static ReportElement Field(string id, string value) => new()
    {
        Id = id, Type = ElementType.Field, Value = value,
        Bounds = new Bounds { X = 10, Y = 10, Width = 100, Height = 16 },
    };

    [Fact]
    public void Clean_Report_Has_No_Issues()
    {
        var issues = ReportInspector.Inspect(FreeReport(Field("a", "{orders.total}"), Field("b", "{param:from}")));
        Assert.Empty(issues);
    }

    [Fact]
    public void Flags_Unknown_Data_Source_And_Parameter()
    {
        var issues = ReportInspector.Inspect(FreeReport(
            Field("a", "{sales.total}"),
            Field("b", "{param:missing}")));

        Assert.Equal(2, issues.Count);
        Assert.All(issues, i => Assert.Equal(IssueSeverity.Error, i.Severity));
        Assert.Contains(issues, i => i.ElementId == "a" && i.Message.Contains("sales"));
        Assert.Contains(issues, i => i.ElementId == "b" && i.Message.Contains("missing"));
    }

    [Fact]
    public void Flags_Empty_Table_And_Unknown_Style()
    {
        var report = FreeReport(new ReportElement
        {
            Id = "t",
            Type = ElementType.Table,
            StyleRef = "ghost",
            Bounds = new Bounds { X = 0, Y = 0, Width = 200, Height = 100 },
            Table = new TableSpec { DataSource = "orders", Columns = [] },
        });

        var issues = ReportInspector.Inspect(report);

        Assert.Contains(issues, i => i.Message.Contains("no columns"));
        Assert.Contains(issues, i => i.Message.Contains("unknown style"));
    }

    [Fact]
    public void Flags_Group_Band_Without_Expression()
    {
        var report = new ReportDefinition
        {
            Name = "b",
            LayoutMode = LayoutMode.Banded,
            Bands =
            [
                new Band { Type = BandType.GroupHeader, Height = 20, Group = new GroupSpec() },
                new Band { Type = BandType.Detail, Height = 20 },
            ],
        };

        var issues = ReportInspector.Inspect(report);

        Assert.Contains(issues, i => i.Severity == IssueSeverity.Error && i.Message.Contains("group expression"));
    }
}
