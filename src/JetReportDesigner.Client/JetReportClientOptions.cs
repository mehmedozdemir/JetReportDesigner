using System;

namespace JetReportDesigner.Client
{
    public sealed class JetReportClientOptions
    {
        /// <summary>Address of the JetReportDesigner server, e.g. <c>http://reports.corp.local:8081</c>.</summary>
        public string BaseUrl { get; set; } = string.Empty;

        /// <summary>The API key issued for this application (see docs/entegrasyon.md).</summary>
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>How long a single render may take. Default 2 minutes.</summary>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(2);
    }
}
