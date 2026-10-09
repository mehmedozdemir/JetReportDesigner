using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace JetReportDesigner.Core.Model;

/// <summary>
/// The "unique name" a program uses to call a report: <c>barkod-rapor-claude</c>. Always stored
/// lowercase, ASCII only, no spaces, so it is safe in URLs, config files and source code.
/// </summary>
public static partial class ReportCode
{
    public const int MaxLength = 64;

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9_-]{0,63}$")]
    private static partial Regex Pattern();

    public static bool IsValid(string? code) => code is not null && Pattern().IsMatch(code.Trim());

    /// <summary>The stored form of a valid code: trimmed and lowercase.</summary>
    public static string Normalize(string code) => code.Trim().ToLowerInvariant();

    /// <summary>
    /// Turns any title into a code: Turkish and other accented letters become their plain ASCII
    /// letter (ç→c, ğ→g, ı/İ→i, ö→o, ş→s, ü→u), everything else that is not a letter or digit becomes
    /// a single '-'. "Barkod Rapor - Claude" → "barkod-rapor-claude".
    /// </summary>
    public static string FromName(string? name)
    {
        var sb = new StringBuilder();
        var pendingSeparator = false;

        foreach (var ch in (name ?? string.Empty).Normalize(NormalizationForm.FormD))
        {
            var mapped = ch switch
            {
                'ı' or 'İ' => 'i',
                'ß' => 's',
                'ø' or 'Ø' => 'o',
                'đ' or 'Đ' => 'd',
                'ł' or 'Ł' => 'l',
                _ => ch,
            };

            if (CharUnicodeInfo.GetUnicodeCategory(mapped) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            var lower = char.ToLowerInvariant(mapped);
            if (lower is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                if (pendingSeparator && sb.Length > 0)
                {
                    sb.Append('-');
                }

                pendingSeparator = false;
                sb.Append(lower);
            }
            else
            {
                pendingSeparator = true;
            }
        }

        var code = sb.Length > MaxLength ? sb.ToString(0, MaxLength).TrimEnd('-') : sb.ToString();
        return code.Length == 0 ? "report" : code;
    }

    /// <summary>
    /// <paramref name="baseCode"/> if free, otherwise the first of <c>base-2</c>, <c>base-3</c>, … that is
    /// (shortening the base so the suffix always fits).
    /// </summary>
    public static string MakeUnique(string baseCode, Func<string, bool> exists)
    {
        if (!exists(baseCode))
        {
            return baseCode;
        }

        for (var n = 2; ; n++)
        {
            var suffix = "-" + n.ToString(CultureInfo.InvariantCulture);
            var stem = baseCode.Length + suffix.Length > MaxLength ? baseCode[..(MaxLength - suffix.Length)].TrimEnd('-') : baseCode;
            var candidate = stem + suffix;
            if (!exists(candidate))
            {
                return candidate;
            }
        }
    }
}
