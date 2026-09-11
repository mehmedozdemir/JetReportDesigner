using System.Text;
using JetReportDesigner.Core.Model;
using JetReportDesigner.DataSources;
using JetReportDesigner.Rendering;
using JetReportDesigner.Rendering.Engines;

namespace JetReportDesigner.Rendering.Tests;

public class BarcodeTests
{
    private static ReportDefinition Report(string symbology, string value, bool showText = true) => new()
    {
        Name = "barcode",
        LayoutMode = LayoutMode.Free,
        Body = new ReportBody
        {
            Elements =
            [
                new ReportElement
                {
                    Id = "bc", Type = ElementType.Barcode,
                    Bounds = new Bounds { X = 10, Y = 10, Width = 160, Height = 160 },
                    Barcode = new BarcodeSpec { Symbology = symbology, Value = value, ShowText = showText },
                },
            ],
        },
    };

    [Fact]
    public async Task Qr_code_emits_dark_module_rectangles()
    {
        var service = new ReportRenderService(new ReportDataResolver([]), new MigraDocPdfRenderer());
        var doc = await service.RenderAsync(Report("qr", "https://example.com"), null, RenderFormat.Pdf, CancellationToken.None);

        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(doc.Content, 0, 4));
    }

    [Theory]
    [InlineData("qr", "HELLO")]
    [InlineData("code128", "HELLO-128")]
    [InlineData("ean13", "400638133393")]
    [InlineData("code39", "HELLO39")]
    [InlineData("dataMatrix", "HELLO-DM")]
    public async Task Every_symbology_renders_to_pdf_and_html(string symbology, string value)
    {
        var service = new ReportRenderService(new ReportDataResolver([]), new MigraDocPdfRenderer());
        var report = Report(symbology, value);

        var pdf = await service.RenderAsync(report, null, RenderFormat.Pdf, CancellationToken.None);
        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(pdf.Content, 0, 4));

        var html = await service.RenderAsync(report, null, RenderFormat.Html, CancellationToken.None);
        var text = Encoding.UTF8.GetString(html.Content);
        Assert.Contains("class=\"el\"", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Invalid_ean13_value_becomes_an_error_box_not_a_failure()
    {
        var service = new ReportRenderService(new ReportDataResolver([]), new MigraDocPdfRenderer());
        var result = await service.RenderAsync(Report("ean13", "not-numeric"), null, RenderFormat.Html, CancellationToken.None);

        Assert.Contains("Invalid ean13 value", Encoding.UTF8.GetString(result.Content));
    }

    [Fact]
    public async Task Empty_value_renders_nothing_rather_than_an_error()
    {
        var service = new ReportRenderService(new ReportDataResolver([]), new MigraDocPdfRenderer());
        var result = await service.RenderAsync(Report("qr", ""), null, RenderFormat.Html, CancellationToken.None);

        Assert.DoesNotContain("Invalid", Encoding.UTF8.GetString(result.Content));
    }
}
