using JetReportDesigner.Core.Model;
using JetReportDesigner.DataSources;
using JetReportDesigner.Rendering.Layout;

namespace JetReportDesigner.Rendering.Tests;

public class MultiColumnLayoutTests
{
    private static ReportData Rows(int count)
    {
        var rows = Enumerable.Range(1, count)
            .Select(i => (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?> { ["name"] = $"Item {i}" })
            .ToList();
        return new ReportData(new Dictionary<string, ResolvedDataSet> { ["items"] = new ResolvedDataSet(rows, []) });
    }

    private static ReportDefinition Report(int columns, int rowCount, double detailHeight = 16) => new()
    {
        Name = "multi-column",
        LayoutMode = LayoutMode.Banded,
        Page = new PageSetup
        {
            Margins = new Margins { Top = 40, Right = 40, Bottom = 40, Left = 40 },
            Columns = columns,
            ColumnSpacing = 10,
        },
        DataSources = [new DataSourceDefinition { Name = "items", Kind = DataSourceKind.None }],
        Bands =
        [
            new Band
            {
                Type = BandType.Detail, Height = detailHeight, DataSource = "items",
                Elements =
                [
                    new ReportElement
                    {
                        Id = "name", Type = ElementType.Field, Value = "{items.name}",
                        Bounds = new Bounds { X = 0, Y = 0, Width = 100, Height = detailHeight },
                    },
                ],
            },
        ],
    };

    [Fact]
    public void Three_columns_fill_left_to_right_before_wrapping_down()
    {
        var report = Report(columns: 3, rowCount: 7);
        var doc = new BandedLayoutBuilder().Build(report, Rows(7), new Dictionary<string, object?>());
        var texts = doc.Pages.Single().Primitives.OfType<TextPrimitive>()
            .OrderBy(t => t.Y).ThenBy(t => t.X)
            .ToList();

        Assert.Equal(7, texts.Count);

        // Row 1: items 1-3 share one Y, ascending X.
        var row1 = texts.Take(3).ToList();
        Assert.All(row1, t => Assert.Equal(row1[0].Y, t.Y));
        Assert.True(row1[0].X < row1[1].X && row1[1].X < row1[2].X);
        Assert.Equal(["Item 1", "Item 2", "Item 3"], row1.Select(t => t.Text));

        // Row 2: items 4-6 at the next Y down.
        var row2 = texts.Skip(3).Take(3).ToList();
        Assert.All(row2, t => Assert.Equal(row2[0].Y, t.Y));
        Assert.True(row2[0].Y > row1[0].Y);
        Assert.Equal(["Item 4", "Item 5", "Item 6"], row2.Select(t => t.Text));

        // Row 3: only item 7, alone in column 1 — still its own Y, below row 2.
        var last = texts[6];
        Assert.Equal("Item 7", last.Text);
        Assert.True(last.Y > row2[0].Y);
        Assert.Equal(row1[0].X, last.X); // column 1's X position
    }

    [Fact]
    public void Column_spacing_and_width_match_the_page_setup()
    {
        var report = Report(columns: 2, rowCount: 2);
        var doc = new BandedLayoutBuilder().Build(report, Rows(2), new Dictionary<string, object?>());
        var texts = doc.Pages.Single().Primitives.OfType<TextPrimitive>().OrderBy(t => t.X).ToList();

        var (pageWidth, _) = PageGeometry.Resolve(report.Page);
        var usableWidth = pageWidth - report.Page.Margins.Left - report.Page.Margins.Right;
        var expectedColumnWidth = (usableWidth - report.Page.ColumnSpacing) / 2;

        Assert.Equal(report.Page.Margins.Left, texts[0].X, 3);
        Assert.Equal(report.Page.Margins.Left + expectedColumnWidth + report.Page.ColumnSpacing, texts[1].X, 3);
    }

    [Fact]
    public void A_group_change_flushes_a_partially_filled_row_of_columns()
    {
        var groupRows = new List<IReadOnlyDictionary<string, object?>>
        {
            new Dictionary<string, object?> { ["group"] = "A", ["name"] = "A1" },
            new Dictionary<string, object?> { ["group"] = "B", ["name"] = "B1" },
            new Dictionary<string, object?> { ["group"] = "B", ["name"] = "B2" },
        };
        var data = new ReportData(new Dictionary<string, ResolvedDataSet>
        {
            ["items"] = new ResolvedDataSet(groupRows, []),
        });

        var report = Report(columns: 3, rowCount: 3);
        report.Bands.Insert(0, new Band
        {
            Type = BandType.GroupHeader, GroupLevel = 0, Height = 14,
            Group = new GroupSpec { DataSource = "items", Expression = "{items.group}", Sort = "asc" },
            Elements =
            [
                new ReportElement
                {
                    Id = "gh", Type = ElementType.Field, Value = "{items.group}",
                    Bounds = new Bounds { X = 0, Y = 0, Width = 100, Height = 14 },
                },
            ],
        });

        var doc = new BandedLayoutBuilder().Build(report, data, new Dictionary<string, object?>());
        var texts = doc.Pages.Single().Primitives.OfType<TextPrimitive>().OrderBy(t => t.Y).ThenBy(t => t.X).ToList();

        // Group A has exactly one row (fills only column 1 of its row-of-columns); group
        // B's header must still start on its own line, not squeezed into A's row.
        var groupBY = texts.Single(t => t.Text == "B").Y;
        var b1Y = texts.Single(t => t.Text == "B1").Y;
        var a1Y = texts.Single(t => t.Text == "A1").Y;

        Assert.True(groupBY > a1Y);
        Assert.Equal(groupBY, b1Y - 14, 3); // B1 sits directly under the B header, one detail-row's worth of Y below... actually just below the header
        Assert.True(b1Y > groupBY);

        // B1 and B2 land side by side (same Y), not stacked.
        var b2Y = texts.Single(t => t.Text == "B2").Y;
        Assert.Equal(b1Y, b2Y, 3);
    }

    [Fact]
    public void Columns_of_1_is_identical_to_the_ordinary_single_column_layout()
    {
        var report = Report(columns: 1, rowCount: 3);
        var doc = new BandedLayoutBuilder().Build(report, Rows(3), new Dictionary<string, object?>());
        var texts = doc.Pages.Single().Primitives.OfType<TextPrimitive>().OrderBy(t => t.Y).ToList();

        Assert.Equal(3, texts.Count);
        Assert.True(texts[0].Y < texts[1].Y && texts[1].Y < texts[2].Y);
        Assert.All(texts, t => Assert.Equal(texts[0].X, t.X));
    }
}
