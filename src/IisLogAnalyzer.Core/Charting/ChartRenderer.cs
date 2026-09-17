using IisLogAnalyzer.Core.Analysis;
using IisLogAnalyzer.Core.Localization;
using ScottPlot;

namespace IisLogAnalyzer.Core.Charting;

/// <summary>
/// Renders the same hourly-traffic and response-time charts shown in the app's
/// dashboard to static PNG bytes, so the Excel/PDF reports carry visuals instead
/// of tables alone. Uses ScottPlot's headless renderer (no window/control needed).
/// </summary>
public static class ChartRenderer
{
    public static byte[] TrafficChartPng(AnalysisReport report, AppLanguage language, int width = 900, int height = 380)
    {
        var plot = new Plot();
        plot.Title(Loc.T("chart.traffic.title", language));
        plot.YLabel(Loc.T("chart.traffic.ylabel", language));

        if (report.HourlyTraffic.Count > 0)
        {
            var xs = report.HourlyTraffic.Select(b => b.BucketStartUtc.ToOADate()).ToArray();
            var ys = report.HourlyTraffic.Select(b => (double)b.RequestCount).ToArray();
            var scatter = plot.Add.Scatter(xs, ys);
            scatter.Color = Colors.Blue.WithAlpha(0.75);
            scatter.LineWidth = 2;
            scatter.MarkerSize = 3;
            scatter.LegendText = Loc.T("chart.traffic.legend", language);

            var anomalyBuckets = report.Anomalies
                .Where(a => a.Type is AnomalyType.TrafficSpike or AnomalyType.TrafficDrop)
                .Select(a => a.BucketStartUtc)
                .ToHashSet();

            if (anomalyBuckets.Count > 0)
            {
                var pairs = report.HourlyTraffic.Where(b => anomalyBuckets.Contains(b.BucketStartUtc)).ToArray();
                var axs = pairs.Select(b => b.BucketStartUtc.ToOADate()).ToArray();
                var ays = pairs.Select(b => (double)b.RequestCount).ToArray();
                var markers = plot.Add.ScatterPoints(axs, ays);
                markers.Color = Colors.Red;
                markers.MarkerSize = 9;
                markers.LegendText = Loc.T("chart.traffic.anomalyLegend", language);
                plot.ShowLegend();
            }

            plot.Axes.DateTimeTicksBottom();
        }

        return plot.GetImage(width, height).GetImageBytes();
    }

    public static byte[] ResponseTimeChartPng(AnalysisReport report, AppLanguage language, int width = 900, int height = 380)
    {
        var plot = new Plot();
        plot.Title(Loc.T("chart.responseTime.title", language));
        plot.YLabel(Loc.T("chart.responseTime.ylabel", language));

        if (report.HourlyTraffic.Count > 0)
        {
            var xs = report.HourlyTraffic.Select(b => b.BucketStartUtc.ToOADate()).ToArray();
            var avg = report.HourlyTraffic.Select(b => b.AvgResponseTimeMs).ToArray();

            var avgScatter = plot.Add.Scatter(xs, avg);
            avgScatter.Color = Colors.Blue.WithAlpha(0.8);
            avgScatter.LineWidth = 2;
            avgScatter.MarkerSize = 3;
            avgScatter.LegendText = Loc.T("chart.responseTime.avglegend", language);

            plot.Axes.DateTimeTicksBottom();
            plot.ShowLegend();
        }

        return plot.GetImage(width, height).GetImageBytes();
    }
}
