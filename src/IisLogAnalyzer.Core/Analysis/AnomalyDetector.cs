namespace IisLogAnalyzer.Core.Analysis;

/// <summary>
/// Flags traffic-volume and error-rate anomalies using a z-score against the
/// mean/standard deviation of hourly buckets. Simple and explainable on
/// purpose: every flagged bucket can be justified with "N standard deviations
/// away from the period average" in the report.
/// </summary>
public static class AnomalyDetector
{
    private const int MinBucketsRequired = 4;
    private const double ZScoreThreshold = 2.5;
    private const int MinRequestsForVolumeSignal = 10;

    public static IReadOnlyList<TrafficAnomaly> Detect(IReadOnlyList<TimeBucketStats> buckets)
    {
        if (buckets.Count < MinBucketsRequired)
            return [];

        var results = new List<TrafficAnomaly>();

        DetectVolumeAnomalies(buckets, results);
        DetectErrorRateAnomalies(buckets, results);

        return results.OrderBy(a => a.BucketStartUtc).ToList();
    }

    private static void DetectVolumeAnomalies(IReadOnlyList<TimeBucketStats> buckets, List<TrafficAnomaly> results)
    {
        var counts = buckets.Select(b => (double)b.RequestCount).ToArray();
        var (mean, std) = MeanAndStdDev(counts);
        if (std <= 0 || mean < MinRequestsForVolumeSignal)
            return;

        foreach (var bucket in buckets)
        {
            var z = (bucket.RequestCount - mean) / std;
            if (z >= ZScoreThreshold)
            {
                results.Add(new TrafficAnomaly(bucket.BucketStartUtc, AnomalyType.TrafficSpike,
                    bucket.RequestCount, mean, z,
                    $"{bucket.BucketStartUtc:yyyy-MM-dd HH:mm} saatinde {bucket.RequestCount} istek, ortalamanın ({mean:F0}) {z:F1} std. sapma üzerinde."));
            }
            else if (z <= -ZScoreThreshold)
            {
                results.Add(new TrafficAnomaly(bucket.BucketStartUtc, AnomalyType.TrafficDrop,
                    bucket.RequestCount, mean, z,
                    $"{bucket.BucketStartUtc:yyyy-MM-dd HH:mm} saatinde {bucket.RequestCount} istek, ortalamanın ({mean:F0}) {Math.Abs(z):F1} std. sapma altında."));
            }
        }
    }

    private static void DetectErrorRateAnomalies(IReadOnlyList<TimeBucketStats> buckets, List<TrafficAnomaly> results)
    {
        var relevant = buckets.Where(b => b.RequestCount >= MinRequestsForVolumeSignal).ToArray();
        if (relevant.Length < MinBucketsRequired)
            return;

        var rates = relevant.Select(b => b.ErrorRatePercent).ToArray();
        var (mean, std) = MeanAndStdDev(rates);
        if (std <= 0)
            return;

        foreach (var bucket in relevant)
        {
            var z = (bucket.ErrorRatePercent - mean) / std;
            if (z >= ZScoreThreshold && bucket.ErrorRatePercent > mean)
            {
                results.Add(new TrafficAnomaly(bucket.BucketStartUtc, AnomalyType.ErrorRateSpike,
                    bucket.ErrorRatePercent, mean, z,
                    $"{bucket.BucketStartUtc:yyyy-MM-dd HH:mm} saatinde hata oranı %{bucket.ErrorRatePercent:F1}, ortalamanın (%{mean:F1}) {z:F1} std. sapma üzerinde."));
            }
        }
    }

    private static (double Mean, double StdDev) MeanAndStdDev(double[] values)
    {
        if (values.Length == 0)
            return (0, 0);

        var mean = values.Average();
        var variance = values.Sum(v => (v - mean) * (v - mean)) / values.Length;
        return (mean, Math.Sqrt(variance));
    }
}
