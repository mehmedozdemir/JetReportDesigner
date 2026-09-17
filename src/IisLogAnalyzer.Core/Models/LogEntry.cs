namespace IisLogAnalyzer.Core.Models;

public sealed class LogEntry
{
    public required DateTime Timestamp { get; init; }
    public string? ServerIp { get; init; }
    public required string Method { get; init; }
    public required string UriStem { get; init; }
    public string? UriQuery { get; init; }
    public int? Port { get; init; }
    public string? Username { get; init; }
    public string? ClientIp { get; init; }
    public string? UserAgent { get; init; }
    public string? Referer { get; init; }
    public required int StatusCode { get; init; }
    public int SubStatusCode { get; init; }
    public int Win32Status { get; init; }
    public required long TimeTakenMs { get; init; }

    public bool IsError => StatusCode >= 400;
    public bool IsServerError => StatusCode >= 500;
    public bool IsClientError => StatusCode is >= 400 and < 500;
}
