using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

namespace JetReportDesigner.Rendering.Engines;

/// <summary>
/// PDF engine candidate #2. PdfSharp (MIT, no licence threshold) drawn with
/// <see cref="XGraphics"/> for absolute placement. Trade-off surfaced by the spike:
/// PdfSharp has no bundled fonts, so non-Windows hosts need a custom
/// <see cref="IFontResolver"/> and font files provisioned in the image.
/// </summary>
public sealed class MigraDocPdfRenderer : IPdfRenderer
{
    static MigraDocPdfRenderer() => GlobalFontSettings.FontResolver ??= new WindowsCoreFontResolver();

    public string EngineName => "migradoc";

    public byte[] Render(RenderDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        using var pdf = new PdfDocument();
        var widthPt = RenderUnits.ToPoints(document.PageWidthPx);
        var heightPt = RenderUnits.ToPoints(document.PageHeightPx);

        foreach (var page in document.Pages)
        {
            var pdfPage = pdf.AddPage();
            pdfPage.Width = XUnit.FromPoint(widthPt);
            pdfPage.Height = XUnit.FromPoint(heightPt);

            using var gfx = XGraphics.FromPdfPage(pdfPage);
            foreach (var primitive in page.Primitives)
            {
                Draw(gfx, primitive);
            }
        }

        using var stream = new MemoryStream();
        pdf.Save(stream);
        return stream.ToArray();
    }

    private static void Draw(XGraphics gfx, RenderPrimitive primitive)
    {
        switch (primitive)
        {
            case TextPrimitive text:
                DrawText(gfx, text);
                break;

            case LinePrimitive line:
                gfx.DrawLine(
                    new XPen(XColor.FromArgb(ParseColor(line.ColorHex)), RenderUnits.ToPoints(line.ThicknessPx)),
                    RenderUnits.ToPoints(line.X),
                    RenderUnits.ToPoints(line.Y),
                    RenderUnits.ToPoints(line.X2),
                    RenderUnits.ToPoints(line.Y2));
                break;

            case RectanglePrimitive rect:
                DrawRectangle(gfx, rect);
                break;
        }
    }

    private static void DrawText(XGraphics gfx, TextPrimitive text)
    {
        var style = XFontStyleEx.Regular;
        if (text.Bold)
        {
            style |= XFontStyleEx.Bold;
        }

        if (text.Italic)
        {
            style |= XFontStyleEx.Italic;
        }

        var font = new XFont(text.FontFamily, text.FontSizePt, style);
        var rect = new XRect(
            RenderUnits.ToPoints(text.X),
            RenderUnits.ToPoints(text.Y),
            RenderUnits.ToPoints(text.Width),
            RenderUnits.ToPoints(text.Height));

        var format = new XStringFormat
        {
            Alignment = text.HAlign switch
            {
                HorizontalAlign.Center => XStringAlignment.Center,
                HorizontalAlign.Right => XStringAlignment.Far,
                _ => XStringAlignment.Near,
            },
            LineAlignment = text.VAlign switch
            {
                VerticalAlign.Middle => XLineAlignment.Center,
                VerticalAlign.Bottom => XLineAlignment.Far,
                _ => XLineAlignment.Near,
            },
        };

        gfx.DrawString(text.Text, font, new XSolidBrush(XColor.FromArgb(ParseColor(text.ColorHex))), rect, format);
    }

    private static void DrawRectangle(XGraphics gfx, RectanglePrimitive rect)
    {
        var box = new XRect(
            RenderUnits.ToPoints(rect.X),
            RenderUnits.ToPoints(rect.Y),
            RenderUnits.ToPoints(rect.Width),
            RenderUnits.ToPoints(rect.Height));

        if (rect.FillColorHex is { } fill)
        {
            gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(ParseColor(fill))), box);
        }

        if (rect.BorderThicknessPx > 0)
        {
            gfx.DrawRectangle(
                new XPen(XColor.FromArgb(ParseColor(rect.BorderColorHex)), RenderUnits.ToPoints(rect.BorderThicknessPx)),
                box);
        }
    }

    private static int ParseColor(string hex)
    {
        var value = hex.TrimStart('#');
        var rgb = int.Parse(value, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);
        return unchecked((int)(0xFF000000 | (uint)rgb));
    }

    /// <summary>
    /// Resolves fonts from the Windows core set (Arial / Times New Roman / Courier New)
    /// living in %WINDIR%\Fonts. Spike finding: this is Windows-only; a Linux host
    /// needs font files provisioned in the image and a resolver that reads them —
    /// see docs/04-pdf-motoru-karari.md.
    /// </summary>
    private sealed class WindowsCoreFontResolver : IFontResolver
    {
        private static readonly string FontsDir =
            Environment.GetFolderPath(Environment.SpecialFolder.Fonts);

        public byte[] GetFont(string faceName)
        {
            var path = Path.Combine(FontsDir, faceName + ".ttf");
            if (!OperatingSystem.IsWindows() || !File.Exists(path))
            {
                throw new PlatformNotSupportedException(
                    $"MigraDocPdfRenderer could not load '{faceName}'. PdfSharp has no bundled fonts; "
                    + "non-Windows hosts must provision font files. See docs/04-pdf-motoru-karari.md.");
            }

            return File.ReadAllBytes(path);
        }

        public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
        {
            var family = familyName.ToLowerInvariant();
            var (regular, bold, italic, boldItalic) =
                family.Contains("courier") || family.Contains("mono")
                    ? ("cour", "courbd", "couri", "courbi")
                    : family.Contains("times") || family.Contains("serif") || family.Contains("georgia")
                        ? ("times", "timesbd", "timesi", "timesbi")
                        : ("arial", "arialbd", "ariali", "arialbi");

            var face = (isBold, isItalic) switch
            {
                (true, true) => boldItalic,
                (true, false) => bold,
                (false, true) => italic,
                _ => regular,
            };
            return new FontResolverInfo(face);
        }
    }
}
