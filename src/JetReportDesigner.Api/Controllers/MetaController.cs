using System.Reflection;
using System.Text.Json;
using JetReportDesigner.Core.Model;
using JetReportDesigner.Core.Schema;
using JetReportDesigner.Core.Serialization;
using Microsoft.AspNetCore.Mvc;

namespace JetReportDesigner.Api.Controllers;

[ApiController]
[Route("api/meta")]
public sealed class MetaController : ControllerBase
{
    /// <summary>The JSON Schema (draft 2020-12) describing a report definition.</summary>
    [HttpGet("schema")]
    [Produces("application/schema+json")]
    public ContentResult Schema() => Content(ReportDefinitionSchema.Json, "application/schema+json");

    [HttpGet("health")]
    public IActionResult Health() => Ok(new { status = "ok" });

    /// <summary>Gallery grouping for a built-in sample, keyed by its resource file name
    /// (e.g. "invoice" for "invoice.sample.json"). Falls back to "Other" when unlisted.</summary>
    private static readonly Dictionary<string, string> SampleCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        ["invoice"] = "Invoices",
        ["orders-by-customer"] = "Sales",
        ["credit-card"] = "Cards & IDs",
        ["personnel-card"] = "Cards & IDs",
        ["transit-card"] = "Cards & IDs",
    };

    /// <summary>Built-in sample reports the designer can create from.</summary>
    [HttpGet("samples")]
    public ActionResult<IReadOnlyList<SampleResponse>> Samples()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var samples = assembly.GetManifestResourceNames()
            .Where(n => n.EndsWith(".sample.json", StringComparison.Ordinal))
            .OrderBy(n => n, StringComparer.Ordinal)
            .Select(name =>
            {
                using var stream = assembly.GetManifestResourceStream(name)!;
                using var reader = new StreamReader(stream);
                var definition = JsonSerializer.Deserialize<ReportDefinition>(reader.ReadToEnd(), ReportJson.Options)!;
                var baseName = name[..^".sample.json".Length].Split('.')[^1];
                var category = SampleCategories.GetValueOrDefault(baseName, "Other");
                return new SampleResponse(definition.Name, category, definition);
            })
            .ToList();

        return Ok(samples);
    }

    public sealed record SampleResponse(string Name, string Category, ReportDefinition Definition);
}
