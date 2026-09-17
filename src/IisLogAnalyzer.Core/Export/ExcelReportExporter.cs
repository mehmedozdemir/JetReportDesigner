using ClosedXML.Excel;
using IisLogAnalyzer.Core.Analysis;
using IisLogAnalyzer.Core.Charting;
using IisLogAnalyzer.Core.Localization;

namespace IisLogAnalyzer.Core.Export;

public static class ExcelReportExporter
{
    public static void Export(AnalysisReport report, string filePath, AppLanguage language = AppLanguage.Turkish)
    {
        using var workbook = new XLWorkbook();

        WriteSummarySheet(workbook, report, language);
        WriteEndpointsSheet(workbook, report, language);
        WriteProblematicSheet(workbook, report, language);
        WriteHourlyTrafficSheet(workbook, report, language);
        WriteAnomaliesSheet(workbook, report, language);
        WriteGlossarySheet(workbook, language);

        workbook.SaveAs(filePath);
    }

    private static void WriteSummarySheet(XLWorkbook workbook, AnalysisReport report, AppLanguage lang)
    {
        var ws = workbook.Worksheets.Add(Loc.T("report.sheet.summary", lang));
        var row = 1;

        ws.Cell(row, 1).Value = Loc.T("report.title", lang);
        ws.Cell(row, 1).Style.Font.SetBold().Font.SetFontSize(16);
        row += 2;

        void KeyValue(string labelKey, object value)
        {
            ws.Cell(row, 1).Value = Loc.T(labelKey, lang);
            ws.Cell(row, 1).Style.Font.SetBold();
            ws.Cell(row, 2).Value = value.ToString();
            row++;
        }

        KeyValue("report.generatedAt", report.GeneratedAtUtc);
        KeyValue("report.periodStart", report.PeriodStartUtc?.ToString("yyyy-MM-dd HH:mm") ?? "-");
        KeyValue("report.periodEnd", report.PeriodEndUtc?.ToString("yyyy-MM-dd HH:mm") ?? "-");
        KeyValue("report.totalRequests", report.TotalRequests);
        KeyValue("report.client4xx", report.TotalClientErrors);
        KeyValue("report.server5xx", report.TotalServerErrors);
        KeyValue("report.overallErrorRate", Math.Round(report.OverallErrorRatePercent, 2));
        KeyValue("report.avgResponseTime", Math.Round(report.AvgResponseTimeMs, 1));
        KeyValue("report.distinctEndpoints", report.Endpoints.Count);
        KeyValue("report.anomalyCount", report.Anomalies.Count);
        KeyValue("report.degradingCount", report.DegradingEndpoints.Count);
        KeyValue("report.parseWarnings", report.ParseWarningCount);

        ws.Columns(1, 2).AdjustToContents();

        row += 1;
        const int chartRowSpan = 20; // approximate rows spanned by a 300px-tall picture at default row height
        try
        {
            var trafficPng = ChartRenderer.TrafficChartPng(report, lang);
            using var trafficStream = new MemoryStream(trafficPng);
            ws.AddPicture(trafficStream).MoveTo(ws.Cell(row, 1)).WithSize(700, 300);

            var responsePng = ChartRenderer.ResponseTimeChartPng(report, lang);
            using var responseStream = new MemoryStream(responsePng);
            ws.AddPicture(responseStream).MoveTo(ws.Cell(row + chartRowSpan + 1, 1)).WithSize(700, 300);
        }
        catch
        {
            // Chart rendering is a visual nicety; a rendering failure must never
            // block the (already computed) tabular report from being saved.
        }
    }

