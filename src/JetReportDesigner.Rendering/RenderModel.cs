namespace JetReportDesigner.Rendering;

/// <summary>
/// Engine-independent intermediate representation produced from a report + data and
/// consumed by an <see cref="IPdfRenderer"/>. All coordinates are in 1/96 inch units
/// ("px"), origin top-left; renderers convert to their own unit system.
/// </summary>
public sealed class RenderDocument
{
    public required double PageWidthPx { get; init; }

    public required double PageHeightPx { get; init; }

    public IReadOnlyList<RenderPage> Pages { get; init; } = [];
}

public sealed class RenderPage
{
    public IReadOnlyList<RenderPrimitive> Primitives { get; init; } = [];
}

public abstract class RenderPrimitive
{
    public double X { get; init; }

    public double Y { get; init; }
}

public sealed class TextPrimitive : RenderPrimitive
{
    public double Width { get; init; }

    public double Height { get; init; }

    public string Text { get; init; } = string.Empty;

    public string FontFamily { get; init; } = "Helvetica";

    public double FontSizePt { get; init; } = 10;

    public bool Bold { get; init; }

    public bool Italic { get; init; }

    public string ColorHex { get; init; } = "#000000";

    public HorizontalAnchor HAlign { get; init; } = HorizontalAnchor.Left;

    public VerticalAnchor VAlign { get; init; } = VerticalAnchor.Top;
}

public sealed class LinePrimitive : RenderPrimitive
{
    public double X2 { get; init; }

    public double Y2 { get; init; }

    public double ThicknessPx { get; init; } = 1;

    public string ColorHex { get; init; } = "#000000";
}

public sealed class RectanglePrimitive : RenderPrimitive
{
    public double Width { get; init; }

    public double Height { get; init; }

    public double BorderThicknessPx { get; init; } = 1;

    public string BorderColorHex { get; init; } = "#000000";

    public string? FillColorHex { get; init; }
}

public enum ImageFit
{
    Cover,
    Contain,
    Fill,
    Tile,
}

/// <summary>
/// A bitmap drawn into a box. <see cref="Source"/> (an <c>asset:{id}</c> reference,
/// an http(s) URL or a data URI) is resolved to <see cref="Bytes"/> by
/// <c>ReportRenderService</c> before the engine runs; a primitive whose bytes could
/// not be resolved is skipped.
/// </summary>
public sealed class ImagePrimitive : RenderPrimitive
{
    public double Width { get; init; }

    public double Height { get; init; }

    public string Source { get; init; } = string.Empty;

    public ImageFit Fit { get; init; } = ImageFit.Cover;

    public byte[]? Bytes { get; set; }

    public string ContentType { get; set; } = "image/png";
}

public enum HorizontalAnchor
{
    Left,
    Center,
    Right,
}

public enum VerticalAnchor
{
    Top,
    Middle,
    Bottom,
}
