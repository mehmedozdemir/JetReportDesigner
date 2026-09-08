using System.Globalization;
using System.Text.Json;
using JetReportDesigner.Core.Model;

namespace JetReportDesigner.Core.Binding;

/// <summary>Merges caller-supplied parameter values with the report's declared defaults and coerces them to the declared type.</summary>
public static class ParameterValues
{
    public static IReadOnlyDictionary<string, object?> Resolve(
        ReportDefinition report,
        IReadOnlyDictionary<string, object?>? supplied)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var parameter in report.Parameters)
        {
            var hasSupplied = supplied is not null && supplied.TryGetValue(parameter.Name, out var raw);
            var value = hasSupplied ? supplied![parameter.Name] : parameter.DefaultValue;
            result[parameter.Name] = Coerce(value, parameter.Type);
        }

        return result;
    }

    private static object? Coerce(object? value, ParameterType type)
    {
        value = Unwrap(value);
        if (value is null)
        {
            return null;
        }

        var text = value as string ?? Convert.ToString(value, CultureInfo.InvariantCulture);
        if (text is null)
        {
            return value;
        }

        return type switch
        {
            ParameterType.Number => double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : value,
            ParameterType.Boolean => bool.TryParse(text, out var b) ? b : value,
            ParameterType.Date or ParameterType.DateTime =>
                DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt) ? dt : value,
            _ => text,
        };
    }

    private static object? Unwrap(object? value) => value switch
    {
        JsonElement { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined } => null,
        JsonElement { ValueKind: JsonValueKind.String } e => e.GetString(),
        JsonElement { ValueKind: JsonValueKind.Number } e => e.TryGetInt64(out var l) ? l : e.GetDouble(),
        JsonElement { ValueKind: JsonValueKind.True } => true,
        JsonElement { ValueKind: JsonValueKind.False } => false,
        JsonElement e => e.GetRawText(),
        _ => value,
    };
}
