namespace JetReportDesigner.Core.Schema;

/// <summary>Provides the JSON Schema (draft 2020-12) for <see cref="Model.ReportDefinition"/>, served at <c>/api/meta/schema</c>.</summary>
public static class ReportDefinitionSchema
{
    private const string ResourceName = "JetReportDesigner.Core.Schema.report-definition.schema.json";

    private static readonly Lazy<string> _json = new(Load);

    /// <summary>The raw schema document as a JSON string.</summary>
    public static string Json => _json.Value;

    private static string Load()
    {
        var assembly = typeof(ReportDefinitionSchema).Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded schema resource '{ResourceName}' was not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
