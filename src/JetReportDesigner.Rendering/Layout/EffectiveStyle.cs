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

    public static EffectiveStyle Resolve(ReportElement element, IReadOnlyDictionary<string, ReportStyle> named)
    {
        ReportStyle? baseStyle = element.StyleRef is { } key && named.TryGetValue(key, out var s) ? s : null;
        var inline = element.Style;

        FontSpec? font = Merge(baseStyle?.Font, inline?.Font);

        return new EffectiveStyle
        {
            FontFamily = font?.Family ?? "Helvetica",
            FontSizePt = font?.Size ?? 10,
            Bold = font?.Bold ?? false,
            Italic = font?.Italic ?? false,
            Underline = font?.Underline ?? false,
            Color = inline?.Color ?? baseStyle?.Color ?? "#000000",
            Background = inline?.Background ?? baseStyle?.Background,
            Align = inline?.Align ?? baseStyle?.Align ?? TextAlign.Left,
            VAlign = inline?.VAlign ?? baseStyle?.VAlign ?? VerticalAlign.Top,
            Border = inline?.Border ?? baseStyle?.Border,
            Padding = inline?.Padding ?? baseStyle?.Padding ?? new Spacing(),
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
