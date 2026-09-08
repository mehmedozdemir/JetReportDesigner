using System.Globalization;
using JetReportDesigner.Core.Model;

namespace JetReportDesigner.Core.Binding;

/// <summary>Evaluates <see cref="FormatRule"/> conditions against the current row.</summary>
public static class FormatRuleEvaluator
{
    /// <summary>Style overlays from every matching rule (in order), plus whether any match hides the element.</summary>
    public static (IReadOnlyList<ReportStyle> Styles, bool Hidden) Apply(
        IEnumerable<FormatRule>? rules,
        BindingContext context)
    {
        if (rules is null)
        {
            return (Array.Empty<ReportStyle>(), false);
        }

        List<ReportStyle> styles = [];
        var hidden = false;
        foreach (var rule in rules)
        {
            if (!Matches(rule, context))
            {
                continue;
            }

            styles.Add(rule.Style);
            hidden |= rule.Hidden;
        }

        return (styles, hidden);
    }

    public static bool Matches(FormatRule rule, BindingContext context)
    {
        if (string.IsNullOrWhiteSpace(rule.Field))
        {
            return false;
        }

        var key = rule.Field.Contains('.') ? rule.Field[(rule.Field.IndexOf('.') + 1)..] : rule.Field;
        context.Row.TryGetValue(key, out var raw);

        return rule.Op switch
        {
            ComparisonOp.IsEmpty => IsEmpty(raw),
            ComparisonOp.IsNotEmpty => !IsEmpty(raw),
            ComparisonOp.Contains => Text(raw).Contains(rule.Value, StringComparison.OrdinalIgnoreCase),
            ComparisonOp.StartsWith => Text(raw).StartsWith(rule.Value, StringComparison.OrdinalIgnoreCase),
            ComparisonOp.EndsWith => Text(raw).EndsWith(rule.Value, StringComparison.OrdinalIgnoreCase),
            _ => CompareOrdered(rule.Op, raw, rule.Value),
        };
    }

    private static bool IsEmpty(object? v) =>
        v is null || (v is string s && string.IsNullOrWhiteSpace(s));

    private static string Text(object? v) =>
        v is null ? string.Empty : Convert.ToString(v, CultureInfo.InvariantCulture) ?? string.Empty;

    private static bool CompareOrdered(ComparisonOp op, object? raw, string value)
    {
        var cmp = Compare(raw, value);
        return op switch
        {
            ComparisonOp.Eq => cmp == 0,
            ComparisonOp.Ne => cmp != 0,
            ComparisonOp.Gt => cmp > 0,
            ComparisonOp.Ge => cmp >= 0,
            ComparisonOp.Lt => cmp < 0,
            ComparisonOp.Le => cmp <= 0,
            _ => false,
        };
    }

    private static int Compare(object? raw, string value)
    {
        if (TryNumber(raw, out var ln)
            && double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var rn))
        {
            return ln.CompareTo(rn);
        }

        if (TryDate(raw, out var ld)
            && DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var rd))
        {
            return ld.CompareTo(rd);
        }

        if (raw is bool lb && bool.TryParse(value, out var rb))
        {
            return (lb ? 1 : 0).CompareTo(rb ? 1 : 0);
        }

        return string.Compare(Text(raw), value, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryNumber(object? v, out double n)
    {
        switch (v)
        {
            case double d: n = d; return true;
            case int i: n = i; return true;
            case long l: n = l; return true;
            case decimal m: n = (double)m; return true;
            case float f: n = f; return true;
            case string s: return double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out n);
            default: n = 0; return false;
        }
    }

    private static bool TryDate(object? v, out DateTime d)
    {
        switch (v)
        {
            case DateTime dt: d = dt; return true;
            case DateTimeOffset dto: d = dto.DateTime; return true;
            case string s: return DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out d);
            default: d = default; return false;
        }
    }
}
