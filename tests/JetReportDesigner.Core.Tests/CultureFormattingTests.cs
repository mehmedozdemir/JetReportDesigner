using System.Globalization;
using JetReportDesigner.Core.Binding;

namespace JetReportDesigner.Core.Tests;

public class CultureFormattingTests
{
    private static BindingContext Ctx(string? culture) => new(
        new Dictionary<string, object?> { ["amt"] = 1234.5 },
        null)
    {
        Culture = CultureResolver.Resolve(culture),
    };

    [Theory]
    [InlineData("tr-TR", "1.234,50")]
    [InlineData("en-US", "1,234.50")]
    [InlineData("de-DE", "1.234,50")]
    public void ResolveValue_formats_numbers_in_the_report_culture(string culture, string expected) =>
        Assert.Equal(expected, BindingResolver.ResolveValue("{d.amt}", "N2", Ctx(culture)));

    [Fact]
    public void Expression_format_function_uses_the_context_culture()
    {
        Assert.Equal("1.234,50", BindingResolver.ResolveValue("=format(amt, 'N2')", null, Ctx("tr-TR")));
        Assert.Equal("1,234.50", BindingResolver.ResolveValue("=format(amt, 'N2')", null, Ctx("en-US")));
    }

    [Fact]
    public void Unset_culture_falls_back_to_the_current_culture()
    {
        Assert.Same(CultureInfo.CurrentCulture, CultureResolver.Resolve(null));
        Assert.Same(CultureInfo.CurrentCulture, CultureResolver.Resolve("   "));
        Assert.Equal("fr-FR", CultureResolver.Resolve("fr-FR").Name);
    }
}
