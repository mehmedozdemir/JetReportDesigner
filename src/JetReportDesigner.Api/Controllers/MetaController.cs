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
                return new SampleResponse(definition.Name, definition);
            })
            .ToList();

        return Ok(samples);
    }

    public sealed record SampleResponse(string Name, ReportDefinition Definition);
}
