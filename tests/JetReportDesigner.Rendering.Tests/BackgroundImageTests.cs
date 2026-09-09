using JetReportDesigner.Core.Model;
using JetReportDesigner.DataSources;
using JetReportDesigner.DataSources.Json;
using JetReportDesigner.Rendering;
using JetReportDesigner.Rendering.Engines;
using JetReportDesigner.Rendering.Layout;

namespace JetReportDesigner.Rendering.Tests;

public class BackgroundImageTests
{
    // 1x1 transparent PNG.
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    private sealed class StubResolver : IRenderImageResolver
    {
        public Task<ResolvedImage?> ResolveAsync(string source, CancellationToken cancellationToken) =>
            Task.FromResult<ResolvedImage?>(new ResolvedImage(Png, "image/png"));
    }

    private static ReportData NoData() => new(new Dictionary<string, ResolvedDataSet>());

    [Fact]
    public void Free_layout_emits_page_and_element_background_image_primitives()
    {
        var report = new ReportDefinition
        {
            Name = "bg",
            LayoutMode = LayoutMode.Free,
            Page = new PageSetup { BackgroundImage = new BackgroundImageSpec { Source = "asset:1", Fit = "contain" } },
            Body = new ReportBody
            {
                Elements =
                [
                    new ReportElement
                    {
                        Id = "img", Type = ElementType.Image,
                        Bounds = new Bounds { X = 10, Y = 10, Width = 80, Height = 40 },
                        Image = new ImageSpec { Source = "asset:2", Fit = "cover" },
                    },
                ],
            },
        };

        var doc = new FreeLayoutBuilder().Build(report, NoData(), new Dictionary<string, object?>());

        var images = doc.Pages.Single().Primitives.OfType<ImagePrimitive>().ToList();
        Assert.Equal(2, images.Count);
        Assert.Contains(images, i => i.Source == "asset:1" && i.Fit == ImageFit.Contain && i.Width == doc.PageWidthPx);
        Assert.Contains(images, i => i.Source == "asset:2" && i.Fit == ImageFit.Cover);
    }

    [Fact]
    public async Task Render_service_fills_image_bytes_from_the_resolver()
    {
        var report = new ReportDefinition
        {
            Name = "bg",
            LayoutMode = LayoutMode.Free,
            Page = new PageSetup { BackgroundImage = new BackgroundImageSpec { Source = "asset:1" } },
            Body = new ReportBody(),
        };

        var service = new ReportRenderService(
            new ReportDataResolver([new JsonDataSourceReader()]),
            new MigraDocPdfRenderer(),
            new StubResolver());

        var result = await service.RenderAsync(report, null, RenderFormat.Html, CancellationToken.None);
        var html = System.Text.Encoding.UTF8.GetString(result.Content);

        Assert.Contains("data:image/png;base64,", html);
    }
}
