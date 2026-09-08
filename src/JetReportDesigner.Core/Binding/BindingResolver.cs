using System.Globalization;
using System.Text.RegularExpressions;

namespace JetReportDesigner.Core.Binding;

/// <summary>
/// The row currently in scope while a template is resolved: field values from the
/// active data-source row, the report's parameter values, and page-info state
/// (page number / total pages / render time) used by <c>pageNumber()</c> etc.
/// </summary>
public sealed class BindingContext(
    IReadOnlyDictionary<string, object?>? row,
    IReadOnlyDictionary<string, object?>? parameters = null)
{
    private static readonly IReadOnlyDictionary<string, object?> Empty =
        new Dictionary<string, object?>();

    public IReadOnlyDictionary<string, object?> Row { get; } = row ?? Empty;

    public IReadOnlyDictionary<string, object?> Parameters { get; } = parameters ?? Empty;

    public int PageNumber { get; init; } = 1;

    public int TotalPages { get; init; } = 1;

    public DateTime Now { get; init; } = DateTime.Now;

    /// <summary>Culture applied to number/date formatting and case functions.</summary>
    public CultureInfo Culture { get; init; } = CultureInfo.CurrentCulture;

    public BindingContext WithRow(IReadOnlyDictionary<string, object?>? newRow) =>
        new(newRow, Parameters) { PageNumber = PageNumber, TotalPages = TotalPages, Now = Now, Culture = Culture };

    public BindingContext WithPaging(int pageNumber, int totalPages) =>
        new(Row, Parameters) { PageNumber = pageNumber, TotalPages = totalPages, Now = Now, Culture = Culture };
}

/// <summary>
/// Resolves the binding surface: <c>{dataSource.field}</c>, <c>{param:name}</c>, and
/// the page-info functions <c>{pageNumber()}</c>, <c>{totalPages()}</c>, <c>{now()}</c>
/// inside a string; plus a single placeholder used as an element's whole value (then
/// an optional .NET format string is applied). A full expression language is a later
/// slice.
/// </summary>
public static partial class BindingResolver
{
    [GeneratedRegex(
        @"\{(?<expr>param:[A-Za-z_][A-Za-z0-9_]*|pageNumber\(\)|totalPages\(\)|now\(\)|[A-Za-z_][A-Za-z0-9_]*\.[A-Za-z_][A-Za-z0-9_]*)\}")]
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
            return value is null ? string.Empty : Convert.ToString(value, context.Culture) ?? string.Empty;
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

        if (ExpressionEvaluator.IsExpression(expression))
        {
            try
            {
                return Format(ExpressionEvaluator.Evaluate(expression, context), format, context.Culture);
            }
            catch (ExpressionException ex)
            {
                // Degrade gracefully in the cell rather than failing the whole render.
                return $"#ERR: {ex.Message}";
            }
        }

        var single = PlaceholderRegex().Match(expression);
        if (single.Success && single.Value.Length == expression.Length)
        {
            var raw = Lookup(single.Groups["expr"].Value, context);
            return Format(raw, format, context.Culture);
        }

        return ResolveText(expression, context);
    }

    /// <summary>Resolves a bare group expression such as <c>{orders.customer}</c> or <c>orders.customer</c> to its raw value.</summary>
    public static object? ResolveGroupKey(string? expression, BindingContext context)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return null;
        }

        var match = PlaceholderRegex().Match(expression);
        if (match.Success)
        {
            return Lookup(match.Groups["expr"].Value, context);
        }

        // Allow the un-braced "source.field" form for group expressions.
        var dot = expression.IndexOf('.');
        var field = dot >= 0 ? expression[(dot + 1)..] : expression;
        return context.Row.TryGetValue(field.Trim(), out var v) ? v : null;
    }

    private static object? Lookup(string expr, BindingContext context)
    {
        switch (expr)
        {
            case "pageNumber()":
                return context.PageNumber;
            case "totalPages()":
                return context.TotalPages;
            case "now()":
                return context.Now;
        }

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

    /// <summary>Formats a raw value with an optional .NET format string. Null <paramref name="culture"/> uses the current culture.</summary>
    public static string FormatValue(object? value, string? format, CultureInfo? culture = null) =>
        Format(value, format, culture ?? CultureInfo.CurrentCulture);

    private static string Format(object? value, string? format, CultureInfo culture)
    {
        if (value is null)
        {
            return string.Empty;
        }

        if (string.IsNullOrEmpty(format))
        {
            return Convert.ToString(value, culture) ?? string.Empty;
        }

        return value is IFormattable formattable
            ? formattable.ToString(format, culture)
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
            if (!tokens.Contains(expr) && !expr.EndsWith("()", StringComparison.Ordinal))
            {
                tokens.Add(expr);
            }
        }

        return tokens;
    }
}
