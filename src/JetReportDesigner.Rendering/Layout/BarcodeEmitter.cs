using JetReportDesigner.Core.Binding;
using JetReportDesigner.Core.Model;
using ZXing;
using ZXing.Common;

namespace JetReportDesigner.Rendering.Layout;

/// <summary>
/// Turns a <see cref="ElementType.Barcode"/> element into a background fill plus
/// filled bars/modules, using ZXing.Net purely as an encoder — it hands back a
/// <see cref="BitMatrix"/> of dark/light modules, which this emitter draws itself
/// (consecutive dark modules in a row are merged into one rectangle) so it fits the
/// same vector pipeline as everything else. A 1D symbology reserves a strip at the
/// bottom for the human-readable value; an encoding failure (e.g. non-numeric EAN-13)
/// becomes a small error box rather than failing the export.
/// </summary>
public static class BarcodeEmitter
{
    private const double TextReservePx = 14;

    public static IEnumerable<RenderPrimitive> Emit(
        ReportElement element,
        BindingContext context,
        double offsetX,
        double offsetY)
    {
        var spec = element.Barcode;
        if (spec is null)
        {
            yield break;
        }

        var b = element.Bounds;
        var x0 = b.X + offsetX;
        var y0 = b.Y + offsetY;

        var value = BindingResolver.ResolveValue(spec.Value, null, context);
        if (string.IsNullOrWhiteSpace(value))
        {
            yield break;
        }

        var format = ParseFormat(spec.Symbology);
        var is2D = format is BarcodeFormat.QR_CODE or BarcodeFormat.DATA_MATRIX;
        var textReserve = !is2D && spec.ShowText ? TextReservePx : 0;
        var barcodeHeight = Math.Max(1, b.Height - textReserve);

        BitMatrix? matrix = null;
        try
        {
            var hints = new Dictionary<EncodeHintType, object> { [EncodeHintType.MARGIN] = 0 };
            matrix = new MultiFormatWriter().encode(value, format, 0, 0, hints);
        }
        catch (Exception ex) when (ex is WriterException or ArgumentException or System.FormatException)
        {
            matrix = null;
        }

        if (matrix is null)
        {
            foreach (var p in ErrorBox(x0, y0, b.Width, b.Height, $"Invalid {spec.Symbology} value"))
            {
                yield return p;
            }

            yield break;
        }

        yield return new RectanglePrimitive
        {
            X = x0, Y = y0, Width = b.Width, Height = b.Height,
            FillColorHex = spec.BackColor, BorderThicknessPx = 0,
        };

        double moduleW, moduleH, ox, oy;
        if (is2D)
        {
            var moduleSize = Math.Min(b.Width / matrix.Width, barcodeHeight / matrix.Height);
            moduleW = moduleH = moduleSize;
            ox = x0 + (b.Width - moduleSize * matrix.Width) / 2;
            oy = y0 + (barcodeHeight - moduleSize * matrix.Height) / 2;
        }
        else
        {
            moduleW = b.Width / matrix.Width;
            moduleH = barcodeHeight;
            ox = x0;
            oy = y0;
        }

        for (var row = 0; row < matrix.Height; row++)
        {
            foreach (var (start, length) in DarkRuns(matrix, row))
            {
                yield return new RectanglePrimitive
                {
                    X = ox + start * moduleW,
                    Y = oy + row * moduleH,
                    Width = length * moduleW,
                    Height = moduleH,
                    FillColorHex = spec.ForeColor,
                    BorderThicknessPx = 0,
                };
            }
        }

        if (!is2D && spec.ShowText)
        {
            yield return new TextPrimitive
            {
                X = x0, Y = y0 + barcodeHeight, Width = b.Width, Height = textReserve,
                Text = value, FontFamily = "Consolas", FontSizePt = 8, ColorHex = spec.ForeColor,
                HAlign = HorizontalAnchor.Center, VAlign = VerticalAnchor.Middle,
            };
        }
    }

    private static IEnumerable<(int Start, int Length)> DarkRuns(BitMatrix matrix, int row)
    {
        var start = -1;
        for (var x = 0; x <= matrix.Width; x++)
        {
            var dark = x < matrix.Width && matrix[x, row];
            if (dark && start < 0)
            {
                start = x;
            }
            else if (!dark && start >= 0)
            {
                yield return (start, x - start);
                start = -1;
            }
        }
    }

    private static BarcodeFormat ParseFormat(string? symbology) => symbology?.Trim().ToLowerInvariant() switch
    {
        "code128" => BarcodeFormat.CODE_128,
        "ean13" => BarcodeFormat.EAN_13,
        "code39" => BarcodeFormat.CODE_39,
        "datamatrix" => BarcodeFormat.DATA_MATRIX,
        _ => BarcodeFormat.QR_CODE,
    };

    private static IEnumerable<RenderPrimitive> ErrorBox(double x, double y, double w, double h, string message)
    {
        yield return new RectanglePrimitive { X = x, Y = y, Width = w, Height = h, BorderThicknessPx = 1, BorderColorHex = "#dc2626" };
        yield return new TextPrimitive
        {
            X = x + 4, Y = y + 4, Width = Math.Max(0, w - 8), Height = Math.Max(0, h - 8),
            Text = message, FontSizePt = 8, ColorHex = "#dc2626",
        };
    }
}
