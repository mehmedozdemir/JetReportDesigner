using JetReportDesigner.Rendering;
using JetReportDesigner.Rendering.Engines;

namespace JetReportDesigner.Rendering.Tests;

/// <summary>
/// Phase 0 spike smoke tests: every candidate <see cref="IPdfRenderer"/> produces a
/// valid PDF for the shared sample. Detailed fidelity/perf comparison and the
/// decision live in docs/04-pdf-motoru-karari.md.
/// </summary>
public class PdfEngineSpikeTests
{
    private static readonly RenderDocument Sample = SampleDocuments.HelloWorld();

    private static void AssertIsPdf(byte[] bytes)
    {
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 400, $"PDF unexpectedly small: {bytes.Length} bytes");
        Assert.Equal("%PDF-"u8.ToArray(), bytes.AsSpan(0, 5).ToArray());
    }

    [Fact]
    public void QuestPdf_Renders_The_Sample()
    {
        var pdf = new QuestPdfRenderer().Render(Sample);
        AssertIsPdf(pdf);
    }

    [Fact]
    public void MigraDoc_Renders_The_Sample()
    {
        // SystemFontResolver reads OS fonts; on Linux the image installs fonts-liberation
        // (a spike finding). Exercised on Windows where the OS fonts resolve.
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var pdf = new MigraDocPdfRenderer().Render(Sample);
        AssertIsPdf(pdf);
    }

    [Fact]
    public void Both_Engines_Report_Distinct_Names()
    {
        Assert.Equal("questpdf", new QuestPdfRenderer().EngineName);
        Assert.Equal("migradoc", new MigraDocPdfRenderer().EngineName);
    }
}