    private static void WriteEndpointsSheet(XLWorkbook workbook, AnalysisReport report, AppLanguage lang)
    {
        var ws = workbook.Worksheets.Add(Loc.T("report.sheet.allEndpoints", lang));
        var headers = new[]
        {
            Loc.T("col.method", lang), Loc.T("col.path", lang), Loc.T("col.requests", lang), Loc.T("col.avg", lang),
            Loc.T("col.p50", lang), Loc.T("col.p90", lang), Loc.T("col.p95", lang), Loc.T("col.p99", lang),
            Loc.T("col.err4xx", lang), Loc.T("col.err5xx", lang), Loc.T("col.errorRatePct", lang),
            Loc.T("col.slope", lang), Loc.T("col.trend", lang), Loc.T("col.problemScore", lang),
        };
        WriteHeaderRow(ws, headers);

        var row = 2;
        foreach (var e in report.Endpoints)
        {
            ws.Cell(row, 1).Value = e.Method;
            ws.Cell(row, 2).Value = e.NormalizedPath;
            ws.Cell(row, 3).Value = e.RequestCount;
            ws.Cell(row, 4).Value = Math.Round(e.AvgResponseTimeMs, 1);
            ws.Cell(row, 5).Value = e.P50ResponseTimeMs;
            ws.Cell(row, 6).Value = e.P90ResponseTimeMs;
            ws.Cell(row, 7).Value = e.P95ResponseTimeMs;
            ws.Cell(row, 8).Value = e.P99ResponseTimeMs;
            ws.Cell(row, 9).Value = e.ClientErrorCount;
            ws.Cell(row, 10).Value = e.ServerErrorCount;
            ws.Cell(row, 11).Value = Math.Round(e.ErrorRatePercent, 2);
            ws.Cell(row, 12).Value = Math.Round(e.TrendSlopeMsPerHour, 2);
            ws.Cell(row, 13).Value = e.IsDegradingTrend ? Loc.T("trend.degrading", lang) : Loc.T("trend.stable", lang);
            ws.Cell(row, 14).Value = e.ProblemScore;
            row++;
        }

        var table = ws.RangeUsed()!.AsTable();
        table.Theme = XLTableTheme.TableStyleMedium2;
        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents();
    }

    private static void WriteProblematicSheet(XLWorkbook workbook, AnalysisReport report, AppLanguage lang)
    {
        var ws = workbook.Worksheets.Add(Loc.T("report.sheet.problematic", lang));
        var headers = new[]
        {
            Loc.T("col.rank", lang), Loc.T("col.method", lang), Loc.T("col.path", lang), Loc.T("col.problemScore", lang),
            Loc.T("col.errorRatePct", lang), Loc.T("col.p95", lang), Loc.T("col.trend", lang), Loc.T("col.requests", lang),
        };
        WriteHeaderRow(ws, headers);

        var row = 2;
        var rank = 1;
        foreach (var e in report.TopProblematicEndpoints)
        {
            ws.Cell(row, 1).Value = rank++;
            ws.Cell(row, 2).Value = e.Method;
            ws.Cell(row, 3).Value = e.NormalizedPath;
            ws.Cell(row, 4).Value = e.ProblemScore;
            ws.Cell(row, 5).Value = Math.Round(e.ErrorRatePercent, 2);
            ws.Cell(row, 6).Value = e.P95ResponseTimeMs;
            ws.Cell(row, 7).Value = e.IsDegradingTrend ? Loc.T("trend.degrading", lang) : Loc.T("trend.stable", lang);
            ws.Cell(row, 8).Value = e.RequestCount;
            row++;
        }

        if (report.TopProblematicEndpoints.Count > 0)
        {
            var table = ws.RangeUsed()!.AsTable();
            table.Theme = XLTableTheme.TableStyleMedium6;
        }
        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents();
    }

