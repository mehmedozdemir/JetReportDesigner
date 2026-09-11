namespace JetReportDesigner.Rendering.Layout;

/// <summary>
/// Scales and translates a rendered primitive into a subreport placeholder's box, and
/// approximates "clip to height": a primitive whose top has already crossed the box's
/// bottom edge is dropped; a text or rectangle that starts inside but would overflow
/// has its height trimmed. Lines, polygons, wedges and images are left whole if their
/// top is inside the box — true per-pixel clipping would need a clip-region concept
/// neither render engine has today.
/// </summary>
internal static class PrimitiveTransform
{
    public static RenderPrimitive? ScaleInto(RenderPrimitive primitive, double scale, double dx, double dy, double boxBottom)
    {
        double X(double x) => x * scale + dx;
        double Y(double y) => y * scale + dy;
        double S(double v) => v * scale;

        switch (primitive)
        {
            case TextPrimitive t:
            {
                var y = Y(t.Y);
                if (y >= boxBottom)
                {
                    return null;
                }

                return new TextPrimitive
                {
                    X = X(t.X), Y = y, Width = S(t.Width), Height = Math.Min(S(t.Height), boxBottom - y),
                    Text = t.Text, FontFamily = t.FontFamily, FontSizePt = S(t.FontSizePt),
                    Bold = t.Bold, Italic = t.Italic, ColorHex = t.ColorHex, HAlign = t.HAlign, VAlign = t.VAlign,
                };
            }

            case RectanglePrimitive r:
            {
                var y = Y(r.Y);
                if (y >= boxBottom)
                {
                    return null;
                }

                return new RectanglePrimitive
                {
                    X = X(r.X), Y = y, Width = S(r.Width), Height = Math.Min(S(r.Height), boxBottom - y),
                    BorderThicknessPx = S(r.BorderThicknessPx), BorderColorHex = r.BorderColorHex, FillColorHex = r.FillColorHex,
                };
            }

            case LinePrimitive l:
            {
                var y = Y(l.Y);
                if (y >= boxBottom)
                {
                    return null;
                }

                return new LinePrimitive { X = X(l.X), Y = y, X2 = X(l.X2), Y2 = Y(l.Y2), ThicknessPx = S(l.ThicknessPx), ColorHex = l.ColorHex };
            }

            case ImagePrimitive img:
            {
                var y = Y(img.Y);
                if (y >= boxBottom)
                {
                    return null;
                }

                return new ImagePrimitive
                {
                    X = X(img.X), Y = y, Width = S(img.Width), Height = S(img.Height),
                    Source = img.Source, Fit = img.Fit, Bytes = img.Bytes, ContentType = img.ContentType,
                };
            }

            case PolygonPrimitive poly:
            {
                if (poly.Points.Count == 0 || Y(poly.Points[0].Y) >= boxBottom)
                {
                    return null;
                }

                return new PolygonPrimitive
                {
                    Points = poly.Points.Select(p => new PointPx(X(p.X), Y(p.Y))).ToList(),
                    FillColorHex = poly.FillColorHex, StrokeColorHex = poly.StrokeColorHex, StrokeWidthPx = S(poly.StrokeWidthPx),
                };
            }

            case WedgePrimitive w:
            {
                var y = Y(w.Y);
                if (y >= boxBottom)
                {
                    return null;
                }

                return new WedgePrimitive
                {
                    X = X(w.X), Y = y, Radius = S(w.Radius), StartAngleDeg = w.StartAngleDeg, SweepAngleDeg = w.SweepAngleDeg,
                    FillColorHex = w.FillColorHex, StrokeColorHex = w.StrokeColorHex, StrokeWidthPx = S(w.StrokeWidthPx),
                };
            }

            default:
                return null; // e.g. a nested, unresolved SubreportPrimitive — should not happen
        }
    }
}
