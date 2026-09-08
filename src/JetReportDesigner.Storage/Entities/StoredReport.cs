namespace JetReportDesigner.Storage.Entities;

/// <summary>
/// Persistence row for a report. The full <see cref="JetReportDesigner.Core.Model.ReportDefinition"/>
/// lives in <see cref="DefinitionJson"/>; the other columns are denormalised copies
/// for listing and filtering without parsing the JSON.
/// </summary>
public sealed class StoredReport
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Denormalised copy of <c>ReportDefinition.LayoutMode</c> ("banded" | "free").</summary>
    public string LayoutMode { get; set; } = "free";

    /// <summary>The canonical report definition, serialized with <c>ReportJson</c>.</summary>
    public string DefinitionJson { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    /// <summary>Provider-agnostic optimistic-concurrency token, regenerated on every update.</summary>
    public Guid ConcurrencyToken { get; set; }
}
