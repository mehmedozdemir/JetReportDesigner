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
        Func<ReportElement, string?>? aggregateText = null,
        IReadOnlyList<ReportStyle>? inheritedStyles = null)
    {
        if (!IsVisible(element, context))
        {
            yield break;
        }

        var b = element.Bounds;
        var x = b.X + offsetX;
        var y = b.Y + offsetY;
        var style = EffectiveStyle.Resolve(element, styles, context, inheritedStyles);

        if (style.Hidden)
        {
            yield break;
        }

        if (element.Type != ElementType.Line
            && style.BackgroundImage is { Source: { Length: > 0 } bgSource } bgSpec)
        {
            yield return new ImagePrimitive
            {
                X = x, Y = y, Width = b.Width, Height = b.Height,
                Source = bgSource, Fit = ParseFit(bgSpec.Fit),
            };
        }

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
            {
                if (style.Background is { } rbg)
                {
                    yield return new RectanglePrimitive
                    {
                        X = x, Y = y, Width = b.Width, Height = b.Height,
                        FillColorHex = rbg, BorderThicknessPx = 0,
                    };
                }

                var rectBorder = style.Border
                    ?? new BorderSpec { Top = 1, Right = 1, Bottom = 1, Left = 1, Color = style.Color };
                foreach (var p in BorderPrimitives(x, y, b.Width, b.Height, rectBorder))
                {
                    yield return p;
                }

                yield break;
            }

            case ElementType.Image:
            {
                if (element.Image is { Source: { Length: > 0 } imgSource })
                {
                    yield return new ImagePrimitive
                    {
                        X = x, Y = y, Width = b.Width, Height = b.Height,
                        Source = imgSource, Fit = ParseFit(element.Image.Fit),
                    };
                }

                if (style.Border is { } ib)
                {
                    foreach (var p in BorderPrimitives(x, y, b.Width, b.Height, ib))
                    {
                        yield return p;
                    }
                }

                yield break;
            }

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
            foreach (var p in BorderPrimitives(x, y, b.Width, b.Height, border))
            {
                yield return p;
            }
        }

        var text =
            element.Type == ElementType.Label ? BindingResolver.ResolveValue(element.Text, element.Format, context)
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

    internal static ImageFit ParseFit(string? fit) => fit?.ToLowerInvariant() switch
    {
        "contain" => ImageFit.Contain,
        "fill" or "stretch" => ImageFit.Fill,
        "tile" => ImageFit.Tile,
        _ => ImageFit.Cover,
    };

    /// <summary>
    /// Border render primitives for a box. A uniform border (all four edges equal and
    /// positive) is one stroked rectangle; otherwise each positive edge is its own line.
    /// </summary>
    internal static IEnumerable<RenderPrimitive> BorderPrimitives(
        double x, double y, double w, double h, BorderSpec border)
    {
        if (border.Top > 0 && border.Top == border.Right && border.Right == border.Bottom && border.Bottom == border.Left)
        {
            yield return new RectanglePrimitive
            {
                X = x, Y = y, Width = w, Height = h,
                BorderThicknessPx = border.Top, BorderColorHex = border.Color,
            };
            yield break;
        }

        if (border.Top > 0)
        {
            yield return new LinePrimitive { X = x, Y = y, X2 = x + w, Y2 = y, ThicknessPx = border.Top, ColorHex = border.Color };
        }

        if (border.Bottom > 0)
        {
            yield return new LinePrimitive { X = x, Y = y + h, X2 = x + w, Y2 = y + h, ThicknessPx = border.Bottom, ColorHex = border.Color };
        }

        if (border.Left > 0)
        {
            yield return new LinePrimitive { X = x, Y = y, X2 = x, Y2 = y + h, ThicknessPx = border.Left, ColorHex = border.Color };
        }

        if (border.Right > 0)
        {
            yield return new LinePrimitive { X = x + w, Y = y, X2 = x + w, Y2 = y + h, ThicknessPx = border.Right, ColorHex = border.Color };
        }
    }
}
