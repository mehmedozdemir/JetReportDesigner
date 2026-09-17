using IisLogAnalyzer.Core.Models;
using IisLogAnalyzer.Core.Parsing;

namespace IisLogAnalyzer.Core.Analysis;

public sealed class LogAnalysisEngine
{
    private const int TopListSize = 15;
    private const int MinRequestsForErrorRanking = 5;

    public event Action<int>? OnProgress;

    /// <summary>
    /// Consumes the entry stream in a single pass, aggregating per-endpoint and
    /// per-hour statistics as it goes so memory stays proportional to the number
    /// of distinct endpoints/hours rather than the raw line count.
    /// </summary>
    public AnalysisReport Analyze(IEnumerable<LogEntry> entries)
    {
        var endpoints = new Dictionary<EndpointKey, EndpointStatistics>();
        var buckets = new Dictionary<DateTime, TimeBucketStats>();

        int totalRequests = 0, totalClientErrors = 0, totalServerErrors = 0;
        long totalTimeTaken = 0;
        DateTime? periodStart = null, periodEnd = null;
        int processed = 0;

        foreach (var entry in entries)
        {
            processed++;
            if (processed % 5000 == 0)
                OnProgress?.Invoke(processed);

            if (entry.Timestamp == DateTime.MinValue)
                continue;

            totalRequests++;
            totalTimeTaken += entry.TimeTakenMs;
            if (entry.IsServerError) totalServerErrors++;
            else if (entry.IsClientError) totalClientErrors++;

            if (periodStart is null || entry.Timestamp < periodStart) periodStart = entry.Timestamp;
            if (periodEnd is null || entry.Timestamp > periodEnd) periodEnd = entry.Timestamp;

            var normalizedPath = EndpointNormalizer.Normalize(entry.UriStem);
            var key = new EndpointKey(entry.Method, normalizedPath);

            if (!endpoints.TryGetValue(key, out var stats))
            {
                stats = new EndpointStatistics { Method = entry.Method, NormalizedPath = normalizedPath };
                endpoints[key] = stats;
            }
            stats.AddSample(entry.Timestamp, entry.TimeTakenMs, entry.StatusCode);

            var bucketKey = new DateTime(entry.Timestamp.Year, entry.Timestamp.Month, entry.Timestamp.Day, entry.Timestamp.Hour, 0, 0, DateTimeKind.Utc);
            if (!buckets.TryGetValue(bucketKey, out var bucket))
            {
                bucket = new TimeBucketStats { BucketStartUtc = bucketKey };
                buckets[bucketKey] = bucket;
            }
            bucket.RequestCount++;
            bucket.TotalTimeTakenMs += entry.TimeTakenMs;
            if (entry.IsError) bucket.ErrorCount++;
        }

        OnProgress?.Invoke(processed);

        var endpointList = endpoints.Values.ToList();
        foreach (var e in endpointList)
            e.ComputePercentiles();

        TrendAnalyzer.ApplyTrends(endpointList);

        var hourlyTraffic = buckets.Values.OrderBy(b => b.BucketStartUtc).ToList();
        var anomalies = AnomalyDetector.Detect(hourlyTraffic);

        ComputeProblemScores(endpointList);

        var degrading = endpointList.Where(e => e.IsDegradingTrend)
            .OrderByDescending(e => e.TrendSlopeMsPerHour)
            .ToList();

        var topSlow = endpointList.Where(e => e.RequestCount >= MinRequestsForErrorRanking)
            .OrderByDescending(e => e.P95ResponseTimeMs)
            .Take(TopListSize)
            .ToList();

        var topErrors = endpointList.Where(e => e.RequestCount >= MinRequestsForErrorRanking && e.ErrorRatePercent > 0)
            .OrderByDescending(e => e.ErrorRatePercent)
            .ThenByDescending(e => e.RequestCount)
            .Take(TopListSize)
            .ToList();

        var topProblematic = endpointList.Where(e => e.RequestCount >= MinRequestsForErrorRanking)
            .OrderByDescending(e => e.ProblemScore)
            .Take(TopListSize)
            .ToList();

        return new AnalysisReport
        {
            GeneratedAtUtc = DateTime.UtcNow,
            PeriodStartUtc = periodStart,
            PeriodEndUtc = periodEnd,
            TotalRequests = totalRequests,
            TotalClientErrors = totalClientErrors,
            TotalServerErrors = totalServerErrors,
            AvgResponseTimeMs = totalRequests == 0 ? 0 : (double)totalTimeTaken / totalRequests,
            Endpoints = endpointList.OrderByDescending(e => e.RequestCount).ToList(),
            HourlyTraffic = hourlyTraffic,
            Anomalies = anomalies,
            DegradingEndpoints = degrading,
            TopSlowEndpoints = topSlow,
            TopErrorEndpoints = topErrors,
            TopProblematicEndpoints = topProblematic,
        };
    }

    /// <summary>
    /// Combines error rate, p95 latency and degrading-trend signals into a single
    /// 0-100 ranking score per endpoint, using min-max normalization across the
    /// current dataset so the score is always relative to this run's own scale.
    /// </summary>
    private static void ComputeProblemScores(List<EndpointStatistics> endpoints)
    {
        var eligible = endpoints.Where(e => e.RequestCount >= MinRequestsForErrorRanking).ToList();
        if (eligible.Count == 0)
            return;

        var maxErrorRate = eligible.Max(e => e.ErrorRatePercent);
        var maxP95 = eligible.Max(e => e.P95ResponseTimeMs);

        foreach (var e in eligible)
        {
            var errorNorm = maxErrorRate <= 0 ? 0 : e.ErrorRatePercent / maxErrorRate;
            var latencyNorm = maxP95 <= 0 ? 0 : (double)e.P95ResponseTimeMs / maxP95;
            var trendBonus = e.IsDegradingTrend ? 1.0 : 0.0;

            e.ProblemScore = Math.Round((errorNorm * 55 + latencyNorm * 30 + trendBonus * 15), 1);
        }
    }
}
