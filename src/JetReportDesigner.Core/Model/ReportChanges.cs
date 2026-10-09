using System.Text.Json;
using JetReportDesigner.Core.Serialization;

namespace JetReportDesigner.Core.Model;

/// <summary>
/// A short, language-neutral description of what a save changed, stored with each report version so the
/// history can say "3 elements added, page size changed" without comparing snapshots by hand. The
/// result is a list of tokens the UI turns into words: <c>name</c>, <c>code</c>, <c>page</c>,
/// <c>parameters</c>, <c>dataSources</c>, <c>styles</c>, <c>bands</c>, <c>added:N</c>, <c>removed:N</c>,
/// <c>edited:N</c>, <c>other</c>.
/// </summary>
public static class ReportChanges
{
    /// <summary>Tokens describing how <paramref name="next"/> differs from <paramref name="previous"/>. Empty = identical.</summary>
    public static IReadOnlyList<string> Summarize(ReportDefinition previous, ReportDefinition next)
    {
        var tokens = new List<string>();

        if (!string.Equals(previous.Name, next.Name, StringComparison.Ordinal))
        {
            tokens.Add("name");
        }

        if (!string.Equals(previous.Code, next.Code, StringComparison.Ordinal))
        {
            tokens.Add("code");
        }

        if (Differs(previous.Page, next.Page))
        {
            tokens.Add("page");
        }

        if (Differs(previous.Parameters, next.Parameters))
        {
            tokens.Add("parameters");
        }

        if (Differs(previous.DataSources, next.DataSources) || Differs(previous.Connections, next.Connections))
        {
            tokens.Add("dataSources");
        }

        if (Differs(previous.Styles, next.Styles))
        {
            tokens.Add("styles");
        }

        if (previous.LayoutMode == LayoutMode.Banded && next.LayoutMode == LayoutMode.Banded
            && previous.Bands.Count != next.Bands.Count)
        {
            tokens.Add("bands");
        }

        var before = Elements(previous);
        var after = Elements(next);
        var added = after.Keys.Count(id => !before.ContainsKey(id));
        var removed = before.Keys.Count(id => !after.ContainsKey(id));
        var edited = after.Count(kv => before.TryGetValue(kv.Key, out var old) && !string.Equals(old, kv.Value, StringComparison.Ordinal));

        if (added > 0)
        {
            tokens.Add($"added:{added}");
        }

        if (removed > 0)
        {
            tokens.Add($"removed:{removed}");
        }

        if (edited > 0)
        {
            tokens.Add($"edited:{edited}");
        }

        // Something differs, but none of the categories above explains it (band heights, culture, description…).
        if (tokens.Count == 0 && Differs(previous, next))
        {
            tokens.Add("other");
        }

        return tokens;
    }

    private static bool Differs<T>(T a, T b) =>
        !string.Equals(JsonSerializer.Serialize(a, ReportJson.Options), JsonSerializer.Serialize(b, ReportJson.Options), StringComparison.Ordinal);

    /// <summary>Every element of the report (body or all bands) by id, mapped to its serialized form.</summary>
    private static Dictionary<string, string> Elements(ReportDefinition report)
    {
        var all = new Dictionary<string, string>(StringComparer.Ordinal);
        void Add(IEnumerable<ReportElement> elements, string scope)
        {
            foreach (var element in elements)
            {
                // Ids are unique within a container; scope keeps equal ids in different bands apart.
                all[$"{scope}/{element.Id}"] = JsonSerializer.Serialize(element, ReportJson.Options);
            }
        }

        if (report.Body is not null)
        {
            Add(report.Body.Elements, "body");
        }

        for (var i = 0; i < report.Bands.Count; i++)
        {
            Add(report.Bands[i].Elements, $"band{i}");
        }

        return all;
    }
}
