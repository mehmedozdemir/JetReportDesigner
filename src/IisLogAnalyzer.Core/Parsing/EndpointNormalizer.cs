namespace IisLogAnalyzer.Core.Parsing;

/// <summary>
/// Collapses path segments that look like identifiers (numeric ids, GUIDs) into
/// a placeholder so that e.g. /api/orders/123 and /api/orders/456 are grouped
/// under the same logical endpoint /api/orders/{id}.
/// </summary>
public static class EndpointNormalizer
{
    public static string Normalize(ReadOnlySpan<char> uriStem)
    {
        if (uriStem.IsEmpty)
            return "/";

        Span<Range> segmentRanges = stackalloc Range[64];
        var segmentSpan = uriStem.Split(segmentRanges, '/', StringSplitOptions.RemoveEmptyEntries);

        if (segmentSpan == 0)
            return "/";

        var sb = new System.Text.StringBuilder(uriStem.Length + 8);
        for (int i = 0; i < segmentSpan; i++)
        {
            var segment = uriStem[segmentRanges[i]];
            sb.Append('/');
            sb.Append(LooksLikeIdentifier(segment) ? "{id}" : segment);
        }

        return sb.Length == 0 ? "/" : sb.ToString();
    }

    private static bool LooksLikeIdentifier(ReadOnlySpan<char> segment)
    {
        if (segment.IsEmpty)
            return false;

        if (IsNumeric(segment))
            return true;

        if (Guid.TryParse(segment, out _))
            return true;

        return false;
    }

    private static bool IsNumeric(ReadOnlySpan<char> segment)
    {
        for (int i = 0; i < segment.Length; i++)
        {
            if (!char.IsAsciiDigit(segment[i]))
                return false;
        }
        return true;
    }
}
