#if !NETSTANDARD2_0
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace JetReportDesigner.Client
{
    /// <summary>Sends a report straight to a Windows printer. The paper is sized to the report's own page size.</summary>
    public static class ReportPrinter
    {
        /// <summary>The printers installed on this machine.</summary>
        public static IReadOnlyList<string> InstalledPrinters()
        {
            var list = new List<string>();
            foreach (string name in PrinterSettings.InstalledPrinters) list.Add(name);
            return list;
        }

        /// <param name="client">The client to render with.</param>
        /// <param name="report">Report name or id.</param>
        /// <param name="data">Rows to print.</param>
        /// <param name="printerName">Null prints to the default printer.</param>
        /// <param name="copies">Number of copies.</param>
        /// <param name="parameters">Report parameter values, by name.</param>
        /// <param name="dpi">Render resolution. 300 gives crisp barcodes and small text on a label printer.</param>
        /// <param name="cancellationToken">Cancels the render (not a job already sent to the printer).</param>
        public static async Task PrintAsync(
            this JetReportClient client,
            string report,
            ReportData data = null,
            string printerName = null,
            int copies = 1,
            IDictionary<string, object> parameters = null,
            int dpi = 300,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            var pages = await client.RenderPagesAsync(report, data, parameters, ReportFormat.Png, dpi, cancellationToken)
                .ConfigureAwait(false);
            var images = new List<Image>();
            try
            {
                foreach (var p in pages) images.Add(Image.FromStream(new MemoryStream(p.Content)));
                await Task.Run(() => Print(images, printerName, copies), cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                foreach (var image in images) image.Dispose();
            }
        }

        private static void Print(IReadOnlyList<Image> images, string printerName, int copies)
        {
            using (var document = new PrintDocument())
            {
                if (!string.IsNullOrWhiteSpace(printerName))
                {
                    document.PrinterSettings.PrinterName = printerName;
                    if (!document.PrinterSettings.IsValid)
                    {
                        throw new InvalidOperationException("Printer '" + printerName + "' was not found.");
                    }
                }

                document.PrinterSettings.Copies = (short)Math.Max(1, copies);
                document.DocumentName = "JetReportDesigner";
                document.OriginAtMargins = false;
                document.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);

                var index = 0;
                document.QueryPageSettings += (s, e) =>
                {
                    var image = images[index];
                    // Paper size in 1/100 inch, from the pixels and the DPI the image was rendered at.
                    var w = (int)Math.Round(image.Width / image.HorizontalResolution * 100);
                    var h = (int)Math.Round(image.Height / image.VerticalResolution * 100);
                    e.PageSettings.PaperSize = new PaperSize("JetReport", w, h);
                    e.PageSettings.Landscape = false;
                    e.PageSettings.Margins = new Margins(0, 0, 0, 0);
                };
                document.PrintPage += (s, e) =>
                {
                    var image = images[index];
                    e.Graphics.DrawImage(image, e.PageBounds);
                    index++;
                    e.HasMorePages = index < images.Count;
                };
                document.Print();
            }
        }
    }
}
#endif
