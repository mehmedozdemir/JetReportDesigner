using System.Globalization;
using System.Text.RegularExpressions;

namespace JetReportDesigner.DataSources;

/// <summary>Replaces <c>{param:name}</c> tokens in connector configuration (REST URLs, headers, SQL parameter values).</summary>
public static partial class ParameterInterpolation
{
    [GeneratedRegex(@"\{param:(?<name>[A-Za-z_][A-Za-z0-9_]*)\}")]
    private static partial Regex TokenRegex();

    public static string Apply(string? template, IReadOnlyDictionary<string, object?> parameters)
    {
        if (string.IsNullOrEmpty(template))
        {
            return string.Empty;
        }

        return TokenRegex().Replace(template, match =>
        {
            var name = match.Groups["name"].Value;
            return parameters.TryGetValue(name, out var value) && value is not null
                ? Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
                : string.Empty;
        });
    }

    public static bool ReferencesParameters(string? template) =>
        !string.IsNullOrEmpty(template) && TokenRegex().IsMatch(template);
}
