namespace JetReportDesigner.Core.Model;

/// <summary>
/// Visual styling for an element or a named style. Every property is nullable so
/// that a style can be layered: named style first, then the element's inline style,
/// with nulls meaning "inherit / not set".
/// </summary>
public sealed class ReportStyle
{
    public FontSpec? Font { get; set; }

    /// <summary>Foreground / text colour as <c>#rrggbb</c>.</summary>
    public string? Color { get; set; }

    /// <summary>Background fill as <c>#rrggbb</c>, or null for transparent.</summary>
    public string? Background { get; set; }

    public TextAlign? Align { get; set; }

    public VerticalAlign? VAlign { get; set; }

    public BorderSpec? Border { get; set; }

    public Spacing? Padding { get; set; }

    /// <summary>A background image layered behind the fill and content, or null for none.</summary>
    public BackgroundImageSpec? BackgroundImage { get; set; }
}

/// <summary>
/// A background image for an element, band or page. <see cref="Source"/> is an
/// <c>asset:{id}</c> reference, an <c>http(s)</c> URL or a data URI.
/// </summary>
public sealed class BackgroundImageSpec
{
    public string Source { get; set; } = string.Empty;

    /// <summary><c>cover</c> | <c>contain</c> | <c>fill</c> | <c>tile</c>. Default <c>cover</c>.</summary>
    public string Fit { get; set; } = "cover";
}

public sealed class FontSpec
{
    public string? Family { get; set; }

    /// <summary>Font size in points.</summary>
    public double? Size { get; set; }

    public bool? Bold { get; set; }

    public bool? Italic { get; set; }

    public bool? Underline { get; set; }
}

/// <summary>Per-edge border widths (1/96 inch) and a shared colour.</summary>
public sealed class BorderSpec
{
    public double Top { get; set; }
    public double Right { get; set; }
    public double Bottom { get; set; }
    public double Left { get; set; }
    public string Color { get; set; } = "#000000";
}

/// <summary>Per-edge spacing in 1/96 inch units.</summary>
public sealed class Spacing
{
    public double Top { get; set; }
    public double Right { get; set; }
    public double Bottom { get; set; }
    public double Left { get; set; }
}
