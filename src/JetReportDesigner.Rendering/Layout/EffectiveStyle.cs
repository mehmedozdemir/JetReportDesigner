using JetReportDesigner.Core.Binding;
using JetReportDesigner.Core.Model;

namespace JetReportDesigner.Rendering.Layout;

/// <summary>A fully-resolved style: named style (<c>styleRef</c>) overlaid by the element's inline style, with defaults filled in.</summary>
public sealed class EffectiveStyle
{
    public string FontFamily { get; init; } = "Helvetica";
    public double FontSizePt { get; init; } = 10;
    public bool Bold { get; init; }
    public bool Italic { get; init; }
    public bool Underline { get; init; }
    public string Color { get; init; } = "#000000";
    public string? Background { get; init; }
    public TextAlign Align { get; init; } = TextAlign.Left;
    public VerticalAlign VAlign { get; init; } = VerticalAlign.Top;
    public BorderSpec? Border { get; init; }
    public Spacing Padding { get; init; } = new();

    /// <summary>A matching conditional-formatting rule asked for the element to be hidden.</summary>
    public bool Hidden { get; init; }

    /// <summary>
    /// Resolves the effective style. Layers, lowest first: named style, the element's
    /// inline style, any <paramref name="inherited"/> styles (e.g. from a band's matching
    /// rules), then the element's own matching conditional-formatting rules in order.
    /// </summary>
    public static EffectiveStyle Resolve(
        ReportElement element,
        IReadOnlyDictionary<string, ReportStyle> named,
        BindingContext context,
        IReadOnlyList<ReportStyle>? inherited = null)
    {
        ReportStyle? baseStyle = element.StyleRef is { } key && named.TryGetValue(key, out var s) ? s : null;
        var (ruleStyles, hidden) = FormatRuleEvaluator.Apply(element.FormatRules, context);

        List<ReportStyle?> layers = [baseStyle, element.Style];
        if (inherited is { Count: > 0 })
        {
            layers.AddRange(inherited);
        }

        layers.AddRange(ruleStyles);

        FontSpec? font = null;
        string? color = null;
        string? background = null;
        TextAlign? align = null;
        VerticalAlign? vAlign = null;
        BorderSpec? border = null;
        Spacing? padding = null;

        foreach (var layer in layers)
        {
            if (layer is null)
            {
                continue;
            }

            font = Merge(font, layer.Font);
            color = layer.Color ?? color;
            background = layer.Background ?? background;
            align = layer.Align ?? align;
            vAlign = layer.VAlign ?? vAlign;
            border = layer.Border ?? border;
            padding = layer.Padding ?? padding;
        }

        return new EffectiveStyle
        {
            FontFamily = font?.Family ?? "Helvetica",
            FontSizePt = font?.Size ?? 10,
            Bold = font?.Bold ?? false,
            Italic = font?.Italic ?? false,
            Underline = font?.Underline ?? false,
            Color = color ?? "#000000",
            Background = background,
            Align = align ?? TextAlign.Left,
            VAlign = vAlign ?? VerticalAlign.Top,
            Border = border,
            Padding = padding ?? new Spacing(),
            Hidden = hidden,
        };
    }

    private static FontSpec? Merge(FontSpec? baseFont, FontSpec? over)
    {
        if (baseFont is null)
        {
            return over;
        }

        if (over is null)
        {
            return baseFont;
        }

        return new FontSpec
        {
            Family = over.Family ?? baseFont.Family,
            Size = over.Size ?? baseFont.Size,
            Bold = over.Bold ?? baseFont.Bold,
            Italic = over.Italic ?? baseFont.Italic,
            Underline = over.Underline ?? baseFont.Underline,
        };
    }
}
