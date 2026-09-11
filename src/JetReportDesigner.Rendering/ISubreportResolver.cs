using JetReportDesigner.Core.Model;

namespace JetReportDesigner.Rendering;

/// <summary>
/// Looks up a saved report by id for a <c>subreport</c> element. Implemented in the
/// host (over its report store) so the rendering project stays free of storage
/// concerns; returns null when the id does not resolve to a report.
/// </summary>
public interface ISubreportResolver
{
    Task<ReportDefinition?> ResolveAsync(string reportId, CancellationToken cancellationToken);
}

/// <summary>Resolves nothing — the default when no host resolver is supplied (e.g. in tests).</summary>
public sealed class NullSubreportResolver : ISubreportResolver
{
    public static readonly NullSubreportResolver Instance = new();

    public Task<ReportDefinition?> ResolveAsync(string reportId, CancellationToken cancellationToken) =>
        Task.FromResult<ReportDefinition?>(null);
}
