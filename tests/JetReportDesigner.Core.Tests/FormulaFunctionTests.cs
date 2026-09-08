using System.Globalization;
using JetReportDesigner.Core.Binding;

namespace JetReportDesigner.Core.Tests;

public class FormulaFunctionTests
{
    static FormulaFunctionTests()
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
    }

    private static readonly IReadOnlyDictionary<string, object?>[] Orders =
    [
        new Dictionary<string, object?> { ["total"] = 100.0, ["cust"] = "acme" },
        new Dictionary<string, object?> { ["total"] = 50.0, ["cust"] = "acme" },
        new Dictionary<string, object?> { ["total"] = 200.0, ["cust"] = "globex" },
    ];

    private static BindingContext Ctx() => new(
        Orders[0],
        new Dictionary<string, object?>())
    {
        AggregateRows = Orders,
        RowNumber = 1,
        TotalRows = Orders.Length,
        Now = new DateTime(2026, 3, 9, 14, 7, 0),
        Culture = CultureInfo.InvariantCulture,
    };

    private static string Eval(string expr) => BindingResolver.ResolveValue(expr, null, Ctx());

    [Theory]
    [InlineData("=abs(-3)", "3")]
    [InlineData("=round(3.14159, 2)", "3.14")]
    [InlineData("=floor(3.9)", "3")]
    [InlineData("=ceiling(3.1)", "4")]
    [InlineData("=pow(2, 10)", "1024")]
    [InlineData("=mod(10, 3)", "1")]
    [InlineData("=sign(-5)", "-1")]
    public void Math_functions(string expr, string expected) => Assert.Equal(expected, Eval(expr));

    [Theory]
    [InlineData("=upper('abc')", "ABC")]
    [InlineData("=lower('ABC')", "abc")]
    [InlineData("=trim('  x  ')", "x")]
    [InlineData("=left('abcdef', 3)", "abc")]
    [InlineData("=right('abcdef', 2)", "ef")]
    [InlineData("=substring('abcdef', 1, 3)", "bcd")]
    [InlineData("=replace('a-b-c', '-', '_')", "a_b_c")]
    [InlineData("=contains('Hello World', 'world')", "True")]
    public void Text_functions(string expr, string expected) => Assert.Equal(expected, Eval(expr));

    [Theory]
    [InlineData("=year(now())", "2026")]
    [InlineData("=month(now())", "3")]
    [InlineData("=day(now())", "9")]
    public void Date_functions(string expr, string expected) => Assert.Equal(expected, Eval(expr));

    [Theory]
    [InlineData("=rowNumber()", "1")]
    [InlineData("=totalRows()", "3")]
    [InlineData("=pageNumber()", "1")]
    public void Report_functions(string expr, string expected) => Assert.Equal(expected, Eval(expr));

    [Theory]
    [InlineData("=sum(orders.total)", "350")]
    [InlineData("=avg(orders.total)", "116.66666666666667")]
    [InlineData("=count(orders.total)", "3")]
    [InlineData("=min(orders.total)", "50")]
    [InlineData("=max(orders.total)", "200")]
    [InlineData("=first(orders.cust)", "acme")]
    [InlineData("=last(orders.cust)", "globex")]
    [InlineData("=sum(orders.total * 2)", "700")]
    public void Aggregates_iterate_the_scope_rows(string expr, string expected) =>
        Assert.Equal(expected, Eval(expr));

    [Fact]
    public void Bare_function_call_is_treated_as_an_expression()
    {
        Assert.True(ExpressionEvaluator.IsExpression("now()"));
        Assert.True(ExpressionEvaluator.IsExpression("sum(orders.total)"));
        Assert.False(ExpressionEvaluator.IsExpression("Summary (2026)"));
        Assert.False(ExpressionEvaluator.IsExpression("just text"));
        Assert.Equal("350", BindingResolver.ResolveValue("sum(orders.total)", null, Ctx()));
    }

    [Fact]
    public void Scalar_min_max_with_two_args()
    {
        Assert.Equal("3", Eval("=min(3, 7)"));
        Assert.Equal("7", Eval("=max(3, 7)"));
    }
}
