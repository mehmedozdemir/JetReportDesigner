namespace IisLogAnalyzer.Core.Analysis;

public enum AnomalyType
{
    TrafficSpike,
    TrafficDrop,
    ErrorRateSpike,
}

public sealed record TrafficAnomaly(
    DateTime BucketStartUtc,
    AnomalyType Type,
    double ObservedValue,
    double ExpectedValue,
    double DeviationScore,
    string Description);
