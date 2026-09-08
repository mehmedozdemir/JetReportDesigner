using System.Collections.Concurrent;
using PdfSharp.Fonts;

namespace JetReportDesigner.Rendering.Engines;

/// <summary>
/// Resolves PDF fonts from the host's installed fonts. PdfSharp ships none, so:
/// on Windows it reads the core set from %WINDIR%\Fonts (Arial / Times New Roman /
/// Courier New); on Linux it scans the standard font directories for the
/// Liberation or DejaVu families (metric-compatible with the Windows core fonts).
/// The container image installs <c>fonts-liberation</c>.
/// </summary>
public sealed class SystemFontResolver : IFontResolver
{
    private static readonly string[] LinuxFontDirs =
    [
        "/usr/share/fonts", "/usr/local/share/fonts", "/Library/Fonts", "/System/Library/Fonts",
    ];

    private readonly ConcurrentDictionary<string, byte[]> _cache = new(StringComparer.OrdinalIgnoreCase);

    private enum Family { Sans, Serif, Mono }

    public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        var family = Classify(familyName);
        var suffix = (isBold, isItalic) switch
        {
            (true, true) => "BI",
            (true, false) => "B",
            (false, true) => "I",
            _ => "R",
        };
        return new FontResolverInfo($"{family}-{suffix}");
    }

    public byte[] GetFont(string faceName)
    {
        return _cache.GetOrAdd(faceName, key =>
        {
            var parts = key.Split('-');
            var family = Enum.Parse<Family>(parts[0]);
            var bold = parts[1].Contains('B');
            var italic = parts[1].Contains('I');

            var path = OperatingSystem.IsWindows()
                ? FindWindowsFont(family, bold, italic)
                : FindUnixFont(family, bold, italic);

            if (path is null || !File.Exists(path))
            {
                throw new InvalidOperationException(
                    $"No system font found for {family} (bold={bold}, italic={italic}). "
                    + "Install 'fonts-liberation' (Linux) — see docs/04-pdf-motoru-karari.md.");
            }

            return File.ReadAllBytes(path);
        });
    }

    private static Family Classify(string familyName)
    {
        var f = familyName.ToLowerInvariant();
        if (f.Contains("courier") || f.Contains("mono") || f.Contains("consol"))
        {
            return Family.Mono;
        }

        if (f.Contains("times") || f.Contains("serif") || f.Contains("georgia") || f.Contains("roman"))
        {
            return Family.Serif;
        }

        return Family.Sans;
    }

    private static string FindWindowsFont(Family family, bool bold, bool italic)
    {
        var dir = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        var (baseName, b, i, bi) = family switch
        {
            Family.Mono => ("cour", "courbd", "couri", "courbi"),
            Family.Serif => ("times", "timesbd", "timesi", "timesbi"),
            _ => ("arial", "arialbd", "ariali", "arialbi"),
        };
        var file = (bold, italic) switch { (true, true) => bi, (true, false) => b, (false, true) => i, _ => baseName };
        return Path.Combine(dir, file + ".ttf");
    }

    private static string? FindUnixFont(Family family, bool bold, bool italic)
    {
        var style = (bold, italic) switch
        {
            (true, true) => "BoldItalic",
            (true, false) => "Bold",
            (false, true) => "Italic",
            _ => "Regular",
        };

        string[] candidates = family switch
        {
            Family.Mono => [$"LiberationMono-{style}.ttf", $"DejaVuSansMono{UnixStyle(style)}.ttf"],
            Family.Serif => [$"LiberationSerif-{style}.ttf", $"DejaVuSerif{UnixStyle(style)}.ttf"],
            _ => [$"LiberationSans-{style}.ttf", $"DejaVuSans{UnixStyle(style)}.ttf"],
        };

        foreach (var root in LinuxFontDirs.Where(Directory.Exists))
        {
            foreach (var name in candidates)
            {
                var hit = Directory.EnumerateFiles(root, name, SearchOption.AllDirectories).FirstOrDefault();
                if (hit is not null)
                {
                    return hit;
                }
            }
        }

        // Last resort: any ttf that looks like the family.
        var keyword = family switch { Family.Mono => "Mono", Family.Serif => "Serif", _ => "Sans" };
        return LinuxFontDirs
            .Where(Directory.Exists)
            .SelectMany(d => Directory.EnumerateFiles(d, "*.ttf", SearchOption.AllDirectories))
            .FirstOrDefault(p => p.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private static string UnixStyle(string style) => style == "Regular" ? string.Empty : $"-{style}";
}
