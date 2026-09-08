using JetReportDesigner.Core.Binding;
using JetReportDesigner.Core.Model;

namespace JetReportDesigner.Core.Validation;

public enum IssueSeverity
{
    Warning,
    Error,
}

/// <summary>A non-fatal design problem: something that will render but probably not as intended.</summary>
public sealed record ReportIssue(IssueSeverity Severity, string Message, string? ElementId = null);

/// <summary>
/// Walks a report and reports design problems the JSON schema and the structural
/// validator do not catch: bindings that name an unknown data source or parameter,
/// tables without columns, elements that fall outside their container, and so on.
/// Never throws — returns a list for the designer to surface.
/// </summary>
public static class ReportInspector
{
    public static IReadOnlyList<ReportIssue> Inspect(ReportDefinition report)
    {
        var issues = new List<ReportIssue>();
        var sources = report.DataSources.Select(d => d.Name).ToHashSet(StringComparer.Ordinal);
        var parameters = report.Parameters.Select(p => p.Name).ToHashSet(StringComparer.Ordinal);

        void CheckTokens(string? text, string elementId)
        {
            foreach (var token in BindingResolver.ReferencedTokens(text))
            {
                if (token.StartsWith("param:", StringComparison.Ordinal))
                {
                    var name = token["param:".Length..];
                    if (!parameters.Contains(name))
                    {
                        issues.Add(new ReportIssue(IssueSeverity.Error, $"Binding references unknown parameter '{name}'.", elementId));
                    }
                }
                else
                {
                    var source = token[..token.IndexOf('.')];
                    if (!sources.Contains(source))
                    {
                        issues.Add(new ReportIssue(IssueSeverity.Error, $"Binding references unknown data source '{source}'.", elementId));
                    }
                }
            }
        }

        void CheckElement(ReportElement element, double containerWidth, double containerHeight)
        {
            CheckTokens(element.Text, element.Id);
            CheckTokens(element.Value, element.Id);
            CheckTokens(element.VisibleWhen, element.Id);

            if (element.Type == ElementType.Table)
            {
                if (element.Table is null || element.Table.Columns.Count == 0)
                {
                    issues.Add(new ReportIssue(IssueSeverity.Warning, "Table has no columns.", element.Id));
                }
                else
                {
                    if (element.Table.DataSource.Length > 0 && !sources.Contains(element.Table.DataSource))
                    {
                        issues.Add(new ReportIssue(IssueSeverity.Error, $"Table uses unknown data source '{element.Table.DataSource}'.", element.Id));
                    }

                    foreach (var column in element.Table.Columns)
                    {
                        CheckTokens(column.Value, element.Id);
                    }
                }
            }

            if (element.StyleRef is { } styleRef && !report.Styles.ContainsKey(styleRef))
            {
                issues.Add(new ReportIssue(IssueSeverity.Warning, $"Element uses unknown style '{styleRef}'.", element.Id));
            }

            var b = element.Bounds;
            if (containerWidth > 0 && (b.X < -1 || b.Y < -1 || b.X + b.Width > containerWidth + 1 || b.Y + b.Height > containerHeight + 1))
            {
                issues.Add(new ReportIssue(IssueSeverity.Warning, "Element extends outside its area.", element.Id));
            }
        }

        if (report.LayoutMode == LayoutMode.Free && report.Body is { } body)
        {
            foreach (var element in body.Elements)
            {
                CheckElement(element, 0, 0); // page-size bounds check skipped (depends on paper geometry)
            }
        }

        foreach (var band in report.Bands)
        {
            if (band.Type is BandType.GroupHeader or BandType.GroupFooter
                && string.IsNullOrWhiteSpace(band.Group?.Expression))
            {
                issues.Add(new ReportIssue(IssueSeverity.Error, $"The {band.Type} band has no group expression."));
            }

            if (band.Type == BandType.Detail && string.IsNullOrWhiteSpace(band.DataSource))
            {
                issues.Add(new ReportIssue(IssueSeverity.Warning, "The detail band has no data source; it will render once."));
            }

            foreach (var element in band.Elements)
            {
                CheckElement(element, double.MaxValue, band.Height);
            }
        }

        return issues;
    }
}
