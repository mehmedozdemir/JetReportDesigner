using SkiaSharp;

namespace JetReportDesigner.Rendering.Engines;

/// <summary>Rasterizes a rendered PDF page to PNG or JPEG (pdfium), so image output matches the PDF exactly.</summary>
public static class PdfRasterizer
{
    private const int MaxDpi = 600;

    public static RenderResult Rasterize(byte[] pdf, RenderFormat format, int page, int dpi, string fileName)
    {
        var pageCount = PDFtoImage.Conversion.GetPageCount(pdf);
        if (page < 1 || page > pageCount)
        {
            throw new PageOutOfRangeException(page, pageCount);
        }

        var png = format == RenderFormat.Png;
        var options = new PDFtoImage.RenderOptions(Dpi: Math.Clamp(dpi, 36, MaxDpi), WithAnnotations: false, BackgroundColor: SKColors.White);

        using var output = new MemoryStream();
        using var input = new MemoryStream(pdf);
        PDFtoImage.Conversion.SavePng(output, input, page - 1, options: options);
        var bytes = output.ToArray();

        if (!png)
        {
            using var bitmap = SKBitmap.Decode(bytes);
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Jpeg, 90);
            bytes = data.ToArray();
        }

        return png
            ? new RenderResult(bytes, "image/png", $"{fileName}-{page}.png", pageCount)
            : new RenderResult(bytes, "image/jpeg", $"{fileName}-{page}.jpg", pageCount);
    }

    public static int PageCount(byte[] pdf) => PDFtoImage.Conversion.GetPageCount(pdf);
}
