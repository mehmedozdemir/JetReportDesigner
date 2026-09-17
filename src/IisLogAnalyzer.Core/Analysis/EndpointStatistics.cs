namespace IisLogAnalyzer.Core.Analysis;

public sealed class EndpointStatistics
{
    public required string Method { get; init; }
    public required string NormalizedPath { get; init; }

    public int RequestCount { get; internal set; }
    public long TotalTimeTakenMs { get; internal set; }
    public long MinTimeTakenMs { get; internal set; } = long.MaxValue;
    public long MaxTimeTakenMs { get; internal set; }
    public int ClientErrorCount { get; internal set; }
    public int ServerErrorCount { get; internal set; }
    public Dictionary<int, int> StatusCodeCounts { get; } = new();

    internal List<(DateTime Timestamp, long TimeTakenMs)> Samples { get; } = new();

    public long P50ResponseTimeMs { get; private set; }
    public long P90ResponseTimeMs { get; private set; }
    public long P95ResponseTimeMs { get; private set; }
    public long P99ResponseTimeMs { get; private set; }

    public double TrendSlopeMsPerHour { get; internal set; }
    public bool IsDegradingTrend { get; internal set; }
    public double ProblemScore { get; internal set; }

    public double AvgResponseTimeMs => RequestCount == 0 ? 0 : (double)TotalTimeTakenMs / RequestCount;
    public double ErrorRatePercent => RequestCount == 0 ? 0 : (ClientErrorCount + ServerErrorCount) * 100.0 / RequestCount;
    public double ServerErrorRatePercent => RequestCount == 0 ? 0 : ServerErrorCount * 100.0 / RequestCount;

    internal void AddSample(DateTime timestamp, long timeTakenMs, int statusCode)
    {
        RequestCount++;
        TotalTimeTakenMs += timeTakenMs;
        if (timeTakenMs < MinTimeTakenMs) MinTimeTakenMs = timeTakenMs;
        if (timeTakenMs > MaxTimeTakenMs) MaxTimeTakenMs = timeTakenMs;

        if (statusCode is >= 500) ServerErrorCount++;
        else if (statusCode is >= 400 and < 500) ClientErrorCount++;

        StatusCodeCounts.TryGetValue(statusCode, out var count);
        StatusCodeCounts[statusCode] = count + 1;

        Samples.Add((timestamp, timeTakenMs));
    }

    internal void ComputePercentiles()
    {
        if (Samples.Count == 0)
        {
            MinTimeTakenMs = 0;
            return;
        }

        var sorted = Samples.Select(s => s.TimeTakenMs).OrderBy(v => v).ToArray();
        P50ResponseTimeMs = Percentile(sorted, 50);
        P90ResponseTimeMs = Percentile(sorted, 90);
        P95ResponseTimeMs = Percentile(sorted, 95);
        P99ResponseTimeMs = Percentile(sorted, 99);
    }

    private static long Percentile(long[] sortedValues, int percentile)
    {
        if (sortedValues.Length == 0) return 0;
        if (sortedValues.Length == 1) return sortedValues[0];

        var rank = (percentile / 100.0) * (sortedValues.Length - 1);
        var lowerIndex = (int)Math.Floor(rank);
        var upperIndex = (int)Math.Ceiling(rank);
        if (lowerIndex == upperIndex) return sortedValues[lowerIndex];

        var fraction = rank - lowerIndex;
        return (long)Math.Round(sortedValues[lowerIndex] + (sortedValues[upperIndex] - sortedValues[lowerIndex]) * fraction);
    }
}
