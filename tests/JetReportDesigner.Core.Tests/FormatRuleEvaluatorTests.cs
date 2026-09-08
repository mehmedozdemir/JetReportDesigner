using System.Globalization;
using JetReportDesigner.Core.Binding;
using JetReportDesigner.Core.Model;

namespace JetReportDesigner.Core.Tests;

public class FormatRuleEvaluatorTests
{
    static FormatRuleEvaluatorTests()
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
    }

    private static BindingContext Context() => new(
        new Dictionary<string, object?>
        {
            ["customer"] = "Acme Ltd",
            ["total"] = 1234.5,
            ["status"] = "OVERDUE",
            ["notes"] = "",
            ["orderDate"] = "2026-03-09",
        },
        new Dictionary<string, object?>());

    private static FormatRule Rule(string field, ComparisonOp op, string value = "") =>
        new() { Field = field, Op = op, Value = value };

    [Theory]
    [InlineData("total", ComparisonOp.Gt, "1000", true)]
    [InlineData("total", ComparisonOp.Gt, "2000", false)]
    [InlineData("total", ComparisonOp.Le, "1234.5", true)]
    [InlineData("orders.total", ComparisonOp.Ge, "1234.5", true)]
    public void Numeric_comparisons(string field, ComparisonOp op, string value, bool expected) =>
        Assert.Equal(expected, FormatRuleEvaluator.Matches(Rule(field, op, value), Context()));

    [Theory]
    [InlineData(ComparisonOp.Eq, "overdue", true)] // case-insensitive
    [InlineData(ComparisonOp.Ne, "paid", true)]
    [InlineData(ComparisonOp.Contains, "due", true)]
    [InlineData(ComparisonOp.StartsWith, "OVER", true)]
    [InlineData(ComparisonOp.EndsWith, "DUE", true)]
    [InlineData(ComparisonOp.Contains, "paid", false)]
    public void Text_comparisons(ComparisonOp op, string value, bool expected) =>
        Assert.Equal(expected, FormatRuleEvaluator.Matches(Rule("status", op, value), Context()));

    [Fact]
    public void Empty_checks()
    {
        Assert.True(FormatRuleEvaluator.Matches(Rule("notes", ComparisonOp.IsEmpty), Context()));
        Assert.False(FormatRuleEvaluator.Matches(Rule("notes", ComparisonOp.IsNotEmpty), Context()));
        Assert.True(FormatRuleEvaluator.Matches(Rule("missing", ComparisonOp.IsEmpty), Context()));
        Assert.True(FormatRuleEvaluator.Matches(Rule("customer", ComparisonOp.IsNotEmpty), Context()));
    }

    [Fact]
    public void Date_string_comparison()
    {
        Assert.True(FormatRuleEvaluator.Matches(Rule("orderDate", ComparisonOp.Lt, "2026-06-01"), Context()));
        Assert.False(FormatRuleEvaluator.Matches(Rule("orderDate", ComparisonOp.Gt, "2026-06-01"), Context()));
    }

    [Fact]
    public void Apply_layers_every_match_and_reports_hidden()
    {
        var rules = new List<FormatRule>
        {
            new() { Field = "total", Op = ComparisonOp.Gt, Value = "0", Style = new ReportStyle { Background = "#eee" } },
            new() { Field = "status", Op = ComparisonOp.Eq, Value = "OVERDUE", Style = new ReportStyle { Color = "#f00" }, Hidden = true },
            new() { Field = "status", Op = ComparisonOp.Eq, Value = "PAID", Style = new ReportStyle { Color = "#0f0" } },
        };

        var (styles, hidden) = FormatRuleEvaluator.Apply(rules, Context());

        Assert.Equal(2, styles.Count);
        Assert.Equal("#eee", styles[0].Background);
        Assert.Equal("#f00", styles[1].Color);
        Assert.True(hidden);
    }
}
