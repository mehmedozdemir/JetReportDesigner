using System.Text.Json;
using System.Text.Json.Serialization;
using JetReportDesigner.Core.Model;

namespace JetReportDesigner.Core.Serialization;

/// <summary>
/// The single canonical way to (de)serialize a <see cref="ReportDefinition"/>. The
/// same options are used by the API, the storage layer and the renderer so a report
/// round-trips byte-for-byte through persistence.
/// </summary>
public static class ReportJson
{
    public static JsonSerializerOptions Options { get; } = Apply(new JsonSerializerOptions());

    public static string Serialize(ReportDefinition definition) =>
        JsonSerializer.Serialize(definition, Options);

    public static ReportDefinition Deserialize(string json) =>
        JsonSerializer.Deserialize<ReportDefinition>(json, Options)
        ?? throw new JsonException("Report definition deserialized to null.");

    /// <summary>
    /// Applies the canonical report JSON conventions to an existing options object.
    /// Used both for <see cref="Options"/> and to configure the API's MVC JSON
    /// options, so the wire format is defined in exactly one place.
    /// </summary>
    public static JsonSerializerOptions Apply(JsonSerializerOptions options)
    {
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.PropertyNameCaseInsensitive = true;
        options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.WriteIndented = false;
        if (!options.Converters.Any(c => c is JsonStringEnumConverter))
        {
            options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        }

        return options;
    }
}
