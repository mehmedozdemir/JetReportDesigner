namespace IisLogAnalyzer.Core.Analysis;

/// <summary>
/// Detects endpoints whose response time is trending upward over the analyzed
/// period, using hourly-average response time as the signal and an ordinary
/// least-squares slope (ms/hour) as the trend indicator.
/// </summary>
public static class TrendAnalyzer
{
    private const int MinDistinctHours = 3;
    private const int MinSamples = 20;
    private const double MinSlopeMsPerHour = 5.0;
    private const double MinRelativeIncrease = 0.25;

    public static void ApplyTrends(IEnumerable<EndpointStatistics> endpoints)
    {
        foreach (var endpoint in endpoints)
            ApplyTrend(endpoint);
    }

    private static void ApplyTrend(EndpointStatistics endpoint)
    {
        if (endpoint.RequestCount < MinSamples)
            return;

        var hourly = endpoint.Samples
            .GroupBy(s => new DateTime(s.Timestamp.Year, s.Timestamp.Month, s.Timestamp.Day, s.Timestamp.Hour, 0, 0, DateTimeKind.Utc))
            .OrderBy(g => g.Key)
            .Select(g => (Hour: g.Key, AvgMs: g.Average(s => (double)s.TimeTakenMs)))
            .ToArray();

        if (hourly.Length < MinDistinctHours)
            return;

        var baseHour = hourly[0].Hour;
        var points = hourly.Select(h => (X: (h.Hour - baseHour).TotalHours, Y: h.AvgMs)).ToArray();

        var slope = OrdinaryLeastSquaresSlope(points);
        endpoint.TrendSlopeMsPerHour = slope;

        var firstAvg = hourly[0].AvgMs;
        var lastAvg = hourly[^1].AvgMs;
        var relativeIncrease = firstAvg <= 0 ? 0 : (lastAvg - firstAvg) / firstAvg;

        endpoint.IsDegradingTrend = slope >= MinSlopeMsPerHour && relativeIncrease >= MinRelativeIncrease;
    }

    private static double OrdinaryLeastSquaresSlope((double X, double Y)[] points)
    {
        var n = points.Length;
        var sumX = points.Sum(p => p.X);
        var sumY = points.Sum(p => p.Y);
        var sumXY = points.Sum(p => p.X * p.Y);
        var sumXX = points.Sum(p => p.X * p.X);

        var denominator = n * sumXX - sumX * sumX;
        if (Math.Abs(denominator) < 1e-9)
            return 0;

        return (n * sumXY - sumX * sumY) / denominator;
    }
}
