using JetReportDesigner.Core.Model;
using JetReportDesigner.DataSources;
using JetReportDesigner.Rendering.Layout;

namespace JetReportDesigner.Rendering.Tests;

public class AutoHeightTests
{
    private const string LongText =
        "This note is deliberately long enough that it must wrap across several lines " +
        "when squeezed into a narrow box, which is exactly the case a can-grow element " +
        "needs to handle without clipping any of it.";

    private static ReportData NoData() => new(new Dictionary<string, ResolvedDataSet>());

    [Fact]
    public void CanGrow_label_in_free_layout_grows_taller_than_its_bounds()
    {
        var report = new ReportDefinition
        {
            Name = "grow",
            LayoutMode = LayoutMode.Free,
            Body = new ReportBody
            {
                Elements =
                [
                    new ReportElement
                    {
                        Id = "note", Type = ElementType.Label, Text = LongText, CanGrow = true,
                        Bounds = new Bounds { X = 0, Y = 0, Width = 120, Height = 16 },
                    },
                ],
            },
        };

        var doc = new FreeLayoutBuilder().Build(report, NoData(), new Dictionary<string, object?>());
        var text = Assert.Single(doc.Pages.Single().Primitives.OfType<TextPrimitive>());

        Assert.True(text.Height > 16, $"expected the box to grow past 16px, got {text.Height}");
    }

    [Fact]
    public void Without_CanGrow_the_same_long_text_keeps_its_designed_height()
    {
        var report = new ReportDefinition
        {
            Name = "no-grow",
            LayoutMode = LayoutMode.Free,
            Body = new ReportBody
            {
                Elements =
                [
                    new ReportElement
                    {
                        Id = "note", Type = ElementType.Label, Text = LongText, CanGrow = false,
                        Bounds = new Bounds { X = 0, Y = 0, Width = 120, Height = 16 },
                    },
                ],
            },
        };

        var doc = new FreeLayoutBuilder().Build(report, NoData(), new Dictionary<string, object?>());
        var text = Assert.Single(doc.Pages.Single().Primitives.OfType<TextPrimitive>());

        Assert.Equal(16, text.Height);
    }

    [Fact]
    public void CanGrow_detail_row_pushes_the_next_rows_band_down()
    {
        var rows = new List<IReadOnlyDictionary<string, object?>>
        {
            new Dictionary<string, object?> { ["note"] = LongText },
            new Dictionary<string, object?> { ["note"] = "short" },
        };

        var report = new ReportDefinition
        {
            Name = "grow-detail",
            LayoutMode = LayoutMode.Banded,
            DataSources = [new DataSourceDefinition { Name = "notes", Kind = DataSourceKind.None }],
            Bands =
            [
                new Band
                {
                    Type = BandType.Detail, Height = 16, DataSource = "notes",
                    Elements =
                    [
                        new ReportElement
                        {
                            Id = "note", Type = ElementType.Field, Value = "{notes.note}", CanGrow = true,
                            Bounds = new Bounds { X = 0, Y = 0, Width = 100, Height = 16 },
                        },
                    ],
                },
            ],
        };

        var data = new ReportData(new Dictionary<string, ResolvedDataSet>
        {
            ["notes"] = new ResolvedDataSet(rows, []),
        });

        var doc = new BandedLayoutBuilder().Build(report, data, new Dictionary<string, object?>());
        var texts = doc.Pages.SelectMany(p => p.Primitives).OfType<TextPrimitive>()
            .Where(t => t.Text is not "")
            .OrderBy(t => t.Y)
            .ToList();

        // The first (grown) row's text box must already be taller than the configured
        // 16px, and the second row must start below where a fixed 16px row would have.
        var first = texts.First(t => t.Text.StartsWith("This note", StringComparison.Ordinal));
        var second = texts.First(t => t.Text == "short");

        Assert.True(first.Height > 16, $"expected row 1 to grow past 16px, got {first.Height}");
        Assert.True(second.Y >= first.Y + first.Height, $"row 2 (y={second.Y}) overlaps row 1's grown box (bottom={first.Y + first.Height})");
    }
}
