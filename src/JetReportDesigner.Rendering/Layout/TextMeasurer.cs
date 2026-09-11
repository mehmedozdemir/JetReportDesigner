using PdfSharp.Drawing;
using PdfSharp.Fonts;

namespace JetReportDesigner.Rendering.Layout;

/// <summary>
/// Word-wraps text against a box width using PdfSharp's font metrics (a measure-only
/// <see cref="XGraphics"/> context — no page is created) so a "can grow" element's
/// required height can be known during layout, before either engine actually draws it.
/// Shares <see cref="SystemFontResolver"/> with the PDF engine, so the estimate matches
/// what PdfSharp will render; the HTML engine's browser text-wrap can differ by a
/// pixel or two, which is why <see cref="ElementEmitter.CanGrowSlackPx"/> pads the result.
/// </summary>
internal static class TextMeasurer
{
    // PdfSharp keeps process-global font/state caches that are not thread-safe (the PDF
    // engine already serialises its own rendering behind a similar lock).
    private static readonly Lock Gate = new();

    static TextMeasurer() => GlobalFontSettings.FontResolver ??= new Engines.SystemFontResolver();

    /// <summary>Height (in px, 1/96 inch) needed to lay out <paramref name="text"/> word-wrapped into <paramref name="widthPx"/>.</summary>
    public static double MeasureWrappedHeightPx(string text, string fontFamily, double fontSizePt, bool bold, bool italic, double widthPx)
    {
        if (string.IsNullOrEmpty(text) || widthPx <= 0)
        {
            return 0;
        }

        lock (Gate)
        {
            using var gfx = XGraphics.CreateMeasureContext(new XSize(100_000, 100_000), XGraphicsUnit.Point, XPageDirection.Downwards);
            var style = XFontStyleEx.Regular;
            if (bold)
            {
                style |= XFontStyleEx.Bold;
            }

            if (italic)
            {
                style |= XFontStyleEx.Italic;
            }

            var font = new XFont(fontFamily, fontSizePt, style);
            var widthPt = RenderUnits.ToPoints(widthPx);
            var lineHeightPt = gfx.MeasureString("Aq", font).Height;

            var totalLines = 0;
            foreach (var paragraph in text.Replace("\r\n", "\n").Split('\n'))
            {
                totalLines += CountWrappedLines(gfx, font, paragraph, widthPt);
            }

            return totalLines * lineHeightPt / RenderUnits.PxToPt;
        }
    }

    private static int CountWrappedLines(XGraphics gfx, XFont font, string paragraph, double widthPt)
    {
        if (paragraph.Length == 0)
        {
            return 1;
        }

        var words = paragraph.Split(' ');
        var lines = 1;
        var current = "";

        foreach (var word in words)
        {
            var candidate = current.Length == 0 ? word : current + " " + word;
            if (gfx.MeasureString(candidate, font).Width <= widthPt || current.Length == 0)
            {
                current = candidate;
            }
            else
            {
                lines++;
                current = word;
            }
        }

        return lines;
    }
}
