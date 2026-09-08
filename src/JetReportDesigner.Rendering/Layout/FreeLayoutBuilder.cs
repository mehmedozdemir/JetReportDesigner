using JetReportDesigner.Core.Binding;
using JetReportDesigner.Core.Model;
using JetReportDesigner.DataSources;

namespace JetReportDesigner.Rendering.Layout;

/// <summary>
/// Builds a <see cref="RenderDocument"/> from a free-layout report. Phase 1: a single
/// page, elements positioned absolutely, bindings resolved against row 0 of the
/// report's first data source. Bands, grouping and multi-page flow arrive later.
/// </summary>
public sealed class FreeLayoutBuilder
{
    public RenderDocument Build(
        ReportDefinition report,
        ReportData data,
        IReadOnlyDictionary<string, object?> parameters)
    {
        if (report.LayoutMode != LayoutMode.Free || report.Body is null)
        {
            throw new InvalidOperationException("FreeLayoutBuilder requires a free-layout report with a body.");
        }

        var (pageWidth, pageHeight) = PageGeometry.Resolve(report.Page);
        var primarySource = report.DataSources.FirstOrDefault()?.Name ?? string.Empty;
        var context = new BindingContext(data.Row(primarySource, 0), parameters);

        var primitives = new List<RenderPrimitive>();
        foreach (var element in report.Body.Elements)
        {
            if (!IsVisible(element, context))
            {
                continue;
            }

            primitives.AddRange(Emit(element, report.Styles, context));
        }

        return new RenderDocument
        {
            PageWidthPx = pageWidth,
            PageHeightPx = pageHeight,
            Pages = [new RenderPage { Primitives = primitives }],
        };
    }

    private static bool IsVisible(ReportElement element, BindingContext context)
    {
        if (string.IsNullOrWhiteSpace(element.VisibleWhen))
        {
            return true;
        }

        // Phase 1: only a bare binding is understood; expressions arrive in Phase 2.
        var resolved = BindingResolver.ResolveValue(element.VisibleWhen, null, context).Trim();
        return !resolved.Equals("false", StringComparison.OrdinalIgnoreCase) && resolved is not "0" and not "";
    }

    private static IEnumerable<RenderPrimitive> Emit(
        ReportElement element,
        IReadOnlyDictionary<string, ReportStyle> styles,
        BindingContext context)
    {
        var b = element.Bounds;
        var style = EffectiveStyle.Resolve(element, styles);

        switch (element.Type)
        {
            case ElementType.Line:
                yield return BuildLine(element, b, style);
                yield break;

            case ElementType.Rectangle:
                yield return new RectanglePrimitive
                {
                    X = b.X,
                    Y = b.Y,
                    Width = b.Width,
                    Height = b.Height,
                    FillColorHex = style.Background,
                    BorderThicknessPx = style.Border is { } br ? MaxEdge(br) : 1,
                    BorderColorHex = style.Border?.Color ?? style.Color,
                };
                yield break;

            case ElementType.Image:
                // Image rendering lands with the font/asset work later in Phase 1;
                // for now draw its frame so layout is visible.
                if (style.Border is not null)
                {
                    yield return new RectanglePrimitive
                    {
                        X = b.X, Y = b.Y, Width = b.Width, Height = b.Height,
                        BorderThicknessPx = MaxEdge(style.Border), BorderColorHex = style.Border.Color,
                    };
                }

                yield break;

            case ElementType.Label:
            case ElementType.Field:
            case ElementType.PageInfo:
                foreach (var p in BuildTextBox(element, b, style, context))
                {
                    yield return p;
                }

                yield break;

            case ElementType.Table:
                // Tables are a banded/Phase 2 concern; ignored in free layout for now.
                yield break;

            default:
                yield break;
        }
    }

    private static IEnumerable<RenderPrimitive> BuildTextBox(
        ReportElement element,
        Bounds b,
        EffectiveStyle style,
        BindingContext context)
    {
        if (style.Background is { } bg)
        {
            yield return new RectanglePrimitive
            {
                X = b.X, Y = b.Y, Width = b.Width, Height = b.Height,
                FillColorHex = bg, BorderThicknessPx = 0,
            };
        }

        if (style.Border is { } border && MaxEdge(border) > 0)
        {
            yield return new RectanglePrimitive
            {
                X = b.X, Y = b.Y, Width = b.Width, Height = b.Height,
                BorderThicknessPx = MaxEdge(border), BorderColorHex = border.Color,
            };
        }

        var text = element.Type == ElementType.Label
            ? BindingResolver.ResolveText(element.Text, context)
            : BindingResolver.ResolveValue(element.Value, element.Format, context);

        yield return new TextPrimitive
        {
            X = b.X + style.Padding.Left,
            Y = b.Y + style.Padding.Top,
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

    private static LinePrimitive BuildLine(ReportElement element, Bounds b, EffectiveStyle style)
    {
        var vertical = element.Line?.Orientation.Equals("vertical", StringComparison.OrdinalIgnoreCase) ?? false;
        var thickness = style.Border is { } br && MaxEdge(br) > 0 ? MaxEdge(br) : 1;
        return new LinePrimitive
        {
            X = b.X,
            Y = b.Y,
            X2 = vertical ? b.X : b.X + b.Width,
            Y2 = vertical ? b.Y + b.Height : b.Y,
            ThicknessPx = thickness,
            ColorHex = style.Border?.Color ?? style.Color,
        };
    }

    private static double MaxEdge(BorderSpec b) => Math.Max(Math.Max(b.Top, b.Right), Math.Max(b.Bottom, b.Left));
}
