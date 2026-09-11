using System.Globalization;
using JetReportDesigner.Core.Binding;

namespace JetReportDesigner.Core.Tests;

public class ExpressionEvaluatorTests
{
    static ExpressionEvaluatorTests()
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
    }

    private static BindingContext Ctx() => new(
        new Dictionary<string, object?> { ["customer"] = "Acme", ["total"] = 1500.0, ["qty"] = 3.0 },
        new Dictionary<string, object?> { ["region"] = "EU" })
    {
        PageNumber = 2,
        TotalPages = 5,
    };

    [Theory]
    [InlineData("=1 + 2 * 3", "7")]
    [InlineData("=(1 + 2) * 3", "9")]
    [InlineData("=10 % 3", "1")]
    [InlineData("=total / qty", "500")]
    [InlineData("='Hi, ' + customer", "Hi, Acme")]
    [InlineData("=upper(customer)", "ACME")]
    [InlineData("=if(total > 1000, 'VIP', 'Standard')", "VIP")]
    [InlineData("=if(total > 2000, 'VIP', 'Standard')", "Standard")]
    [InlineData("=coalesce(null, missing, 'fallback')", "fallback")]
    [InlineData("=pageNumber() + ' / ' + totalPages()", "2 / 5")]
    [InlineData("=param.region", "EU")]
    [InlineData("=customer = 'Acme' and total >= 1500", "True")]
    [InlineData("=not (qty = 4)", "True")]
    public void Evaluates(string expression, string expected)
    {
        Assert.Equal(expected, BindingResolver.ResolveValue(expression, null, Ctx()));
    }

    [Theory]
    [InlineData("=startswith(customer, 'Ac')", "True")]
    [InlineData("=endswith(customer, 'me')", "True")]
    [InlineData("=indexof(customer, 'me')", "2")]
    [InlineData("=padleft('7', 3, '0')", "007")]
    [InlineData("=padright('x', 3, '-')", "x--")]
    [InlineData("=concat('a', 1, 'b')", "a1b")]
    [InlineData("=ltrim('  hi')", "hi")]
    [InlineData("=rtrim('hi  ')", "hi")]
    [InlineData("=log10(100)", "2")]
    [InlineData("=exp(0)", "1")]
    [InlineData("=switch(customer, 'Acme', 'A', 'Other', 'B', 'C')", "A")]
    [InlineData("=switch(customer, 'Nope', 'A', 'C')", "C")]
    [InlineData("=isnull(missing)", "True")]
    [InlineData("=isnull(customer)", "False")]
    [InlineData("=nullif(1, 1)", "")]
    [InlineData("=nullif(1, 2)", "1")]
    [InlineData("=tonumber('42') + 1", "43")]
    [InlineData("=tostring(42)", "42")]
    [InlineData("=toboolean('false')", "False")]
    public void Evaluates_Advanced_Functions(string expression, string expected)
    {
        Assert.Equal(expected, BindingResolver.ResolveValue(expression, null, Ctx()));
    }

    [Fact]
    public void Datediff_And_Date_Math()
    {
        var ctx = Ctx();
        Assert.Equal(31d, ExpressionEvaluator.Evaluate("=datediff('days', '2026-01-01', '2026-02-01')", ctx));
        Assert.Equal(new DateTime(2026, 4, 1), ExpressionEvaluator.Evaluate("=addmonths('2026-01-01', 3)", ctx));
        Assert.Equal(new DateTime(2027, 1, 1), ExpressionEvaluator.Evaluate("=addyears('2026-01-01', 1)", ctx));
        Assert.Equal(4d, ExpressionEvaluator.Evaluate("=dayofweek('2026-01-01')", ctx)); // Thursday
    }

    [Fact]
    public void Applies_Format_To_Expression_Result()
    {
        Assert.Equal("1,500.00", BindingResolver.ResolveValue("=total", "n2", Ctx()));
    }

    [Fact]
    public void Non_Expression_Bindings_Still_Work()
    {
        Assert.Equal("Acme", BindingResolver.ResolveValue("{orders.customer}", null, Ctx()));
    }

    [Fact]
    public void Bad_Expression_Throws_From_Evaluator()
    {
        Assert.Throws<ExpressionException>(() => ExpressionEvaluator.Evaluate("=1 +", Ctx()));
        Assert.Throws<ExpressionException>(() => ExpressionEvaluator.Evaluate("=bogus(1)", Ctx()));
    }

    [Fact]
    public void Bad_Expression_Degrades_To_Error_Text_In_A_Cell()
    {
        var result = BindingResolver.ResolveValue("=upper(customer) + | + 1", null, Ctx());
        Assert.StartsWith("#ERR:", result);
    }
}
