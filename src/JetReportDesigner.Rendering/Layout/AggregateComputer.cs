using System.Globalization;
using JetReportDesigner.Core.Model;

namespace JetReportDesigner.Rendering.Layout;

using Row = IReadOnlyDictionary<string, object?>;

/// <summary>Computes an aggregate over a set of rows for a footer band element.</summary>
public static class AggregateComputer
{
    public static object? Compute(AggregateFunction function, string? binding, IReadOnlyList<Row> rows)
    {
        if (function == AggregateFunction.Count)
        {
            return rows.Count;
        }

        var field = FieldName(binding);
        if (field is null)
        {
            return null;
        }

        var values = rows
            .Select(r => r.TryGetValue(field, out var v) ? v : null)
            .Where(v => v is not null)
            .ToList();

        if (values.Count == 0)
        {
            return function is AggregateFunction.Sum or AggregateFunction.Average ? 0d : null;
        }

        return function switch
        {
            AggregateFunction.Sum => values.Sum(ToDouble),
            AggregateFunction.Average => values.Average(ToDouble),
            AggregateFunction.Min => values.Select(ToDouble).Min(),
            AggregateFunction.Max => values.Select(ToDouble).Max(),
            AggregateFunction.First => values[0],
            AggregateFunction.Last => values[^1],
            _ => null,
        };
    }

    private static string? FieldName(string? binding)
    {
        if (string.IsNullOrWhiteSpace(binding))
        {
            return null;
        }

        var trimmed = binding.Trim().Trim('{', '}');
        var dot = trimmed.IndexOf('.');
        return dot >= 0 ? trimmed[(dot + 1)..] : trimmed;
    }

    private static double ToDouble(object? value) => value switch
    {
        null => 0d,
        double d => d,
        long l => l,
        int i => i,
        decimal m => (double)m,
        bool b => b ? 1d : 0d,
        IFormattable f => double.TryParse(f.ToString(null, CultureInfo.InvariantCulture),
            NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0d,
        _ => double.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var p) ? p : 0d,
    };
}
