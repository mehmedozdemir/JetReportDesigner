using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace JetReportDesigner.Client
{
    /// <summary>A rendered report: the file bytes plus what they are.</summary>
    public sealed class RenderedReport
    {
        public RenderedReport(byte[] content, string contentType, string fileName, int? pageCount)
        {
            Content = content;
            ContentType = contentType;
            FileName = fileName;
            PageCount = pageCount;
        }

        public byte[] Content { get; }

        public string ContentType { get; }

        /// <summary>Suggested file name, e.g. <c>Barkod Rapor - Claude.pdf</c>.</summary>
        public string FileName { get; }

        /// <summary>For PNG/JPEG: how many pages the whole report has (this result is one of them). Otherwise null.</summary>
        public int? PageCount { get; }

        /// <summary>Writes the report to <paramref name="path"/> (a directory means: use <see cref="FileName"/> inside it).</summary>
        public string Save(string path)
        {
            if (Directory.Exists(path)) path = Path.Combine(path, FileName);
            File.WriteAllBytes(path, Content);
            return path;
        }

        public Task<string> SaveAsync(string path, CancellationToken cancellationToken = default(CancellationToken)) =>
            Task.Run(() => Save(path), cancellationToken);
    }
}
