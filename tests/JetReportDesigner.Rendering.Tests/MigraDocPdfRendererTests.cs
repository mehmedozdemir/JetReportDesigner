using JetReportDesigner.Rendering;
using JetReportDesigner.Rendering.Engines;

namespace JetReportDesigner.Rendering.Tests;

public class MigraDocPdfRendererTests
{
    [Fact]
    public void Renders_The_Sample_To_A_Valid_Pdf()
    {
        // SystemFontResolver reads OS fonts; on Linux the image installs
        // fonts-liberation. Exercised on Windows where the OS fonts resolve.
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var pdf = new MigraDocPdfRenderer().Render(SampleDocuments.HelloWorld());

        Assert.Equal("migradoc", new MigraDocPdfRenderer().EngineName);
        Assert.True(pdf.Length > 400);
        Assert.Equal("%PDF-"u8.ToArray(), pdf.AsSpan(0, 5).ToArray());
    }
}
