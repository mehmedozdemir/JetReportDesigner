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

        if (!options.Converters.Any(c => c is UtcDateTimeConverter))
        {
            // Every DateTime in this codebase is a *Utc field by convention, but EF Core
            // hands values back with DateTimeKind.Unspecified (SQL Server/PostgreSql/Oracle
            // don't round-trip Kind), so the default converter serializes those without a
            // "Z" — while a freshly-constructed DateTime.UtcNow elsewhere in the same
            // response DOES get one. Clients then parse the un-marked ones as local time.
            // Forcing Kind=Utc here makes every DateTime on the wire unambiguous.
            options.Converters.Add(new UtcDateTimeConverter());
        }

        return options;
    }
}

/// <summary>Serializes/deserializes DateTime as UTC unconditionally — see the comment where
/// this is registered in <see cref="ReportJson.Apply"/>.</summary>
internal sealed class UtcDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        DateTime.SpecifyKind(reader.GetDateTime(), DateTimeKind.Utc);

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) =>
        writer.WriteStringValue(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
