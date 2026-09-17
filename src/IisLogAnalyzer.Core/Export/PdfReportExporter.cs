using IisLogAnalyzer.Core.Analysis;
using IisLogAnalyzer.Core.Charting;
using IisLogAnalyzer.Core.Localization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace IisLogAnalyzer.Core.Export;

public static class PdfReportExporter
{
    public static void Export(AnalysisReport report, string filePath, string? sourceDescription = null, AppLanguage language = AppLanguage.Turkish)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        // The app targets Windows desktops where Segoe UI always ships with the OS,
        // so we let QuestPDF resolve it from installed system fonts.
        QuestPDF.Settings.UseSystemFonts = true;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Segoe UI"));

                page.Header().Element(c => ComposeHeader(c, report, sourceDescription, language));
                page.Content().Element(c => ComposeContent(c, report, language));
                page.Footer().AlignCenter().Text(t =>
                {
                    t.CurrentPageNumber();
                    t.Span(" / ");
                    t.TotalPages();
                });
            });
        });

        document.GeneratePdf(filePath);
    }

    private static void ComposeHeader(IContainer container, AnalysisReport report, string? sourceDescription, AppLanguage lang)
    {
        container.Column(col =>
        {
            col.Item().Text(Loc.T("report.title", lang)).FontSize(20).Bold().FontColor(Colors.Blue.Darken2);
            col.Item().PaddingTop(2).Text(t =>
            {
                t.Span($"{Loc.T("report.generatedAt", lang)}: ").SemiBold();
                t.Span($"{report.GeneratedAtUtc:yyyy-MM-dd HH:mm}   ");
                t.Span($"{Loc.T("report.period", lang)}: ").SemiBold();
                t.Span(report.PeriodStartUtc is null
                    ? "-"
                    : $"{report.PeriodStartUtc:yyyy-MM-dd HH:mm} - {report.PeriodEndUtc:yyyy-MM-dd HH:mm}");
            });
            if (!string.IsNullOrWhiteSpace(sourceDescription))
                col.Item().Text($"{Loc.T("report.source", lang)}: {sourceDescription}").FontSize(8).FontColor(Colors.Grey.Darken1);
            col.Item().PaddingTop(6).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
        });
    }

    private static void ComposeContent(IContainer container, AnalysisReport report, AppLanguage lang)
    {
        container.PaddingTop(10).Column(col =>
        {
            col.Spacing(14);
            col.Item().Element(c => ComposeSummaryCards(c, report, lang));
            col.Item().Element(c => ComposeChartsSection(c, report, lang));
            col.Item().Element(c => ComposeSection(c, Loc.T("report.section.problematic", lang), t => ComposeProblematicTable(t, report, lang)));
            col.Item().Element(c => ComposeSection(c, Loc.T("report.section.slow", lang), t => ComposeSlowTable(t, report, lang)));
            col.Item().Element(c => ComposeSection(c, Loc.T("report.section.errors", lang), t => ComposeErrorTable(t, report, lang)));
            if (report.DegradingEndpoints.Count > 0)
                col.Item().Element(c => ComposeSection(c, Loc.T("report.section.degrading", lang), t => ComposeDegradingTable(t, report, lang)));
            if (report.Anomalies.Count > 0)
                col.Item().Element(c => ComposeSection(c, Loc.T("report.section.anomalies", lang), t => ComposeAnomalyTable(t, report, lang)));
            col.Item().Element(c => ComposeSection(c, Loc.T("report.section.glossary", lang), t => ComposeGlossary(t, lang)));
        });
    }

    private static void ComposeSection(IContainer container, string title, Action<IContainer> bodyRenderer)
    {
        container.Column(col =>
        {
            col.Item().Text(title).FontSize(13).Bold();
            col.Item().PaddingTop(4).Element(bodyRenderer);
        });
    }

    private static void ComposeSummaryCards(IContainer container, AnalysisReport report, AppLanguage lang)
    {
        var labels = new[]
        {
            Loc.T("card.totalRequests.title", lang), Loc.T("card.errorRate.title", lang),
            Loc.T("card.avgResponse.title", lang), Loc.T("card.anomalyCount.title", lang),
        };
        var values = new[]
        {
            report.TotalRequests.ToString("N0", lang.ToCultureInfo()),
            $"%{report.OverallErrorRatePercent.ToString("F2", lang.ToCultureInfo())}",
            $"{report.AvgResponseTimeMs.ToString("F0", lang.ToCultureInfo())} ms",
            report.Anomalies.Count.ToString(),
        };

        container.Row(row =>
        {
            for (int i = 0; i < labels.Length; i++)
            {
                var label = labels[i];
                var value = values[i];
                row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(c =>
                {
                    c.Item().Text(label).FontSize(8).FontColor(Colors.Grey.Darken1);
                    c.Item().PaddingTop(2).Text(value).FontSize(16).Bold();
                });
            }
        });
    }

    private static void ComposeChartsSection(IContainer container, AnalysisReport report, AppLanguage lang)
    {
        container.Column(col =>
        {
            col.Spacing(8);
            col.Item().Image(ChartRenderer.TrafficChartPng(report, lang, 1000, 320)).FitWidth();
            col.Item().Image(ChartRenderer.ResponseTimeChartPng(report, lang, 1000, 320)).FitWidth();
        });
    }

    private static void ComposeProblematicTable(IContainer container, AnalysisReport report, AppLanguage lang)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(1);
                c.RelativeColumn(1.2f);
                c.RelativeColumn(3.5f);
                c.RelativeColumn(1.3f);
                c.RelativeColumn(1.3f);
                c.RelativeColumn(1.2f);
                c.RelativeColumn(1.3f);
            });

            HeaderRow(table, Loc.T("col.rank", lang), Loc.T("col.method", lang), Loc.T("col.path", lang),
                Loc.T("col.problemScore", lang), Loc.T("col.errorRatePct", lang), Loc.T("col.p95", lang), Loc.T("col.trend", lang));

            var rank = 1;
            foreach (var e in report.TopProblematicEndpoints)
            {
                var isHighRisk = e.ProblemScore >= 60;
                DataRow(table, isHighRisk, rank++.ToString(), e.Method, e.NormalizedPath, e.ProblemScore.ToString("F1"),
                    $"{e.ErrorRatePercent:F1}%", e.P95ResponseTimeMs.ToString("N0"), e.IsDegradingTrend ? Loc.T("trend.degrading", lang) : Loc.T("trend.stable", lang));
            }

            if (report.TopProblematicEndpoints.Count == 0)
                table.Cell().ColumnSpan(7).Padding(6).Text(Loc.T("report.noProblematic", lang)).Italic();
        });
    }

    private static void ComposeSlowTable(IContainer container, AnalysisReport report, AppLanguage lang)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(1.2f);
                c.RelativeColumn(3.5f);
                c.RelativeColumn(1.3f);
                c.RelativeColumn(1.3f);
                c.RelativeColumn(1.3f);
                c.RelativeColumn(1.3f);
            });

            HeaderRow(table, Loc.T("col.method", lang), Loc.T("col.path", lang), Loc.T("col.p50", lang),
                Loc.T("col.p95", lang), Loc.T("col.p99", lang), Loc.T("col.requests", lang));

            foreach (var e in report.TopSlowEndpoints)
            {
                DataRow(table, false, e.Method, e.NormalizedPath, e.P50ResponseTimeMs.ToString("N0"),
                    e.P95ResponseTimeMs.ToString("N0"), e.P99ResponseTimeMs.ToString("N0"), e.RequestCount.ToString("N0"));
            }
        });
    }

    private static void ComposeErrorTable(IContainer container, AnalysisReport report, AppLanguage lang)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(1.2f);
                c.RelativeColumn(3.5f);
                c.RelativeColumn(1.3f);
                c.RelativeColumn(1.3f);
                c.RelativeColumn(1.3f);
                c.RelativeColumn(1.3f);
            });

            HeaderRow(table, Loc.T("col.method", lang), Loc.T("col.path", lang), Loc.T("col.errorRatePct", lang),
                Loc.T("col.err4xx", lang), Loc.T("col.err5xx", lang), Loc.T("col.requests", lang));

            foreach (var e in report.TopErrorEndpoints)
            {
                DataRow(table, e.ServerErrorCount > 0, e.Method, e.NormalizedPath, $"{e.ErrorRatePercent:F1}%",
                    e.ClientErrorCount.ToString("N0"), e.ServerErrorCount.ToString("N0"), e.RequestCount.ToString("N0"));
            }
        });
    }

    private static void ComposeDegradingTable(IContainer container, AnalysisReport report, AppLanguage lang)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(1.2f);
                c.RelativeColumn(4f);
                c.RelativeColumn(1.6f);
                c.RelativeColumn(1.6f);
            });

            HeaderRow(table, Loc.T("col.method", lang), Loc.T("col.path", lang), Loc.T("col.slope", lang), Loc.T("col.requests", lang));

            foreach (var e in report.DegradingEndpoints)
                DataRow(table, true, e.Method, e.NormalizedPath, $"+{e.TrendSlopeMsPerHour:F1}", e.RequestCount.ToString("N0"));
        });
    }

    private static void ComposeAnomalyTable(IContainer container, AnalysisReport report, AppLanguage lang)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(1.6f);
                c.RelativeColumn(1.6f);
                c.RelativeColumn(5f);
            });

            HeaderRow(table, Loc.T("col.time", lang), Loc.T("col.type", lang), Loc.T("col.description", lang));

            foreach (var a in report.Anomalies)
            {
                var typeLabel = a.Type switch
                {
                    AnomalyType.TrafficSpike => Loc.T("anomaly.trafficSpike", lang),
                    AnomalyType.TrafficDrop => Loc.T("anomaly.trafficDrop", lang),
                    AnomalyType.ErrorRateSpike => Loc.T("anomaly.errorSpike", lang),
                    _ => a.Type.ToString(),
                };
                DataRow(table, a.Type == AnomalyType.ErrorRateSpike, a.BucketStartUtc.ToString("MM-dd HH:mm"), typeLabel, a.Description);
            }
        });
    }

    private static void ComposeGlossary(IContainer container, AppLanguage lang)
    {
        string[] terms =
        [
            "glossary.p50", "glossary.p90", "glossary.p95", "glossary.p99",
            "glossary.errorRate", "glossary.anomaly", "glossary.trend", "glossary.problemScore", "glossary.endpoint",
        ];

        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(2f);
                c.RelativeColumn(7f);
            });

            foreach (var term in terms)
            {
                table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(Loc.T($"{term}.term", lang)).Bold().FontSize(8);
                table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(Loc.T($"{term}.def", lang)).FontSize(8);
            }
        });
    }

    private static void HeaderRow(TableDescriptor table, params string[] headers)
    {
        foreach (var h in headers)
            table.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text(h).Bold().FontSize(8);
    }

    private static void DataRow(TableDescriptor table, bool highlight, params string[] values)
    {
        var bg = highlight ? Colors.Red.Lighten4 : Colors.White;
        foreach (var v in values)
            table.Cell().Background(bg).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4).Text(v).FontSize(8);
    }
}
