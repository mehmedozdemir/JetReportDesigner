using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

namespace JetReportDesigner.Rendering.Engines;

/// <summary>
/// PDF engine (MIT, no licence threshold). PdfSharp <see cref="XGraphics"/> for
/// absolute placement; fonts come from <see cref="SystemFontResolver"/> (OS core
/// fonts on Windows, Liberation/DejaVu on Linux — the image installs
/// <c>fonts-liberation</c>).
/// </summary>
public sealed class MigraDocPdfRenderer : IPdfRenderer
{
    // PdfSharp keeps process-global font/state caches that are not thread-safe.
    // Rendering is fast; serialise it for correctness.
    private static readonly Lock RenderGate = new();

    static MigraDocPdfRenderer() => GlobalFontSettings.FontResolver ??= new SystemFontResolver();

    public string EngineName => "migradoc";

    public byte[] Render(RenderDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        lock (RenderGate)
        {
            return RenderCore(document);
        }
    }

    private static byte[] RenderCore(RenderDocument document)
    {
        using var pdf = new PdfDocument();
        var widthPt = RenderUnits.ToPoints(document.PageWidthPx);
        var heightPt = RenderUnits.ToPoints(document.PageHeightPx);

        // XImage may read its stream lazily (at pdf.Save); keep them all alive until then.
        var images = new List<IDisposable>();

        foreach (var page in document.Pages)
        {
            var pdfPage = pdf.AddPage();
            pdfPage.Width = XUnit.FromPoint(widthPt);
            pdfPage.Height = XUnit.FromPoint(heightPt);

            using var gfx = XGraphics.FromPdfPage(pdfPage);
            foreach (var primitive in page.Primitives)
            {
                Draw(gfx, primitive, images);
            }
        }

        try
        {
            using var stream = new MemoryStream();
            pdf.Save(stream);
            return stream.ToArray();
        }
        finally
        {
            foreach (var image in images)
            {
                image.Dispose();
            }
        }
    }

    private static void Draw(XGraphics gfx, RenderPrimitive primitive, List<IDisposable> images)
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

            case ImagePrimitive image:
                DrawImage(gfx, image, images);
                break;

            case PolygonPrimitive polygon:
                DrawPolygon(gfx, polygon);
                break;

            case WedgePrimitive wedge:
                DrawWedge(gfx, wedge);
                break;
        }
    }

    private static void DrawPolygon(XGraphics gfx, PolygonPrimitive polygon)
    {
        if (polygon.Points.Count < 2)
        {
            return;
        }

        var pts = polygon.Points
            .Select(p => new XPoint(RenderUnits.ToPoints(p.X), RenderUnits.ToPoints(p.Y)))
            .ToArray();

        if (polygon.FillColorHex is { } fill)
        {
            gfx.DrawPolygon(new XSolidBrush(XColor.FromArgb(ParseColor(fill))), pts, XFillMode.Winding);
        }

        if (polygon.StrokeColorHex is { } stroke && polygon.StrokeWidthPx > 0)
        {
            gfx.DrawPolygon(new XPen(XColor.FromArgb(ParseColor(stroke)), RenderUnits.ToPoints(polygon.StrokeWidthPx)), pts);
        }
    }

    private static void DrawWedge(XGraphics gfx, WedgePrimitive wedge)
    {
        var box = new XRect(
            RenderUnits.ToPoints(wedge.X - wedge.Radius),
            RenderUnits.ToPoints(wedge.Y - wedge.Radius),
            RenderUnits.ToPoints(wedge.Radius * 2),
            RenderUnits.ToPoints(wedge.Radius * 2));

        gfx.DrawPie(
            new XSolidBrush(XColor.FromArgb(ParseColor(wedge.FillColorHex))),
            box, wedge.StartAngleDeg, wedge.SweepAngleDeg);

        if (wedge.StrokeColorHex is { } stroke && wedge.StrokeWidthPx > 0)
        {
            gfx.DrawPie(
                new XPen(XColor.FromArgb(ParseColor(stroke)), RenderUnits.ToPoints(wedge.StrokeWidthPx)),
                box, wedge.StartAngleDeg, wedge.SweepAngleDeg);
        }
    }

    private static void DrawImage(XGraphics gfx, ImagePrimitive image, List<IDisposable> images)
    {
        if (image.Bytes is null || image.Bytes.Length == 0)
        {
            return;
        }

        XImage xImage;
        var stream = new MemoryStream(image.Bytes, writable: false);
        try
        {
            xImage = XImage.FromStream(stream);
        }
        catch
        {
            stream.Dispose();
            return; // PdfSharp cannot decode this format (e.g. WebP/BMP) — skip it.
        }

        images.Add(stream);
        images.Add(xImage);

        var box = new XRect(
            RenderUnits.ToPoints(image.X),
            RenderUnits.ToPoints(image.Y),
            RenderUnits.ToPoints(image.Width),
            RenderUnits.ToPoints(image.Height));

        if (image.Fit == ImageFit.Fill || xImage.PixelWidth == 0 || xImage.PixelHeight == 0)
        {
            gfx.DrawImage(xImage, box);
            return;
        }

        if (image.Fit == ImageFit.Tile)
        {
            var tileW = RenderUnits.ToPoints(xImage.PixelWidth);
            var tileH = RenderUnits.ToPoints(xImage.PixelHeight);
            if (tileW <= 0 || tileH <= 0)
            {
                gfx.DrawImage(xImage, box);
                return;
            }

            var state = gfx.Save();
            gfx.IntersectClip(box);
            for (var ty = box.Top; ty < box.Bottom; ty += tileH)
            {
                for (var tx = box.Left; tx < box.Right; tx += tileW)
                {
                    gfx.DrawImage(xImage, new XRect(tx, ty, tileW, tileH));
                }
            }

            gfx.Restore(state);
            return;
        }

        // Cover / Contain: preserve aspect ratio, centre in the box, clip for Cover.
        var scale = image.Fit == ImageFit.Cover
            ? Math.Max(box.Width / xImage.PixelWidth, box.Height / xImage.PixelHeight)
            : Math.Min(box.Width / xImage.PixelWidth, box.Height / xImage.PixelHeight);
        var drawW = xImage.PixelWidth * scale;
        var drawH = xImage.PixelHeight * scale;
        var dst = new XRect(
            box.X + (box.Width - drawW) / 2,
            box.Y + (box.Height - drawH) / 2,
            drawW,
            drawH);

        if (image.Fit == ImageFit.Cover)
        {
            var state = gfx.Save();
            gfx.IntersectClip(box);
            gfx.DrawImage(xImage, dst);
            gfx.Restore(state);
        }
        else
        {
            gfx.DrawImage(xImage, dst);
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
                HorizontalAnchor.Center => XStringAlignment.Center,
                HorizontalAnchor.Right => XStringAlignment.Far,
                _ => XStringAlignment.Near,
            },
            LineAlignment = text.VAlign switch
            {
                VerticalAnchor.Middle => XLineAlignment.Center,
                VerticalAnchor.Bottom => XLineAlignment.Far,
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
}
