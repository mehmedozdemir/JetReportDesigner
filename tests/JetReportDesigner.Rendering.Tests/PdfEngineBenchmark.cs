using System.Diagnostics;
using JetReportDesigner.Rendering;
using JetReportDesigner.Rendering.Engines;
using Xunit.Abstractions;

namespace JetReportDesigner.Rendering.Tests;

/// <summary>
/// Not an assertion test — an informational run that prints the numbers filled into
/// docs/04-pdf-motoru-karari.md. Also drops sample PDFs under the OS temp dir for a
/// visual check. Enable with the RUN_PDF_BENCHMARK env var.
/// </summary>
public class PdfEngineBenchmark(ITestOutputHelper output)
{
    private static bool Enabled =>
        string.Equals(Environment.GetEnvironmentVariable("RUN_PDF_BENCHMARK"), "1", StringComparison.Ordinal);

    [Fact]
    public void Compare_Engines()
    {
        if (!Enabled)
        {
            return;
        }

        var doc = SampleDocuments.HelloWorld();
        var outDir = Path.Combine(Path.GetTempPath(), "jetreport-pdf-spike");
        Directory.CreateDirectory(outDir);

        foreach (IPdfRenderer renderer in new IPdfRenderer[] { new QuestPdfRenderer(), new MigraDocPdfRenderer() })
        {
            try
            {
                var cold = Stopwatch.StartNew();
                var bytes = renderer.Render(doc);
                cold.Stop();

                var warm = Stopwatch.StartNew();
                for (var i = 0; i < 20; i++)
                {
                    renderer.Render(doc);
                }

                warm.Stop();

                var path = Path.Combine(outDir, $"hello-{renderer.EngineName}.pdf");
                File.WriteAllBytes(path, bytes);

                output.WriteLine(
                    $"{renderer.EngineName,-10} ok  size={bytes.Length,7} B  cold={cold.ElapsedMilliseconds,5} ms  "
                    + $"warm/render={warm.Elapsed.TotalMilliseconds / 20,7:F2} ms  -> {path}");
            }
            catch (Exception ex)
            {
                output.WriteLine($"{renderer.EngineName,-10} FAILED: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }
}
