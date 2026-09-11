using JetReportDesigner.Core.Model;
using JetReportDesigner.DataSources;
using JetReportDesigner.DataSources.Json;
using JetReportDesigner.Rendering;
using JetReportDesigner.Rendering.Engines;

namespace JetReportDesigner.Rendering.Tests;

public class SubreportTests
{
    private sealed class StubResolver(Dictionary<string, ReportDefinition> reports) : ISubreportResolver
    {
        public Task<ReportDefinition?> ResolveAsync(string reportId, CancellationToken cancellationToken) =>
            Task.FromResult(reports.TryGetValue(reportId, out var r) ? r : null);
    }

    private static ReportElement SubreportElement(string reportId, Dictionary<string, string>? parameters = null) => new()
    {
        Id = "sub",
        Type = ElementType.Subreport,
        Bounds = new Bounds { X = 10, Y = 10, Width = 200, Height = 100 },
        Subreport = new SubreportSpec { ReportId = reportId, Parameters = parameters ?? [] },
    };

    private static ReportDefinition HostReport(ReportElement element, Guid? id = null) => new()
    {
        Id = id ?? Guid.Empty,
        Name = "host",
        LayoutMode = LayoutMode.Free,
        Body = new ReportBody { Elements = [element] },
    };

    private static ReportRenderService Service(ISubreportResolver resolver) =>
        new(new ReportDataResolver([new JsonDataSourceReader()]), new MigraDocPdfRenderer(), null, resolver);

    [Fact]
    public async Task Unresolvable_reference_becomes_an_error_box_not_a_failure()
    {
        var host = HostReport(SubreportElement(Guid.NewGuid().ToString()));
        var service = Service(new StubResolver([]));

        var result = await service.RenderAsync(host, null, RenderFormat.Html, CancellationToken.None);

        Assert.Contains("Subreport not found", System.Text.Encoding.UTF8.GetString(result.Content));
    }

    [Fact]
    public async Task Self_reference_is_reported_as_a_cycle_not_infinite_recursion()
    {
        var hostId = Guid.NewGuid();
        var host = HostReport(SubreportElement(hostId.ToString()), hostId);
        var service = Service(new StubResolver(new Dictionary<string, ReportDefinition> { [hostId.ToString()] = host }));

        var result = await service.RenderAsync(host, null, RenderFormat.Html, CancellationToken.None);

        Assert.Contains("Circular subreport reference", System.Text.Encoding.UTF8.GetString(result.Content));
    }

    [Fact]
    public async Task Child_content_is_spliced_in_with_its_parameter_bound_from_the_parent()
    {
        var childId = Guid.NewGuid();
        var child = new ReportDefinition
        {
            Id = childId,
            Name = "child",
            LayoutMode = LayoutMode.Free,
            Parameters = [new ReportParameter { Name = "who", Type = ParameterType.String }],
            Body = new ReportBody
            {
                Elements =
                [
                    new ReportElement
                    {
                        Id = "greet", Type = ElementType.Label, Text = "Hello {param:who}",
                        Bounds = new Bounds { X = 0, Y = 0, Width = 100, Height = 20 },
                    },
                ],
            },
        };

        var host = HostReport(SubreportElement(childId.ToString(), new Dictionary<string, string> { ["who"] = "World" }));
        var service = Service(new StubResolver(new Dictionary<string, ReportDefinition> { [childId.ToString()] = child }));

        var result = await service.RenderAsync(host, null, RenderFormat.Html, CancellationToken.None);

        Assert.Contains("Hello World", System.Text.Encoding.UTF8.GetString(result.Content));
    }
}
