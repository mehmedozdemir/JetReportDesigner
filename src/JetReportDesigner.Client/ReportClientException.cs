using System;
using System.Net;

namespace JetReportDesigner.Client
{
    /// <summary>The server rejected the request or the render failed. <see cref="Exception.Message"/> says why.</summary>
    public sealed class ReportClientException : Exception
    {
        public ReportClientException(HttpStatusCode statusCode, string message)
            : base(message) => StatusCode = statusCode;

        public HttpStatusCode StatusCode { get; }
    }
}
