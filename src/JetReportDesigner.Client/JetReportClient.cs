using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace JetReportDesigner.Client
{
    /// <summary>
    /// Renders reports on a JetReportDesigner server. Create one instance and reuse it for the life of
    /// the application.
    /// </summary>
    public sealed class JetReportClient : IDisposable
    {
        private readonly HttpClient _http;
        private readonly bool _ownsHttp;
        private readonly SemaphoreSlim _lookupLock = new SemaphoreSlim(1, 1);
        private List<ReportInfo> _reports;

        public JetReportClient(JetReportClientOptions options)
            : this(options, new HttpClient(), true)
        {
        }

        /// <summary>Use your own <see cref="HttpClient"/> (e.g. from a factory); it is not disposed with the client.</summary>
        public JetReportClient(JetReportClientOptions options, HttpClient httpClient)
            : this(options, httpClient, false)
        {
        }

        private JetReportClient(JetReportClientOptions options, HttpClient http, bool ownsHttp)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (http == null) throw new ArgumentNullException(nameof(http));
            if (string.IsNullOrWhiteSpace(options.BaseUrl)) throw new ArgumentException("BaseUrl is required.", nameof(options));
            if (string.IsNullOrWhiteSpace(options.ApiKey)) throw new ArgumentException("ApiKey is required.", nameof(options));

            _http = http;
            _ownsHttp = ownsHttp;
            _http.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
            _http.Timeout = options.Timeout;
            _http.DefaultRequestHeaders.Remove("X-Api-Key");
            _http.DefaultRequestHeaders.Add("X-Api-Key", options.ApiKey);
        }

        /// <summary>
        /// Renders a report. <paramref name="report"/> is the report's code (the unique name shown in the designer,
        /// e.g. <c>barkod-rapor-claude</c>); its id or, failing both, its display name also work.
        /// </summary>
        /// <param name="data">Rows to fill the report with; null renders the report's own sample data.</param>
        /// <param name="parameters">Report parameter values, by name.</param>
        /// <param name="page">PNG/JPEG only: which page (1-based).</param>
        /// <param name="dpi">PNG/JPEG only: resolution. 150 is fine for screens, 300 for printing.</param>
        public async Task<RenderedReport> RenderAsync(
            string report,
            ReportData data = null,
            IDictionary<string, object> parameters = null,
            ReportFormat format = ReportFormat.Pdf,
            int page = 1,
            int dpi = 150,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            var id = await ResolveIdAsync(report, cancellationToken).ConfigureAwait(false);

            var body = new Dictionary<string, object>();
            if (parameters != null && parameters.Count > 0) body["parameters"] = parameters;
            if (data != null && data.Sources.Count > 0) body["data"] = data.Sources;

            var url = string.Format(
                CultureInfo.InvariantCulture,
                "api/reports/{0}/render?format={1}&page={2}&dpi={3}",
                id,
                FormatName(format),
                page,
                dpi);

            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
                using (var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        throw await ToExceptionAsync(response).ConfigureAwait(false);
                    }

                    int? pageCount = null;
                    IEnumerable<string> values;
                    int n;
                    if (response.Headers.TryGetValues("X-Page-Count", out values)
                        && int.TryParse(values.FirstOrDefault(), NumberStyles.Integer, CultureInfo.InvariantCulture, out n))
                    {
                        pageCount = n;
                    }

                    var bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                    var disposition = response.Content.Headers.ContentDisposition;
                    var fileName = disposition?.FileNameStar ?? disposition?.FileName?.Trim('"') ?? "report." + Extension(format);
                    var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
                    return new RenderedReport(bytes, contentType, fileName, pageCount);
                }
            }
        }

        /// <summary>Renders every page of the report as an image (PNG or JPEG), in order.</summary>
        public async Task<IReadOnlyList<RenderedReport>> RenderPagesAsync(
            string report,
            ReportData data = null,
            IDictionary<string, object> parameters = null,
            ReportFormat format = ReportFormat.Png,
            int dpi = 150,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (format != ReportFormat.Png && format != ReportFormat.Jpeg)
            {
                throw new ArgumentException("RenderPagesAsync is for image formats (Png or Jpeg).", nameof(format));
            }

            var pages = new List<RenderedReport>();
            var first = await RenderAsync(report, data, parameters, format, 1, dpi, cancellationToken).ConfigureAwait(false);
            pages.Add(first);
            for (var p = 2; p <= (first.PageCount ?? 1); p++)
            {
                pages.Add(await RenderAsync(report, data, parameters, format, p, dpi, cancellationToken).ConfigureAwait(false));
            }

            return pages;
        }

        /// <summary>Blocking version of <see cref="RenderAsync"/>, for code that is not async.</summary>
        public RenderedReport Render(
            string report,
            ReportData data = null,
            IDictionary<string, object> parameters = null,
            ReportFormat format = ReportFormat.Pdf,
            int page = 1,
            int dpi = 150) =>
            Task.Run(() => RenderAsync(report, data, parameters, format, page, dpi)).GetAwaiter().GetResult();

        /// <summary>The reports this application can render, with their codes.</summary>
        public async Task<IReadOnlyList<ReportInfo>> ListReportsAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            await LoadReportsAsync(cancellationToken, force: true).ConfigureAwait(false);
            return _reports.OrderBy(r => r.Code, StringComparer.Ordinal).ToList();
        }

        /// <summary>
        /// Reads what the report needs: its parameters and its data sources with their fields, so an application can
        /// build its input form from the report itself instead of hard-coding field names.
        /// </summary>
        public async Task<ReportSchema> GetSchemaAsync(string report, CancellationToken cancellationToken = default(CancellationToken))
        {
            var id = await ResolveIdAsync(report, cancellationToken).ConfigureAwait(false);
            using (var response = await _http.GetAsync("api/reports/" + id.ToString("D"), cancellationToken).ConfigureAwait(false))
            {
                if (!response.IsSuccessStatusCode) throw await ToExceptionAsync(response).ConfigureAwait(false);
                var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return ReportSchemaReader.Read(json);
            }
        }

        public void Dispose()
        {
            _lookupLock.Dispose();
            if (_ownsHttp) _http.Dispose();
        }

        private async Task<Guid> ResolveIdAsync(string report, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(report)) throw new ArgumentException("A report name or id is required.", nameof(report));
            Guid id;
            if (Guid.TryParse(report, out id)) return id;

            var wanted = report.Trim();

            // Reports can be added or renamed while the app runs, so refresh once before giving up.
            for (var attempt = 0; attempt < 2; attempt++)
            {
                await LoadReportsAsync(cancellationToken, force: attempt == 1).ConfigureAwait(false);

                var byCode = _reports.FirstOrDefault(r => string.Equals(r.Code, wanted, StringComparison.OrdinalIgnoreCase));
                if (byCode != null) return byCode.Id;

                var byName = _reports.Where(r => string.Equals(r.Name.Trim(), wanted, StringComparison.OrdinalIgnoreCase)).ToList();
                if (byName.Count > 1)
                {
                    throw new ReportClientException(
                        HttpStatusCode.Conflict,
                        "More than one report is named '" + report + "'. Use its code instead.");
                }

                if (byName.Count == 1) return byName[0].Id;
            }

            throw new ReportClientException(HttpStatusCode.NotFound, "No report with the code '" + report + "' was found.");
        }

        private async Task LoadReportsAsync(CancellationToken cancellationToken, bool force = false)
        {
            await _lookupLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_reports != null && !force) return;

                using (var response = await _http.GetAsync("api/reports", cancellationToken).ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode) throw await ToExceptionAsync(response).ConfigureAwait(false);

                    var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var list = new List<ReportInfo>();
                    using (var doc = JsonDocument.Parse(json))
                    {
                        foreach (var item in doc.RootElement.EnumerateArray())
                        {
                            JsonElement code;
                            var codeText = item.TryGetProperty("code", out code) && code.ValueKind == JsonValueKind.String
                                ? code.GetString()
                                : string.Empty;
                            list.Add(new ReportInfo(
                                item.GetProperty("id").GetGuid(),
                                codeText,
                                item.GetProperty("name").GetString() ?? string.Empty));
                        }
                    }

                    _reports = list;
                }
            }
            finally
            {
                _lookupLock.Release();
            }
        }

        private static async Task<ReportClientException> ToExceptionAsync(HttpResponseMessage response)
        {
            var text = response.Content == null ? string.Empty : await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var message = Describe(text);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                message = "The server rejected the API key (401). " + message;
            }

            return new ReportClientException(
                response.StatusCode,
                string.IsNullOrWhiteSpace(message)
                    ? "The server answered " + (int)response.StatusCode + " " + response.ReasonPhrase + "."
                    : message.Trim());
        }

        /// <summary>Pulls the readable part out of an RFC 7807 problem body.</summary>
        private static string Describe(string body)
        {
            if (string.IsNullOrWhiteSpace(body)) return string.Empty;
            try
            {
                using (var doc = JsonDocument.Parse(body))
                {
                    var root = doc.RootElement;
                    if (root.ValueKind != JsonValueKind.Object) return body;

                    var sb = new StringBuilder();
                    JsonElement title;
                    if (root.TryGetProperty("title", out title)) sb.Append(title.GetString());
                    JsonElement errors;
                    if (root.TryGetProperty("errors", out errors) && errors.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var field in errors.EnumerateObject())
                        {
                            foreach (var message in field.Value.EnumerateArray())
                            {
                                sb.Append(' ').Append(field.Name).Append(": ").Append(message.GetString());
                            }
                        }
                    }

                    return sb.Length > 0 ? sb.ToString() : body;
                }
            }
            catch (JsonException)
            {
                return body;
            }
        }

        private static string FormatName(ReportFormat format)
        {
            switch (format)
            {
                case ReportFormat.Html: return "html";
                case ReportFormat.Xlsx: return "xlsx";
                case ReportFormat.Png: return "png";
                case ReportFormat.Jpeg: return "jpeg";
                default: return "pdf";
            }
        }

        private static string Extension(ReportFormat format) => format == ReportFormat.Jpeg ? "jpg" : FormatName(format);
    }
}
