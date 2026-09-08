using System.Globalization;
using System.Text.Json;
using JetReportDesigner.Core.Model;

namespace JetReportDesigner.DataSources.Json;

/// <summary>
/// Turns a JSON document into flat rows: navigates a simple dotted <c>resultPath</c>
/// (<c>$</c>, <c>$.data</c>, <c>$.data.items</c>) to an array of objects and maps each
/// object's scalar properties to CLR values. Nested objects/arrays are kept as their
/// raw JSON string. Field types are inferred from the first non-null value seen.
/// </summary>
public static class JsonRows
{
    public static ResolvedDataSet Parse(string json, string resultPath)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return ResolvedDataSet.Empty;
        }

        using var doc = JsonDocument.Parse(json);
        var node = Navigate(doc.RootElement, resultPath);

        if (node.ValueKind != JsonValueKind.Array)
        {
            // A single object is treated as a one-row set.
            node = node.ValueKind == JsonValueKind.Object
                ? WrapAsArray(node)
                : default;
        }

        if (node.ValueKind != JsonValueKind.Array)
        {
            return ResolvedDataSet.Empty;
        }

        var rows = new List<IReadOnlyDictionary<string, object?>>();
        var fieldOrder = new List<string>();
        var fieldTypes = new Dictionary<string, FieldType>(StringComparer.Ordinal);

        foreach (var item in node.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var row = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var prop in item.EnumerateObject())
            {
                var value = ToClr(prop.Value);
                row[prop.Name] = value;

                if (!fieldTypes.ContainsKey(prop.Name))
                {
                    fieldOrder.Add(prop.Name);
                    fieldTypes[prop.Name] = FieldType.String;
                }

                if (value is not null && fieldTypes[prop.Name] == FieldType.String)
                {
                    fieldTypes[prop.Name] = InferType(prop.Value, value);
                }
            }

            rows.Add(row);
        }

        var fields = fieldOrder.Select(name => new DataField { Name = name, Type = fieldTypes[name] }).ToList();
        return new ResolvedDataSet(rows, fields);
    }

    private static JsonElement Navigate(JsonElement root, string? resultPath)
    {
        if (string.IsNullOrWhiteSpace(resultPath) || resultPath is "$")
        {
            return root;
        }

        var path = resultPath.StartsWith("$.", StringComparison.Ordinal)
            ? resultPath[2..]
            : resultPath.StartsWith('$') ? resultPath[1..] : resultPath;

        var current = root;
        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            if (current.ValueKind == JsonValueKind.Object && current.TryGetProperty(segment, out var next))
            {
                current = next;
            }
            else
            {
                return default;
            }
        }

        return current;
    }

    private static JsonElement WrapAsArray(JsonElement obj)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartArray();
            obj.WriteTo(writer);
            writer.WriteEndArray();
        }

        var wrapped = JsonDocument.Parse(buffer.ToArray());
        return wrapped.RootElement.Clone();
    }

    private static object? ToClr(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.String => e.GetString(),
        JsonValueKind.Number => e.TryGetInt64(out var l) ? l : e.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null or JsonValueKind.Undefined => null,
        _ => e.GetRawText(),
    };

    private static FieldType InferType(JsonElement source, object value) => source.ValueKind switch
    {
        JsonValueKind.Number => FieldType.Number,
        JsonValueKind.True or JsonValueKind.False => FieldType.Boolean,
        JsonValueKind.String when LooksLikeDate((string)value, out var hasTime) =>
            hasTime ? FieldType.DateTime : FieldType.Date,
        _ => FieldType.String,
    };

    private static bool LooksLikeDate(string s, out bool hasTime)
    {
        hasTime = s.Contains('T') || s.Contains(' ') && s.Length > 10;
        return DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _)
            && s.Length >= 8
            && (s.Contains('-') || s.Contains('/') || s.Contains(':'));
    }
}
