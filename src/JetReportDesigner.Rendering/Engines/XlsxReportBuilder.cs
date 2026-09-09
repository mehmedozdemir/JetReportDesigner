using System.Globalization;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using JetReportDesigner.Core.Binding;
using JetReportDesigner.Core.Model;
using JetReportDesigner.DataSources;

namespace JetReportDesigner.Rendering.Engines;

using Row = IReadOnlyDictionary<string, object?>;

/// <summary>
/// Builds an XLSX workbook from a report's resolved data. This is a data export, not a
/// pixel copy of the layout: every table element becomes a sheet, a banded report's
/// detail band becomes a sheet built from its field elements, and a free layout with
/// no table falls back to a label/value sheet. Numeric and date values are written
/// typed so the recipient can sort, filter and sum them.
/// </summary>
public static partial class XlsxReportBuilder
{
    public static byte[] Build(
        ReportDefinition report,
        ReportData data,
        IReadOnlyDictionary<string, object?> parameters)
    {
        var culture = CultureResolver.Resolve(report.Culture);
        var baseContext = new BindingContext(null, parameters) { Culture = culture };

        using var workbook = new XLWorkbook();
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var sheets = CollectSheets(report, data).ToList();
        foreach (var sheet in sheets)
        {
            WriteSheet(workbook, SheetName(sheet.Name, used), sheet, baseContext, culture);
        }

        if (sheets.Count == 0)
        {
            WriteFallbackSheet(workbook, report, data, baseContext);
        }

        if (report.Parameters.Count > 0)
        {
            WriteParametersSheet(workbook, report, parameters, culture);
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private sealed record Column(string Header, string Expr, string? Format, string? Field);

    private sealed record Sheet(string Name, IReadOnlyList<Column> Columns, IReadOnlyList<Row> Rows);

    private static IEnumerable<Sheet> CollectSheets(ReportDefinition report, ReportData data)
    {
        var primary = report.DataSources.FirstOrDefault()?.Name ?? string.Empty;

        var tableElements = (report.Body?.Elements ?? [])
            .Concat(report.Bands.SelectMany(b => b.Elements))
            .Where(e => e.Type == ElementType.Table && e.Table is { Columns.Count: > 0 });

        foreach (var element in tableElements)
        {
            var source = string.IsNullOrWhiteSpace(element.Table!.DataSource) ? primary : element.Table.DataSource;
            var columns = element.Table.Columns
                .Select(c => new Column(
                    string.IsNullOrWhiteSpace(c.Header) ? FieldOf(c.Value) ?? "Column" : c.Header,
                    c.Value,
                    c.Format,
                    FieldOf(c.Value)))
                .ToList();

            yield return new Sheet(source is { Length: > 0 } ? source : "Table", columns, data.Get(source).Rows);
        }

        if (report.LayoutMode == LayoutMode.Banded)
        {
            var detail = report.Bands.FirstOrDefault(b => b.Type == BandType.Detail && b.Visible);
            if (detail is not null)
            {
                var fields = detail.Elements
                    .Where(e => e.Type is ElementType.Field or ElementType.Label && !string.IsNullOrWhiteSpace(TextOf(e)))
                    .OrderBy(e => e.Bounds.X)
                    .Select(e => new Column(Titleize(FieldOf(TextOf(e)) ?? TextOf(e)!), TextOf(e)!, e.Format, FieldOf(TextOf(e))))
                    .ToList();

                if (fields.Count > 0)
                {
                    var source = string.IsNullOrWhiteSpace(detail.DataSource) ? primary : detail.DataSource!;
                    yield return new Sheet(source is { Length: > 0 } ? source : "Detail", fields, data.Get(source).Rows);
                }
            }
        }
    }

    private static void WriteSheet(
        XLWorkbook workbook,
        string name,
        Sheet sheet,
        BindingContext baseContext,
        CultureInfo culture)
    {
        var ws = workbook.Worksheets.Add(name);

        for (var c = 0; c < sheet.Columns.Count; c++)
        {
            ws.Cell(1, c + 1).Value = sheet.Columns[c].Header;
        }

        ws.Row(1).Style.Font.Bold = true;

        for (var r = 0; r < sheet.Rows.Count; r++)
        {
            var context = baseContext.WithRow(sheet.Rows[r]);
            for (var c = 0; c < sheet.Columns.Count; c++)
            {
                WriteValue(ws.Cell(r + 2, c + 1), sheet.Columns[c], sheet.Rows[r], context, culture);
            }
        }

        var lastRow = sheet.Rows.Count + 1;
        var range = ws.Range(1, 1, lastRow, Math.Max(1, sheet.Columns.Count));
        range.SetAutoFilter();
        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents(1, 60d);
    }

    private static void WriteValue(
        IXLCell cell,
        Column column,
        Row row,
        BindingContext context,
        CultureInfo culture)
    {
        if (column.Field is { } field && row.TryGetValue(field, out var raw) && raw is not null)
        {
            switch (raw)
            {
                case byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal:
                    cell.Value = Convert.ToDouble(raw, CultureInfo.InvariantCulture);
                    if (ToExcelFormat(column.Format) is { } nf)
                    {
                        cell.Style.NumberFormat.Format = nf;
                    }

                    return;

                case DateTime dt:
                    cell.Value = dt;
                    cell.Style.NumberFormat.Format = ToExcelFormat(column.Format) ?? "yyyy-mm-dd";
                    return;

                case bool b:
                    cell.Value = b;
                    return;
            }
        }

        cell.Value = BindingResolver.ResolveValue(column.Expr, column.Format, context);
    }

    private static void WriteFallbackSheet(
        XLWorkbook workbook,
        ReportDefinition report,
        ReportData data,
        BindingContext baseContext)
    {
        var primary = report.DataSources.FirstOrDefault()?.Name ?? string.Empty;
        var firstRow = data.Get(primary).Rows.FirstOrDefault();
        var context = firstRow is null ? baseContext : baseContext.WithRow(firstRow);

        var elements = (report.Body?.Elements ?? [])
            .Concat(report.Bands.SelectMany(b => b.Elements))
            .Where(e => e.Type is ElementType.Label or ElementType.Field or ElementType.PageInfo)
            .OrderBy(e => e.Bounds.Y)
            .ThenBy(e => e.Bounds.X)
            .ToList();

        var ws = workbook.Worksheets.Add("Report");
        ws.Cell(1, 1).Value = "Field";
        ws.Cell(1, 2).Value = "Value";
        ws.Row(1).Style.Font.Bold = true;

        var r = 2;
        foreach (var element in elements)
        {
            var raw = element.Type == ElementType.Label ? element.Text : element.Value;
            var field = FieldOf(raw);
            ws.Cell(r, 1).Value = Titleize(field ?? raw ?? $"Item {r - 1}");
            ws.Cell(r, 2).Value = BindingResolver.ResolveValue(raw, element.Format, context);
            r++;
        }

        ws.Columns().AdjustToContents(1, 80d);
    }

    private static void WriteParametersSheet(
        XLWorkbook workbook,
        ReportDefinition report,
        IReadOnlyDictionary<string, object?> parameters,
        CultureInfo culture)
    {
        var ws = workbook.Worksheets.Add("Parameters");
        ws.Cell(1, 1).Value = "Parameter";
        ws.Cell(1, 2).Value = "Value";
        ws.Row(1).Style.Font.Bold = true;

        var r = 2;
        foreach (var parameter in report.Parameters)
        {
            ws.Cell(r, 1).Value = string.IsNullOrWhiteSpace(parameter.Label) ? parameter.Name : parameter.Label;
            var value = parameters.TryGetValue(parameter.Name, out var v) ? v : parameter.DefaultValue;
            ws.Cell(r, 2).Value = value is null ? string.Empty : Convert.ToString(value, culture) ?? string.Empty;
            r++;
        }

        ws.Columns().AdjustToContents(1, 60d);
    }

    /// <summary>The <c>field</c> of a <c>{source.field}</c> or <c>source.field</c> token, else null.</summary>
    private static string? FieldOf(string? expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return null;
        }

        var match = TokenRegex().Match(expression.Trim());
        return match.Success ? match.Groups["field"].Value : null;
    }

    private static string? TextOf(ReportElement element) =>
        element.Type == ElementType.Label ? element.Text : element.Value;

    private static string Titleize(string raw)
    {
        var cleaned = raw.Replace('_', ' ').Replace('-', ' ').Trim();
        if (cleaned.Length == 0)
        {
            return raw;
        }

        return char.ToUpperInvariant(cleaned[0]) + cleaned[1..];
    }

    /// <summary>Maps the common .NET format specifiers the designer offers to Excel number formats.</summary>
    private static string? ToExcelFormat(string? format)
    {
        if (string.IsNullOrWhiteSpace(format))
        {
            return null;
        }

        var f = format.Trim();
        return f.ToLowerInvariant() switch
        {
            "n0" or "d0" => "#,##0",
            "n" or "n2" => "#,##0.00",
            "n1" => "#,##0.0",
            "c" or "c2" => "#,##0.00",
            "c0" => "#,##0",
            "p" or "p2" => "0.00%",
            "p0" => "0%",
            "f2" => "0.00",
            "f0" => "0",
            "d" => "yyyy-mm-dd",
            "g" or "gg" => "yyyy-mm-dd hh:mm",
            _ => f.IndexOfAny(['#', '0']) >= 0 ? f : null, // already an Excel/custom pattern
        };
    }

    private static string SheetName(string desired, HashSet<string> used)
    {
        var clean = InvalidSheetChars().Replace(desired, " ").Trim();
        if (clean.Length == 0)
        {
            clean = "Sheet";
        }

        if (clean.Length > 31)
        {
            clean = clean[..31];
        }

        var name = clean;
        var n = 2;
        while (!used.Add(name))
        {
            var suffix = $" ({n++})";
            name = clean.Length + suffix.Length > 31 ? clean[..(31 - suffix.Length)] + suffix : clean + suffix;
        }

        return name;
    }

    [GeneratedRegex(@"^\{?\s*(?<source>[A-Za-z_][\w]*)\.(?<field>[A-Za-z_][\w]*)\s*\}?$")]
    private static partial Regex TokenRegex();

    [GeneratedRegex(@"[\[\]\*/\\\?:]")]
    private static partial Regex InvalidSheetChars();
}
