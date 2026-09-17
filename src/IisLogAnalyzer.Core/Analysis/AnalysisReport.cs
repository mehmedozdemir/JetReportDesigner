namespace IisLogAnalyzer.Core.Analysis;

public sealed class AnalysisReport
{
    public required DateTime GeneratedAtUtc { get; init; }
    public required DateTime? PeriodStartUtc { get; init; }
    public required DateTime? PeriodEndUtc { get; init; }

    public required int TotalRequests { get; init; }
    public required int TotalClientErrors { get; init; }
    public required int TotalServerErrors { get; init; }
    public required double AvgResponseTimeMs { get; init; }

    /// <summary>Set by the caller after enumeration finishes, since parse warnings
    /// stream in lazily alongside the entries that <see cref="LogAnalysisEngine.Analyze"/> consumes.</summary>
    public int ParseWarningCount { get; set; }

    public required IReadOnlyList<EndpointStatistics> Endpoints { get; init; }
    public required IReadOnlyList<TimeBucketStats> HourlyTraffic { get; init; }
    public required IReadOnlyList<TrafficAnomaly> Anomalies { get; init; }
    public required IReadOnlyList<EndpointStatistics> DegradingEndpoints { get; init; }
    public required IReadOnlyList<EndpointStatistics> TopSlowEndpoints { get; init; }
    public required IReadOnlyList<EndpointStatistics> TopErrorEndpoints { get; init; }
    public required IReadOnlyList<EndpointStatistics> TopProblematicEndpoints { get; init; }

    public double OverallErrorRatePercent => TotalRequests == 0 ? 0 : (TotalClientErrors + TotalServerErrors) * 100.0 / TotalRequests;
}