    private static void WriteHourlyTrafficSheet(XLWorkbook workbook, AnalysisReport report, AppLanguage lang)
    {
        var ws = workbook.Worksheets.Add(Loc.T("report.sheet.hourlyTraffic", lang));
        var headers = new[]
        {
            Loc.T("col.time", lang), Loc.T("col.requests", lang), Loc.T("report.client4xx", lang),
            Loc.T("col.errorRatePct", lang), Loc.T("col.avg", lang),
        };
        WriteHeaderRow(ws, headers);

        var row = 2;
        foreach (var b in report.HourlyTraffic)
        {
            ws.Cell(row, 1).Value = b.BucketStartUtc;
            ws.Cell(row, 1).Style.DateFormat.Format = "yyyy-MM-dd HH:mm";
            ws.Cell(row, 2).Value = b.RequestCount;
            ws.Cell(row, 3).Value = b.ErrorCount;
            ws.Cell(row, 4).Value = Math.Round(b.ErrorRatePercent, 2);
            ws.Cell(row, 5).Value = Math.Round(b.AvgResponseTimeMs, 1);
            row++;
        }

        if (report.HourlyTraffic.Count > 0)
        {
            var table = ws.RangeUsed()!.AsTable();
            table.Theme = XLTableTheme.TableStyleMedium2;
        }
        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents();
    }

    private static void WriteAnomaliesSheet(XLWorkbook workbook, AnalysisReport report, AppLanguage lang)
    {
        var ws = workbook.Worksheets.Add(Loc.T("report.sheet.anomalies", lang));
        var headers = new[]
        {
            Loc.T("col.time", lang), Loc.T("col.type", lang), Loc.T("col.observed", lang),
            Loc.T("col.expected", lang), Loc.T("col.deviationScore", lang), Loc.T("col.description", lang),
        };
        WriteHeaderRow(ws, headers);

        var row = 2;
        foreach (var a in report.Anomalies)
        {
            ws.Cell(row, 1).Value = a.BucketStartUtc;
            ws.Cell(row, 1).Style.DateFormat.Format = "yyyy-MM-dd HH:mm";
            ws.Cell(row, 2).Value = a.Type switch
            {
                AnomalyType.TrafficSpike => Loc.T("anomaly.trafficSpike", lang),
                AnomalyType.TrafficDrop => Loc.T("anomaly.trafficDrop", lang),
                AnomalyType.ErrorRateSpike => Loc.T("anomaly.errorSpike", lang),
                _ => a.Type.ToString(),
            };
            ws.Cell(row, 3).Value = Math.Round(a.ObservedValue, 2);
            ws.Cell(row, 4).Value = Math.Round(a.ExpectedValue, 2);
            ws.Cell(row, 5).Value = Math.Round(a.DeviationScore, 2);
            ws.Cell(row, 6).Value = a.Description;
            row++;
        }

        if (report.Anomalies.Count > 0)
        {
            var table = ws.RangeUsed()!.AsTable();
            table.Theme = XLTableTheme.TableStyleMedium6;
        }
        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents();
    }

    private static void WriteGlossarySheet(XLWorkbook workbook, AppLanguage lang)
    {
        var ws = workbook.Worksheets.Add(Loc.T("report.sheet.glossary", lang));
        WriteHeaderRow(ws, [Loc.T("col.type", lang), Loc.T("col.description", lang)]);

        string[] terms = ["glossary.p50", "glossary.p90", "glossary.p95", "glossary.p99",
            "glossary.errorRate", "glossary.anomaly", "glossary.trend", "glossary.problemScore", "glossary.endpoint"];

        var row = 2;
        foreach (var term in terms)
        {
            ws.Cell(row, 1).Value = Loc.T($"{term}.term", lang);
            ws.Cell(row, 1).Style.Font.SetBold();
            ws.Cell(row, 2).Value = Loc.T($"{term}.def", lang);
            ws.Cell(row, 2).Style.Alignment.SetWrapText(true);
            row++;
        }

        ws.Column(1).Width = 22;
        ws.Column(2).Width = 90;
        ws.SheetView.FreezeRows(1);
    }

    private static void WriteHeaderRow(IXLWorksheet ws, string[] headers)
    {
        for (int i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];
        ws.Row(1).Style.Font.SetBold();
    }
}
