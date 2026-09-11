using JetReportDesigner.Core.Model;
using JetReportDesigner.Rendering;
using JetReportDesigner.Storage.Repositories;

namespace JetReportDesigner.Api.Infrastructure;

/// <summary>Looks up a subreport element's referenced report among the user's saved reports.</summary>
public sealed class SubreportResolver(IReportRepository reports) : ISubreportResolver
{
    public async Task<ReportDefinition?> ResolveAsync(string reportId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(reportId, out var id))
        {
            return null;
        }

        var record = await reports.GetAsync(id, cancellationToken);
        return record?.Definition;
    }
}
