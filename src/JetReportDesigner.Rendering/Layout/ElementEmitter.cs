using JetReportDesigner.Core.Binding;
using JetReportDesigner.Core.Model;

namespace JetReportDesigner.Rendering.Layout;

/// <summary>
/// Turns one <see cref="ReportElement"/> into render primitives at a given offset
/// (0,0 for free layout; the band origin for banded layout). Shared by both builders.
/// </summary>
public static class ElementEmitter
{
    /// <param name="aggregateText">
    /// When the element carries an aggregate, returns its pre-computed formatted text;
    /// null means "resolve normally".
    /// </param>
    public static IEnumerable<RenderPrimitive> Emit(
        ReportElement element,
        IReadOnlyDictionary<string, ReportStyle> styles,
        BindingContext context,
        double offsetX,
        double offsetY,
        Func<ReportElement, string?>? aggregateText = null)
    {
        if (!IsVisible(element, context))
        {
            yield break;
        }

        var b = element.Bounds;
        var x = b.X + offsetX;
        var y = b.Y + offsetY;
        var style = EffectiveStyle.Resolve(element, styles);

        switch (element.Type)
        {
            case ElementType.Line:
            {
                var vertical = element.Line?.Orientation.Equals("vertical", StringComparison.OrdinalIgnoreCase) ?? false;
                var thickness = style.Border is { } lb && MaxEdge(lb) > 0 ? MaxEdge(lb) : 1;
                yield return new LinePrimitive
                {
                    X = x,
                    Y = y,
                    X2 = vertical ? x : x + b.Width,
                    Y2 = vertical ? y + b.Height : y,
                    ThicknessPx = thickness,
                    ColorHex = style.Border?.Color ?? style.Color,
                };
                yield break;
            }

            case ElementType.Rectangle:
                yield return new RectanglePrimitive
                {
                    X = x, Y = y, Width = b.Width, Height = b.Height,
                    FillColorHex = style.Background,
                    BorderThicknessPx = style.Border is { } rb ? MaxEdge(rb) : 1,
                    BorderColorHex = style.Border?.Color ?? style.Color,
                };
                yield break;

            case ElementType.Image:
                if (style.Border is { } ib)
                {
                    yield return new RectanglePrimitive
                    {
                        X = x, Y = y, Width = b.Width, Height = b.Height,
                        BorderThicknessPx = MaxEdge(ib), BorderColorHex = ib.Color,
                    };
                }

                yield break;

            case ElementType.Label:
            case ElementType.Field:
            case ElementType.PageInfo:
                foreach (var p in TextBox(element, style, context, x, y, aggregateText))
                {
                    yield return p;
                }

                yield break;

            case ElementType.Table:
                yield break; // tables are placed by the builders themselves (slice C)

            default:
                yield break;
        }
    }

    private static IEnumerable<RenderPrimitive> TextBox(
        ReportElement element,
        EffectiveStyle style,
        BindingContext context,
        double x,
        double y,
        Func<ReportElement, string?>? aggregateText)
    {
        var b = element.Bounds;

        if (style.Background is { } bg)
        {
            yield return new RectanglePrimitive { X = x, Y = y, Width = b.Width, Height = b.Height, FillColorHex = bg, BorderThicknessPx = 0 };
        }

        if (style.Border is { } border && MaxEdge(border) > 0)
        {
            yield return new RectanglePrimitive { X = x, Y = y, Width = b.Width, Height = b.Height, BorderThicknessPx = MaxEdge(border), BorderColorHex = border.Color };
        }

        var text =
            element.Type == ElementType.Label ? BindingResolver.ResolveText(element.Text, context)
            : element.Aggregate != AggregateFunction.None && aggregateText?.Invoke(element) is { } agg ? agg
            : BindingResolver.ResolveValue(element.Value, element.Format, context);

        yield return new TextPrimitive
        {
            X = x + style.Padding.Left,
            Y = y + style.Padding.Top,
            Width = Math.Max(0, b.Width - style.Padding.Left - style.Padding.Right),
            Height = Math.Max(0, b.Height - style.Padding.Top - style.Padding.Bottom),
            Text = text,
            FontFamily = style.FontFamily,
            FontSizePt = style.FontSizePt,
            Bold = style.Bold,
            Italic = style.Italic,
            ColorHex = style.Color,
            HAlign = style.Align switch
            {
                TextAlign.Center => HorizontalAnchor.Center,
                TextAlign.Right => HorizontalAnchor.Right,
                _ => HorizontalAnchor.Left,
            },
            VAlign = style.VAlign switch
            {
                VerticalAlign.Middle => VerticalAnchor.Middle,
                VerticalAlign.Bottom => VerticalAnchor.Bottom,
                _ => VerticalAnchor.Top,
            },
        };
    }

    private static bool IsVisible(ReportElement element, BindingContext context)
    {
        if (string.IsNullOrWhiteSpace(element.VisibleWhen))
        {
            return true;
        }

        var resolved = BindingResolver.ResolveValue(element.VisibleWhen, null, context).Trim();
        return !resolved.Equals("false", StringComparison.OrdinalIgnoreCase) && resolved is not "0" and not "";
    }

    internal static double MaxEdge(BorderSpec b) => Math.Max(Math.Max(b.Top, b.Right), Math.Max(b.Bottom, b.Left));
}
