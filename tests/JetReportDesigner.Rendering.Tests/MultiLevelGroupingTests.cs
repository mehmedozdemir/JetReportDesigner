using System.Globalization;
using System.Text.Json;
using JetReportDesigner.Core.Model;
using JetReportDesigner.DataSources;
using JetReportDesigner.DataSources.Json;
using JetReportDesigner.Rendering.Layout;

namespace JetReportDesigner.Rendering.Tests;

public class MultiLevelGroupingTests
{
    static MultiLevelGroupingTests()
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
    }

    // "Acme" deliberately appears as a customer under both North and South, to catch a
    // control-break bug that only compares each level's own key (it would wrongly treat
    // North/Acme and South/Acme as the same running group if they were ever adjacent).
    private static readonly object[] Orders =
    [
        new { region = "North", customer = "Acme", total = 100 },
        new { region = "North", customer = "Acme", total = 50 },
        new { region = "North", customer = "Borg", total = 30 },
        new { region = "South", customer = "Acme", total = 20 },
        new { region = "South", customer = "Cyberdyne", total = 40 },
    ];

    private static ReportDefinition Report() => new()
    {
        Name = "region-customer",
        LayoutMode = LayoutMode.Banded,
        DataSources =
        [
            new DataSourceDefinition
            {
                Name = "orders",
                Kind = DataSourceKind.Json,
                Json = new JsonSourceConfig { InlineData = JsonSerializer.Serialize(Orders), ResultPath = "$" },
            },
        ],
        Bands =
        [
            new Band
            {
                Type = BandType.GroupHeader, GroupLevel = 0, Height = 20,
                Group = new GroupSpec { DataSource = "orders", Expression = "{orders.region}", Sort = "asc" },
                Elements = [Field("region-hdr", "{orders.region}", 0, 0, 200, 18)],
            },
            new Band
            {
                Type = BandType.GroupHeader, GroupLevel = 1, Height = 18,
                Group = new GroupSpec { DataSource = "orders", Expression = "{orders.customer}", Sort = "asc" },
                Elements = [Field("customer-hdr", "{orders.customer}", 20, 0, 200, 16)],
            },
            new Band
            {
                Type = BandType.Detail, Height = 16, DataSource = "orders",
                Elements = [Field("d", "{orders.total}", 40, 0, 100, 14, "n2")],
            },
            new Band
            {
                Type = BandType.GroupFooter, GroupLevel = 1, Height = 18,
                Group = new GroupSpec { DataSource = "orders", Expression = "{orders.customer}", Sort = "asc" },
                Elements = [Sum("customer-sub", "{orders.total}", 40, 0, 100, 16)],
            },
            new Band
            {
                Type = BandType.GroupFooter, GroupLevel = 0, Height = 20,
                Group = new GroupSpec { DataSource = "orders", Expression = "{orders.region}", Sort = "asc" },
                Elements = [Sum("region-sub", "{orders.total}", 20, 0, 100, 18)],
            },
            new Band
            {
                Type = BandType.ReportFooter, Height = 20,
                Elements = [Sum("grand", "{orders.total}", AggregateScope.Report, 0, 0, 100, 18)],
            },
        ],
    };

    private static ReportElement Field(string id, string value, double x, double y, double w, double h, string? format = null) => new()
    {
        Id = id, Type = ElementType.Field, Value = value, Format = format,
        Bounds = new Bounds { X = x, Y = y, Width = w, Height = h },
    };

    private static ReportElement Sum(string id, string value, double x, double y, double w, double h) => new()
    {
        Id = id, Type = ElementType.Field, Value = value, Format = "n2",
        Aggregate = AggregateFunction.Sum, AggregateScope = AggregateScope.Group,
        Bounds = new Bounds { X = x, Y = y, Width = w, Height = h },
    };

    private static ReportElement Sum(string id, string value, AggregateScope scope, double x, double y, double w, double h) => new()
    {
        Id = id, Type = ElementType.Field, Value = value, Format = "n2",
        Aggregate = AggregateFunction.Sum, AggregateScope = scope,
        Bounds = new Bounds { X = x, Y = y, Width = w, Height = h },
    };

    private static ReportData Data(ReportDefinition report) => new(new Dictionary<string, ResolvedDataSet>
    {
        ["orders"] = JsonRows.Parse(report.DataSources[0].Json!.InlineData, "$"),
    });

    [Fact]
    public void Level0_footer_sums_the_whole_region_across_multiple_level1_subgroups()
    {
        var report = Report();
        var doc = new BandedLayoutBuilder().Build(report, Data(report), new Dictionary<string, object?>());
        var texts = doc.Pages.SelectMany(p => p.Primitives).OfType<TextPrimitive>().Select(t => t.Text).ToList();

        // North = Acme(100+50) + Borg(30) = 180; South = Acme(20) + Cyberdyne(40) = 60.
        Assert.Contains((180.0).ToString("n2", CultureInfo.InvariantCulture), texts);
        Assert.Contains((60.0).ToString("n2", CultureInfo.InvariantCulture), texts);
    }

    [Fact]
    public void Level1_footer_sums_only_its_own_customer_not_the_other_regions_same_name_customer()
    {
        var report = Report();
        var doc = new BandedLayoutBuilder().Build(report, Data(report), new Dictionary<string, object?>());
        var texts = doc.Pages.SelectMany(p => p.Primitives).OfType<TextPrimitive>().Select(t => t.Text).ToList();

        // North/Acme = 150, South/Acme = 20 — must NOT be merged into one 170 subtotal.
        Assert.Contains((150.0).ToString("n2", CultureInfo.InvariantCulture), texts);
        Assert.Contains((20.0).ToString("n2", CultureInfo.InvariantCulture), texts);
        Assert.DoesNotContain((170.0).ToString("n2", CultureInfo.InvariantCulture), texts);
    }

    [Fact]
    public void Grand_total_sums_every_row()
    {
        var report = Report();
        var doc = new BandedLayoutBuilder().Build(report, Data(report), new Dictionary<string, object?>());
        var texts = doc.Pages.SelectMany(p => p.Primitives).OfType<TextPrimitive>().Select(t => t.Text).ToList();

        Assert.Contains((240.0).ToString("n2", CultureInfo.InvariantCulture), texts);
    }

    [Fact]
    public void Bands_nest_outer_header_then_inner_header_then_detail_then_inner_footer_then_outer_footer()
    {
        var report = Report();
        var doc = new BandedLayoutBuilder().Build(report, Data(report), new Dictionary<string, object?>());
        var order = doc.Pages.SelectMany(p => p.Primitives)
            .OfType<TextPrimitive>()
            .Select(t => t.Text)
            .Where(t => t is "North" or "South" or "Acme" or "Borg" or "Cyberdyne"
                || t == (100.0 + 50.0).ToString("n2", CultureInfo.InvariantCulture) // North/Acme subtotal
                || t == (180.0).ToString("n2", CultureInfo.InvariantCulture)) // North region subtotal
            .ToList();

        // North (outer header) -> Acme (inner header) -> Acme's 150.00 (inner footer) ->
        // Borg (inner header, no footer text asserted) -> North's 180.00 (outer footer).
        var northIdx = order.IndexOf("North");
        var acmeIdx = order.IndexOf("Acme");
        var acmeSubIdx = order.IndexOf((150.0).ToString("n2", CultureInfo.InvariantCulture));
        var regionSubIdx = order.IndexOf((180.0).ToString("n2", CultureInfo.InvariantCulture));

        Assert.True(northIdx >= 0 && acmeIdx > northIdx && acmeSubIdx > acmeIdx && regionSubIdx > acmeSubIdx,
            $"unexpected nesting order: {string.Join(", ", order)}");
    }
}
