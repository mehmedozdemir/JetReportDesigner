using System.Globalization;
using System.Text;


namespace JetReportDesigner.Rendering.Engines;

/// <summary>
/// Renders a <see cref="RenderDocument"/> to a self-contained HTML page: one
/// absolutely-positioned <c>div.page</c> per page, elements placed with inline
/// styles. For fast designer feedback and browser printing; the PDF is the
/// reference output.
/// </summary>
public sealed class HtmlReportRenderer
{
    public string Render(RenderDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var sb = new StringBuilder();
        sb.Append("<!doctype html><html><head><meta charset=\"utf-8\">");
        sb.Append("<style>");
        sb.Append("html,body{margin:0;background:#e5e7eb}");
        sb.Append(".page{position:relative;background:#fff;margin:16px auto;box-shadow:0 1px 4px rgba(0,0,0,.2);overflow:hidden}");
        sb.Append(".el{position:absolute;box-sizing:border-box;overflow:hidden;white-space:pre-wrap;line-height:1.2}");
        sb.Append("@media print{html,body{background:#fff}.page{margin:0;box-shadow:none}}");
        sb.Append("</style></head><body>");

        foreach (var page in document.Pages)
        {
            sb.Append(CultureInfo.InvariantCulture, $"<div class=\"page\" style=\"width:{Px(document.PageWidthPx)};height:{Px(document.PageHeightPx)}\">");
            foreach (var primitive in page.Primitives)
            {
                sb.Append(RenderPrimitiveHtml(primitive));
            }

            sb.Append("</div>");
        }

        sb.Append("</body></html>");
        return sb.ToString();
    }

    private static string RenderPrimitiveHtml(RenderPrimitive primitive) => primitive switch
    {
        TextPrimitive t => RenderText(t),
        LinePrimitive l => RenderLine(l),
        RectanglePrimitive r => RenderRectangle(r),
        _ => string.Empty,
    };

    private static string RenderText(TextPrimitive t)
    {
        var css = new StringBuilder();
        css.Append(CultureInfo.InvariantCulture, $"left:{Px(t.X)};top:{Px(t.Y)};width:{Px(t.Width)};height:{Px(t.Height)};");
        css.Append(CultureInfo.InvariantCulture, $"font-family:{System.Net.WebUtility.HtmlEncode(t.FontFamily)},sans-serif;");
        css.Append(CultureInfo.InvariantCulture, $"font-size:{t.FontSizePt.ToString(CultureInfo.InvariantCulture)}pt;");
        css.Append(CultureInfo.InvariantCulture, $"color:{t.ColorHex};");
        if (t.Bold)
        {
            css.Append("font-weight:700;");
        }

        if (t.Italic)
        {
            css.Append("font-style:italic;");
        }

        css.Append("text-align:").Append(t.HAlign switch
        {
            HorizontalAnchor.Center => "center;",
            HorizontalAnchor.Right => "right;",
            _ => "left;",
        });
        css.Append("display:flex;flex-direction:column;");
        css.Append("justify-content:").Append(t.VAlign switch
        {
            VerticalAnchor.Middle => "center;",
            VerticalAnchor.Bottom => "flex-end;",
            _ => "flex-start;",
        });

        return $"<div class=\"el\" style=\"{css}\">{System.Net.WebUtility.HtmlEncode(t.Text)}</div>";
    }

    private static string RenderLine(LinePrimitive l)
    {
        var x = Math.Min(l.X, l.X2);
        var y = Math.Min(l.Y, l.Y2);
        var w = Math.Max(Math.Abs(l.X2 - l.X), l.ThicknessPx);
        var h = Math.Max(Math.Abs(l.Y2 - l.Y), l.ThicknessPx);
        return $"<div class=\"el\" style=\"left:{Px(x)};top:{Px(y)};width:{Px(w)};height:{Px(h)};background:{l.ColorHex}\"></div>";
    }

    private static string RenderRectangle(RectanglePrimitive r)
    {
        var css = new StringBuilder();
        css.Append(CultureInfo.InvariantCulture, $"left:{Px(r.X)};top:{Px(r.Y)};width:{Px(r.Width)};height:{Px(r.Height)};");
        if (r.FillColorHex is { } fill)
        {
            css.Append(CultureInfo.InvariantCulture, $"background:{fill};");
        }

        if (r.BorderThicknessPx > 0)
        {
            css.Append(CultureInfo.InvariantCulture, $"border:{Px(r.BorderThicknessPx)} solid {r.BorderColorHex};");
        }

        return $"<div class=\"el\" style=\"{css}\"></div>";
    }

    private static string Px(double value) =>
        value.ToString("0.##", CultureInfo.InvariantCulture) + "px";
}
