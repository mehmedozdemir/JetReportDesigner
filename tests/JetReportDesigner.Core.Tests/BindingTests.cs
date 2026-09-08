using System.Globalization;
using System.Text.Json;
using JetReportDesigner.Core.Binding;
using JetReportDesigner.Core.Model;

namespace JetReportDesigner.Core.Tests;

public class BindingTests
{
    static BindingTests()
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
    }

    private static BindingContext Context() => new(
        new Dictionary<string, object?>
        {
            ["customer"] = "Acme Ltd",
            ["total"] = 1234.5,
            ["orderDate"] = new DateTime(2026, 3, 9),
        },
        new Dictionary<string, object?> { ["title"] = "Q1 Report" });

    [Fact]
    public void ResolveText_Interpolates_Bindings_And_Params()
    {
        var result = BindingResolver.ResolveText("{param:title}: {orders.customer}", Context());
        Assert.Equal("Q1 Report: Acme Ltd", result);
    }

    [Fact]
    public void ResolveText_Missing_Field_Becomes_Empty()
    {
        Assert.Equal("x=", BindingResolver.ResolveText("x={orders.missing}", Context()));
    }

    [Fact]
    public void ResolveValue_Applies_Format_To_Single_Binding()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        try
        {
            Assert.Equal("1,234.50", BindingResolver.ResolveValue("{orders.total}", "n2", Context()));
            Assert.Equal("09.03.2026", BindingResolver.ResolveValue("{orders.orderDate}", "dd.MM.yyyy", Context()));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void ResolveValue_Without_Format_Returns_Interpolated_Text()
    {
        Assert.Equal("Total: 1234.5", BindingResolver.ResolveValue("Total: {orders.total}", null, Context()));
    }

    [Fact]
    public void ReferencedTokens_Lists_Distinct_Bindings()
    {
        var tokens = BindingResolver.ReferencedTokens("{a.b} {c:x} {param:p} {a.b}");
        Assert.Equal(["a.b", "param:p"], tokens);
    }

    [Fact]
    public void ParameterValues_Fills_Defaults_And_Coerces()
    {
        var report = new ReportDefinition
        {
            Name = "r",
            LayoutMode = LayoutMode.Free,
            Parameters =
            [
                new ReportParameter { Name = "from", Type = ParameterType.Date, DefaultValue = "2026-01-01" },
                new ReportParameter { Name = "limit", Type = ParameterType.Number, DefaultValue = 10 },
                new ReportParameter { Name = "active", Type = ParameterType.Boolean },
            ],
        };

        var supplied = new Dictionary<string, object?>
        {
            ["limit"] = JsonSerializer.Deserialize<JsonElement>("42"),
            ["active"] = JsonSerializer.Deserialize<JsonElement>("true"),
        };

        var values = ParameterValues.Resolve(report, supplied);

        Assert.Equal(new DateTime(2026, 1, 1), values["from"]);
        Assert.Equal(42d, values["limit"]);
        Assert.Equal(true, values["active"]);
    }
}
