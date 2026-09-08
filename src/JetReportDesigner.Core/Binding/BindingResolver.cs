using System.Globalization;
using System.Text.RegularExpressions;

namespace JetReportDesigner.Core.Binding;

/// <summary>
/// The row currently in scope while a template is resolved: field values from the
/// active data-source row plus the report's parameter values.
/// </summary>
public sealed class BindingContext(
    IReadOnlyDictionary<string, object?>? row,
    IReadOnlyDictionary<string, object?>? parameters = null)
{
    private static readonly IReadOnlyDictionary<string, object?> Empty =
        new Dictionary<string, object?>();

    public IReadOnlyDictionary<string, object?> Row { get; } = row ?? Empty;

    public IReadOnlyDictionary<string, object?> Parameters { get; } = parameters ?? Empty;
}

/// <summary>
/// Resolves the Phase 1 binding surface: <c>{dataSource.field}</c> and
/// <c>{param:name}</c> placeholders inside a string, plus a single placeholder used
/// as an element's whole value (then an optional .NET format string is applied).
/// Expressions (arithmetic, functions) arrive in Phase 2.
/// </summary>
public static partial class BindingResolver
{
    [GeneratedRegex(@"\{(?<expr>param:[A-Za-z_][A-Za-z0-9_]*|[A-Za-z_][A-Za-z0-9_]*\.[A-Za-z_][A-Za-z0-9_]*)\}")]
    private static partial Regex PlaceholderRegex();

    /// <summary>Resolves every placeholder in <paramref name="template"/> and returns the interpolated string.</summary>
    public static string ResolveText(string? template, BindingContext context)
    {
        if (string.IsNullOrEmpty(template))
        {
            return string.Empty;
        }

        return PlaceholderRegex().Replace(template, match =>
        {
            var value = Lookup(match.Groups["expr"].Value, context);
            return value is null ? string.Empty : Convert.ToString(value, CultureInfo.CurrentCulture) ?? string.Empty;
        });
    }

    /// <summary>
    /// Resolves a value that is expected to be a single binding (optionally with
    /// surrounding text), then formats it. When the value is exactly one placeholder
    /// and a <paramref name="format"/> is given, the raw typed value is formatted;
    /// otherwise the interpolated string is returned.
    /// </summary>
    public static string ResolveValue(string? expression, string? format, BindingContext context)
    {
        if (string.IsNullOrEmpty(expression))
        {
            return string.Empty;
        }

        var single = PlaceholderRegex().Match(expression);
        if (single.Success && single.Value.Length == expression.Length)
        {
            var raw = Lookup(single.Groups["expr"].Value, context);
            return Format(raw, format);
        }

        return ResolveText(expression, context);
    }

    private static object? Lookup(string expr, BindingContext context)
    {
        if (expr.StartsWith("param:", StringComparison.Ordinal))
        {
            var name = expr["param:".Length..];
            return context.Parameters.TryGetValue(name, out var p) ? p : null;
        }

        // dataSource.field — the row is already the active source's row, so the
        // source name is a scope marker; the field name is what we look up.
        var field = expr[(expr.IndexOf('.') + 1)..];
        return context.Row.TryGetValue(field, out var v) ? v : null;
    }

    private static string Format(object? value, string? format)
    {
        if (value is null)
        {
            return string.Empty;
        }

        if (string.IsNullOrEmpty(format))
        {
            return Convert.ToString(value, CultureInfo.CurrentCulture) ?? string.Empty;
        }

        return value is IFormattable formattable
            ? formattable.ToString(format, CultureInfo.CurrentCulture)
            : value.ToString() ?? string.Empty;
    }

    /// <summary>The distinct <c>dataSource.field</c> / <c>param:name</c> tokens referenced by a template. Used for validation.</summary>
    public static IReadOnlyList<string> ReferencedTokens(string? template)
    {
        if (string.IsNullOrEmpty(template))
        {
            return [];
        }

        var tokens = new List<string>();
        foreach (Match m in PlaceholderRegex().Matches(template))
        {
            var expr = m.Groups["expr"].Value;
            if (!tokens.Contains(expr))
            {
                tokens.Add(expr);
            }
        }

        return tokens;
    }
}
