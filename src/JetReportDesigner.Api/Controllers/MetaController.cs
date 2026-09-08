using JetReportDesigner.Core.Schema;
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
}
