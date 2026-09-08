using JetReportDesigner.Core.Model;

namespace JetReportDesigner.Api.Contracts;

/// <summary>Body for rendering/previewing a saved report.</summary>
public sealed record RenderRequest(Dictionary<string, object?>? Parameters);

/// <summary>Body for rendering an unsaved report definition.</summary>
public sealed record InlineRenderRequest(ReportDefinition Definition, Dictionary<string, object?>? Parameters);
