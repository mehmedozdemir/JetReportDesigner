using JetReportDesigner.Core.Model;

namespace JetReportDesigner.Rendering.Layout;

/// <summary>Page dimensions in 1/96 inch units ("px").</summary>
public static class PageGeometry
{
    // Named sizes at 96 dpi, portrait (width, height).
    private static readonly Dictionary<string, (double W, double H)> Sizes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["A4"] = (794, 1123),
        ["A5"] = (559, 794),
        ["Letter"] = (816, 1056),
        ["Legal"] = (816, 1344),
    };

    public static (double Width, double Height) Resolve(PageSetup page)
    {
        double w, h;
        if (page.Size.Equals("Custom", StringComparison.OrdinalIgnoreCase))
        {
            w = page.CustomWidth ?? 794;
            h = page.CustomHeight ?? 1123;
        }
        else if (Sizes.TryGetValue(page.Size, out var s))
        {
            (w, h) = s;
        }
        else
        {
            (w, h) = Sizes["A4"];
        }

        return page.Orientation.Equals("landscape", StringComparison.OrdinalIgnoreCase) ? (h, w) : (w, h);
    }
}
