using JetReportDesigner.Core.Model;
using JetReportDesigner.Core.Serialization;

namespace JetReportDesigner.Core.Tests;

public class ReportChangesTests
{
    private static ReportDefinition Report(params string[] elementIds) => new()
    {
        Name = "R",
        LayoutMode = LayoutMode.Free,
        Body = new ReportBody
        {
            Height = 500,
            Elements = elementIds
                .Select(id => new ReportElement
                {
                    Id = id, Type = ElementType.Label, Text = id,
                    Bounds = new Bounds { X = 0, Y = 0, Width = 50, Height = 10 },
                })
                .ToList(),
        },
    };

    private static ReportDefinition Copy(ReportDefinition r) => ReportJson.Deserialize(ReportJson.Serialize(r));

    [Fact]
    public void Identical_reports_have_no_changes()
    {
        var a = Report("a", "b");

        Assert.Empty(ReportChanges.Summarize(a, Copy(a)));
    }

    [Fact]
    public void Added_removed_and_edited_elements_are_counted()
    {
        var before = Report("a", "b", "c");
        var after = Copy(before);
        after.Body!.Elements.RemoveAt(0);                         // removed: a
        after.Body.Elements[0].Text = "changed";                  // edited: b
        after.Body.Elements.Add(new ReportElement { Id = "d", Type = ElementType.Label, Text = "d" });
        after.Body.Elements.Add(new ReportElement { Id = "e", Type = ElementType.Label, Text = "e" });

        var changes = ReportChanges.Summarize(before, after);

        Assert.Equal(["added:2", "removed:1", "edited:1"], changes);
    }

    [Fact]
    public void Moving_an_element_counts_as_an_edit()
    {
        var before = Report("a");
        var after = Copy(before);
        after.Body!.Elements[0].Bounds.X = 40;

        Assert.Equal(["edited:1"], ReportChanges.Summarize(before, after));
    }

    [Fact]
    public void Page_name_and_data_source_changes_are_named()
    {
        var before = Report("a");
        var after = Copy(before);
        after.Name = "Renamed";
        after.Page.Size = "A5";
        after.DataSources.Add(new DataSourceDefinition { Name = "data", Kind = DataSourceKind.Json, Json = new JsonSourceConfig() });

        var changes = ReportChanges.Summarize(before, after);

        Assert.Contains("name", changes);
        Assert.Contains("page", changes);
        Assert.Contains("dataSources", changes);
    }

    [Fact]
    public void A_difference_nothing_else_explains_is_reported_as_other()
    {
        var before = Report("a");
        var after = Copy(before);
        after.Description = "now documented";

        Assert.Equal(["other"], ReportChanges.Summarize(before, after));
    }
}
