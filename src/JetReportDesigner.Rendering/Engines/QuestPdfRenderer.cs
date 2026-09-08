using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace JetReportDesigner.Rendering.Engines;

/// <summary>
/// PDF engine candidate #1. QuestPDF renders via SkiaSharp and bundles its own
/// fonts, so it needs no OS font provisioning — a notable deployment advantage.
/// Its licence is free only below a revenue threshold (Community).
/// </summary>
public sealed class QuestPdfRenderer : IPdfRenderer
{
    static QuestPdfRenderer() => QuestPDF.Settings.License = LicenseType.Community;

    public string EngineName => "questpdf";

    public byte[] Render(RenderDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var widthPt = (float)RenderUnits.ToPoints(document.PageWidthPx);
        var heightPt = (float)RenderUnits.ToPoints(document.PageHeightPx);

        return Document.Create(container =>
        {
            foreach (var page in document.Pages)
            {
                container.Page(p =>
                {
                    p.Size(widthPt, heightPt, Unit.Point);
                    p.Margin(0);
                    p.Content().Layers(layers =>
                    {
                        layers.PrimaryLayer().Width(widthPt).Height(heightPt);
                        foreach (var primitive in page.Primitives)
                        {
                            Place(layers.Layer(), primitive);
                        }
                    });
                });
            }
        }).GeneratePdf();
    }

    private static void Place(IContainer layer, RenderPrimitive primitive)
    {
        var x = (float)RenderUnits.ToPoints(primitive.X);
        var y = (float)RenderUnits.ToPoints(primitive.Y);
        var positioned = layer.OffsetX(x).OffsetY(y);

        switch (primitive)
        {
            case TextPrimitive text:
                RenderText(positioned, text);
                break;

            case LinePrimitive line:
                RenderLine(positioned, line);
                break;

            case RectanglePrimitive rect:
                RenderRectangle(positioned, rect);
                break;
        }
    }

    private static void RenderText(IContainer container, TextPrimitive text)
    {
        var box = container
            .Width((float)RenderUnits.ToPoints(text.Width))
            .Height((float)RenderUnits.ToPoints(text.Height));

        box = text.HAlign switch
        {
            HorizontalAlign.Center => box.AlignCenter(),
            HorizontalAlign.Right => box.AlignRight(),
            _ => box.AlignLeft(),
        };
        box = text.VAlign switch
        {
            VerticalAlign.Middle => box.AlignMiddle(),
            VerticalAlign.Bottom => box.AlignBottom(),
            _ => box.AlignTop(),
        };

        box.Text(span =>
        {
            var run = span.Span(text.Text)
                .FontFamily(text.FontFamily)
                .FontSize((float)text.FontSizePt)
                .FontColor(Color.FromHex(text.ColorHex));
            if (text.Bold)
            {
                run.Bold();
            }

            if (text.Italic)
            {
                run.Italic();
            }
        });
    }

    private static void RenderLine(IContainer container, LinePrimitive line)
    {
        var lengthPx = Math.Sqrt(Math.Pow(line.X2 - line.X, 2) + Math.Pow(line.Y2 - line.Y, 2));
        var isVertical = Math.Abs(line.Y2 - line.Y) > Math.Abs(line.X2 - line.X);
        var thicknessPt = (float)RenderUnits.ToPoints(line.ThicknessPx);
        var lengthPt = (float)RenderUnits.ToPoints(lengthPx);
        var color = Color.FromHex(line.ColorHex);

        if (isVertical)
        {
            container.Width(thicknessPt).Height(lengthPt).Background(color);
        }
        else
        {
            container.Width(lengthPt).Height(thicknessPt).Background(color);
        }
    }

    private static void RenderRectangle(IContainer container, RectanglePrimitive rect)
    {
        var box = container
            .Width((float)RenderUnits.ToPoints(rect.Width))
            .Height((float)RenderUnits.ToPoints(rect.Height));

        if (rect.FillColorHex is { } fill)
        {
            box = box.Background(Color.FromHex(fill));
        }

        if (rect.BorderThicknessPx > 0)
        {
            box.Border((float)RenderUnits.ToPoints(rect.BorderThicknessPx))
                .BorderColor(Color.FromHex(rect.BorderColorHex));
        }
    }
}
