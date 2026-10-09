using System.Globalization;
using System.Text.Json;

namespace JetReportDesigner.Rendering.Layout;

/// <summary>
/// Reads a sparkline column's raw cell value as a series of numbers. A JSON data source's
/// array field comes through as its raw JSON text (see <c>JsonRows.ToClr</c>), so a value
/// like <c>[4,7,2,9]</c> is the expected shape; a plain comma/semicolon-separated list is
/// accepted too, for a field authored by hand or produced by SQL/REST as text.
/// </summary>
public static class SparklineValues
{
    public static IReadOnlyList<double> Parse(object? raw)
    {
        var text = raw switch
        {
            null => null,
            string s => s,
            _ => raw.ToString(),
        };

        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var trimmed = text.Trim();
        if (trimmed.StartsWith('[') && TryParseJsonArray(trimmed, out var fromJson))
        {
            return fromJson;
        }

        var parts = trimmed.Trim('[', ']').Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var values = new List<double>(parts.Length);
        foreach (var part in parts)
        {
            if (double.TryParse(part, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
            {
                values.Add(d);
            }
        }

        return values;
    }

    private static bool TryParseJsonArray(string json, out IReadOnlyList<double> values)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                values = [];
                return false;
            }

            var list = new List<double>();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                if (el.ValueKind == JsonValueKind.Number && el.TryGetDouble(out var d))
                {
                    list.Add(d);
                }
            }

            values = list;
            return true;
        }
        catch (JsonException)
        {
            values = [];
            return false;
        }
    }
}
