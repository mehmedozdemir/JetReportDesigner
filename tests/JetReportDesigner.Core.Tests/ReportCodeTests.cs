using JetReportDesigner.Core.Model;

namespace JetReportDesigner.Core.Tests;

public class ReportCodeTests
{
    [Theory]
    [InlineData("Barkod Rapor - Claude", "barkod-rapor-claude")]
    [InlineData("Çalışan İşlem Şöförü Ğüzel", "calisan-islem-soforu-guzel")]
    [InlineData("ÇĞİÖŞÜ çğıöşü", "cgiosu-cgiosu")]
    [InlineData("  Aylık   Özet / 2026  ", "aylik-ozet-2026")]
    [InlineData("Sample — Invoice", "sample-invoice")]
    [InlineData("###", "report")]
    [InlineData("", "report")]
    public void FromName_gives_plain_ascii_without_spaces(string name, string expected)
    {
        var code = ReportCode.FromName(name);

        Assert.Equal(expected, code);
        Assert.True(ReportCode.IsValid(code));
    }

    [Fact]
    public void FromName_is_capped_at_the_maximum_length()
    {
        var code = ReportCode.FromName(new string('a', 200));

        Assert.Equal(ReportCode.MaxLength, code.Length);
    }

    [Fact]
    public void MakeUnique_appends_the_first_free_number()
    {
        var taken = new HashSet<string> { "rapor", "rapor-2" };

        Assert.Equal("yeni", ReportCode.MakeUnique("yeni", taken.Contains));
        Assert.Equal("rapor-3", ReportCode.MakeUnique("rapor", taken.Contains));
    }

    [Fact]
    public void MakeUnique_keeps_the_suffix_inside_the_length_limit()
    {
        var full = new string('a', ReportCode.MaxLength);

        var code = ReportCode.MakeUnique(full, c => c == full);

        Assert.Equal(ReportCode.MaxLength, code.Length);
        Assert.EndsWith("-2", code, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("barkod-rapor_1", true)]
    [InlineData("Barkod", true)]
    [InlineData("barkod rapor", false)]
    [InlineData("rapör", false)]
    [InlineData("-baslangic", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValid_accepts_only_safe_ascii(string? code, bool expected) =>
        Assert.Equal(expected, ReportCode.IsValid(code));

    [Fact]
    public void Normalize_trims_and_lowercases() =>
        Assert.Equal("barkod-rapor", ReportCode.Normalize("  Barkod-Rapor "));
}
