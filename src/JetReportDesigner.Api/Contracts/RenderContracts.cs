using JetReportDesigner.Core.Model;

namespace JetReportDesigner.Api.Contracts;

/// <summary>Body for rendering/previewing a saved report.</summary>
/// <param name="Parameters">Report parameter values by name.</param>
/// <param name="Data">
/// Rows to render instead of a data source's configured ones, keyed by data source name — each value
/// an array of objects, e.g. <c>{"data": [{"Adi": "Ahmet"}]}</c>. For this render only.
/// </param>
public sealed record RenderRequest(
    Dictionary<string, object?>? Parameters,
    Dictionary<string, System.Text.Json.JsonElement>? Data = null);

/// <summary>Body for rendering an unsaved report definition.</summary>
public sealed record InlineRenderRequest(
    ReportDefinition Definition,
    Dictionary<string, object?>? Parameters,
    Dictionary<string, System.Text.Json.JsonElement>? Data = null);
