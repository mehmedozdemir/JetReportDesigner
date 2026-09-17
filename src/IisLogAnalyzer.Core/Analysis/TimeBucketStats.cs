namespace IisLogAnalyzer.Core.Analysis;

public sealed class TimeBucketStats
{
    public required DateTime BucketStartUtc { get; init; }
    public int RequestCount { get; internal set; }
    public int ErrorCount { get; internal set; }
    public long TotalTimeTakenMs { get; internal set; }

    public double AvgResponseTimeMs => RequestCount == 0 ? 0 : (double)TotalTimeTakenMs / RequestCount;
    public double ErrorRatePercent => RequestCount == 0 ? 0 : ErrorCount * 100.0 / RequestCount;
}
