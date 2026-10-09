using System.Text;
using JetReportDesigner.Core.Model;
using JetReportDesigner.DataSources;
using JetReportDesigner.DataSources.Json;
using JetReportDesigner.Rendering;
using JetReportDesigner.Rendering.Engines;

namespace JetReportDesigner.Rendering.Tests;

public class ExternalDataTests
{
    private static ReportRenderService Service() =>
        new(new ReportDataResolver([new JsonDataSourceReader()]), new MigraDocPdfRenderer());

    private static ReportDefinition Report() => new()
    {
        Name = "ext",
        LayoutMode = LayoutMode.Banded,
        Page = new PageSetup { Size = "Custom", CustomWidth = 300, CustomHeight = 120, Margins = new Margins { Top = 0, Right = 0, Bottom = 0, Left = 0 } },
        DataSources =
        [
            new DataSourceDefinition
            {
                Name = "data",
                Kind = DataSourceKind.Json,
                Json = new JsonSourceConfig { InlineData = "[{\"Adi\":\"SAMPLE\"}]" },
            },
        ],
        Bands =
        [
            new Band
            {
                Type = BandType.Detail, Height = 100, DataSource = "data",
                Elements = [new ReportElement { Id = "f", Type = ElementType.Field, Value = "{data.Adi}", Bounds = new Bounds { X = 0, Y = 0, Width = 200, Height = 20 } }],
            },
        ],
    };

    private static string Html(RenderResult r) => Encoding.UTF8.GetString(r.Content);

    [Fact]
    public async Task Overridden_rows_replace_the_sample_rows_for_that_render_only()
    {
        var overrides = new Dictionary<string, string> { ["data"] = "[{\"Adi\":\"AHMET\"},{\"Adi\":\"AYSE\"}]" };
        var report = Report();

        var withData = Html(await Service().RenderAsync(report, null, RenderFormat.Html, CancellationToken.None, new RenderOptions(overrides)));
        Assert.Contains("AHMET", withData, StringComparison.Ordinal);
        Assert.Contains("AYSE", withData, StringComparison.Ordinal);
        Assert.DoesNotContain("SAMPLE", withData, StringComparison.Ordinal);

        // The stored definition is untouched: the next render without data shows the sample again.
        var plain = Html(await Service().RenderAsync(report, null, RenderFormat.Html, CancellationToken.None));
        Assert.Contains("SAMPLE", plain, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unknown_data_source_is_rejected()
    {
        var options = new RenderOptions(new Dictionary<string, string> { ["nope"] = "[]" });
        await Assert.ThrowsAsync<UnknownDataSourceException>(() =>
            Service().RenderAsync(Report(), null, RenderFormat.Html, CancellationToken.None, options));
    }

    [Theory]
    [InlineData(RenderFormat.Png, new byte[] { 0x89, 0x50, 0x4E, 0x47 })]
    [InlineData(RenderFormat.Jpeg, new byte[] { 0xFF, 0xD8, 0xFF })]
    public async Task Image_formats_return_a_picture_and_the_page_count(RenderFormat format, byte[] magic)
    {
        var result = await Service().RenderAsync(Report(), null, format, CancellationToken.None, new RenderOptions(Dpi: 96));

        Assert.True(result.Content.Take(magic.Length).SequenceEqual(magic));
        Assert.Equal(1, result.PageCount);
    }

    [Fact]
    public async Task Page_beyond_the_last_is_rejected()
    {
        await Assert.ThrowsAsync<PageOutOfRangeException>(() =>
            Service().RenderAsync(Report(), null, RenderFormat.Png, CancellationToken.None, new RenderOptions(Page: 5)));
    }
}
