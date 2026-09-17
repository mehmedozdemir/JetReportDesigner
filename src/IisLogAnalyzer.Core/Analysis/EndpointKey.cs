namespace IisLogAnalyzer.Core.Analysis;

public readonly record struct EndpointKey(string Method, string NormalizedPath)
{
    public override string ToString() => $"{Method} {NormalizedPath}";
}
