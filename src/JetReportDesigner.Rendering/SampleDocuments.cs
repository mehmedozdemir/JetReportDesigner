namespace JetReportDesigner.Rendering;

/// <summary>Hand-built <see cref="RenderDocument"/> fixtures used by the Phase 0 PDF-engine spike and the "hello world" smoke test.</summary>
public static class SampleDocuments
{
    // A4 at 96 dpi.
    private const double A4WidthPx = 794;
    private const double A4HeightPx = 1123;

    /// <summary>A single A4 page exercising text alignment, a rule and a bordered box.</summary>
    public static RenderDocument HelloWorld() => new()
    {
        PageWidthPx = A4WidthPx,
        PageHeightPx = A4HeightPx,
        Pages =
        [
            new RenderPage
            {
                Primitives =
                [
                    new TextPrimitive
                    {
                        X = 40, Y = 40, Width = 714, Height = 32,
                        Text = "JetReportDesigner", FontSizePt = 20, Bold = true, ColorHex = "#111827",
                    },
                    new TextPrimitive
                    {
                        X = 40, Y = 76, Width = 714, Height = 18,
                        Text = "Phase 0 - PDF engine spike", FontSizePt = 11, ColorHex = "#6B7280",
                    },
                    new LinePrimitive { X = 40, Y = 104, X2 = 754, Y2 = 104, ThicknessPx = 1, ColorHex = "#D1D5DB" },
                    new RectanglePrimitive
                    {
                        X = 40, Y = 130, Width = 300, Height = 90,
                        BorderThicknessPx = 1, BorderColorHex = "#9CA3AF", FillColorHex = "#F9FAFB",
                    },
                    new TextPrimitive
                    {
                        X = 40, Y = 130, Width = 300, Height = 90,
                        Text = "Hello, world.", FontSizePt = 14,
                        HAlign = HorizontalAlign.Center, VAlign = VerticalAlign.Middle,
                    },
                ],
            },
        ],
    };
}
