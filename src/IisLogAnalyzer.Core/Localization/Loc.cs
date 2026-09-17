namespace IisLogAnalyzer.Core.Localization;

/// <summary>
/// Single source of truth for every user-facing string, shared by the WinForms UI
/// and the Excel/PDF exporters so the desktop app and the reports it produces are
/// always in the same language.
/// </summary>
public static class Loc
{
    private static readonly Dictionary<string, (string Tr, string En)> Map = new()
    {
        // App shell -----------------------------------------------------------------
        ["app.title"] = ("IIS Log Analyzer", "IIS Log Analyzer"),

        // Toolbar ---------------------------------------------------------------------
        ["toolbar.openFiles"] = ("Log Dosyaları Aç...", "Open Log Files..."),
        ["toolbar.openFolder"] = ("Log Klasörü Aç...", "Open Log Folder..."),
        ["toolbar.analyze"] = ("Analiz Et", "Analyze"),
        ["toolbar.exportExcel"] = ("Excel'e Aktar", "Export to Excel"),
        ["toolbar.exportPdf"] = ("PDF'e Aktar", "Export to PDF"),
        ["toolbar.language"] = ("Dil", "Language"),

        // File selection ----------------------------------------------------------------
        ["files.none"] = ("Dosya seçilmedi.", "No file selected."),
        ["files.multiple"] = ("{0} dosya seçildi", "{0} files selected"),
        ["dialog.openFiles.filter"] = ("IIS Log Dosyaları (*.log)|*.log|Tüm Dosyalar (*.*)|*.*", "IIS Log Files (*.log)|*.log|All Files (*.*)|*.*"),
        ["dialog.openFiles.title"] = ("IIS Log Dosyalarını Seç", "Select IIS Log Files"),
        ["dialog.openFolder.description"] = ("IIS Log Klasörünü Seç", "Select IIS Log Folder"),
        ["dialog.noLogsInFolder"] = ("Seçilen klasörde .log uzantılı dosya bulunamadı.", "No .log files were found in the selected folder."),
        ["dialog.info"] = ("Bilgi", "Information"),
        ["dialog.error"] = ("Hata", "Error"),
        ["dialog.analyzeError"] = ("Analiz sırasında bir hata oluştu:\n{0}", "An error occurred during analysis:\n{0}"),
        ["dialog.excelExportError"] = ("Excel dışa aktarımı başarısız oldu:\n{0}", "Excel export failed:\n{0}"),
        ["dialog.pdfExportError"] = ("PDF dışa aktarımı başarısız oldu:\n{0}", "PDF export failed:\n{0}"),
        ["dialog.saveExcel.filter"] = ("Excel Dosyası (*.xlsx)|*.xlsx", "Excel File (*.xlsx)|*.xlsx"),
        ["dialog.savePdf.filter"] = ("PDF Dosyası (*.pdf)|*.pdf", "PDF File (*.pdf)|*.pdf"),
        ["dialog.reportReady.title"] = ("Rapor Hazır", "Report Ready"),
        ["dialog.reportReady.message"] = ("Rapor kaydedildi. Şimdi açılsın mı?", "The report has been saved. Open it now?"),

        // Status bar ----------------------------------------------------------------------
        ["status.ready"] = ("Hazır.", "Ready."),
        ["status.filesReady"] = ("{0} dosya yüklenmeye hazır.", "{0} file(s) ready to analyze."),
        ["status.processing"] = ("İşleniyor... {0:N0} satır okundu", "Processing... {0:N0} lines read"),
        ["status.analysisComplete"] = ("Analiz tamamlandı: {0:N0} istek, {1:N0} endpoint, {2:F1} sn.", "Analysis complete: {0:N0} requests, {1:N0} endpoints, {2:F1}s."),
        ["status.analysisFailed"] = ("Analiz başarısız oldu.", "Analysis failed."),
        ["status.excelSaved"] = ("Excel raporu kaydedildi: {0}", "Excel report saved: {0}"),
        ["status.pdfSaved"] = ("PDF raporu kaydedildi: {0}", "PDF report saved: {0}"),

        // Tabs ------------------------------------------------------------------------------
        ["tab.summary"] = ("Özet", "Summary"),
        ["tab.problematic"] = ("Sorunlu Endpoint'ler", "Problem Endpoints"),
        ["tab.allEndpoints"] = ("Tüm Endpoint'ler", "All Endpoints"),
        ["tab.degrading"] = ("Yavaşlayan Trendler", "Degrading Trends"),
        ["tab.anomalies"] = ("Anomaliler", "Anomalies"),
        ["tab.rawData"] = ("Ham Veri", "Raw Data"),

        // Summary cards -----------------------------------------------------------------------
        ["card.totalRequests.title"] = ("Toplam İstek", "Total Requests"),
        ["card.totalRequests.tooltip"] = ("Analiz edilen dönemdeki toplam HTTP istek sayısı.", "Total number of HTTP requests in the analyzed period."),
        ["card.errorRate.title"] = ("Genel Hata Oranı", "Overall Error Rate"),
        ["card.errorRate.tooltip"] = ("4xx ve 5xx durum kodlu isteklerin toplam isteklere oranı.", "Share of requests that returned a 4xx or 5xx status code."),
        ["card.avgResponse.title"] = ("Ort. Yanıt Süresi", "Avg. Response Time"),
        ["card.avgResponse.tooltip"] = ("Tüm isteklerin ortalama sunucu yanıt süresi.", "Average server response time across all requests."),
        ["card.anomalyCount.title"] = ("Anomali Sayısı", "Anomaly Count"),
        ["card.anomalyCount.tooltip"] = ("Trafik hacminin veya hata oranının istatistiksel ortalamadan belirgin şekilde saptığı saat sayısı.", "Number of hours where traffic volume or error rate deviated significantly from the statistical average."),
        ["card.degradingCount.title"] = ("Yavaşlayan Endpoint", "Degrading Endpoints"),
        ["card.degradingCount.tooltip"] = ("Yanıt süresi zaman içinde belirgin şekilde artan endpoint sayısı.", "Number of endpoints whose response time is clearly increasing over time."),

        // Charts ------------------------------------------------------------------------------
        ["chart.traffic.title"] = ("Saatlik İstek Trafiği", "Hourly Request Traffic"),
        ["chart.traffic.ylabel"] = ("İstek Sayısı", "Request Count"),
        ["chart.responseTime.title"] = ("Saatlik Ortalama Yanıt Süresi", "Hourly Average Response Time"),
        ["chart.responseTime.ylabel"] = ("Yanıt Süresi (ms)", "Response Time (ms)"),
        ["chart.responseTime.p95legend"] = ("P95", "P95"),
        ["chart.responseTime.avglegend"] = ("Ortalama", "Average"),
        ["chart.traffic.legend"] = ("İstek Sayısı", "Request Count"),
        ["chart.traffic.anomalyLegend"] = ("Anomali", "Anomaly"),

        // Grid columns --------------------------------------------------------------------------
        ["col.rank"] = ("#", "#"),
        ["col.rank.tooltip"] = ("Problem skoruna göre öncelik sırası.", "Priority order based on the problem score."),
        ["col.method"] = ("Method", "Method"),
        ["col.method.tooltip"] = ("HTTP metodu (GET, POST, vb.).", "HTTP method (GET, POST, etc.)."),
        ["col.path"] = ("Path", "Path"),
        ["col.path.tooltip"] = ("Normalize edilmiş istek yolu; sayısal ID'ler {id} olarak birleştirilir.", "Normalized request path; numeric IDs are collapsed to {id}."),
        ["col.problemScore"] = ("Problem Skoru", "Problem Score"),
        ["col.problemScore.tooltip"] = ("Hata oranı (%55), P95 gecikmesi (%30) ve yavaşlama trendinin (%15) ağırlıklı birleşimiyle hesaplanan 0-100 arası öncelik puanı.", "A 0-100 priority score combining error rate (55%), P95 latency (30%) and a degrading-trend flag (15%)."),
        ["col.errorRatePct"] = ("Hata Oranı (%)", "Error Rate (%)"),
        ["col.errorRatePct.tooltip"] = ("4xx ve 5xx durum kodlu isteklerin bu endpoint'teki oranı.", "Share of this endpoint's requests that returned a 4xx or 5xx status code."),
        ["col.p95"] = ("P95 (ms)", "P95 (ms)"),
        ["col.p95.tooltip"] = ("İsteklerin %95'i bu süreden kısa sürede tamamlandı.", "95% of requests completed faster than this time."),
        ["col.trend"] = ("Trend", "Trend"),
        ["col.trend.tooltip"] = ("Yanıt süresi zaman içinde belirgin şekilde artıyorsa işaretlenir.", "Flagged when response time is clearly increasing over time."),
        ["col.requests"] = ("İstek Sayısı", "Request Count"),
        ["col.requests.tooltip"] = ("Bu endpoint için gözlenen toplam istek sayısı.", "Total number of requests observed for this endpoint."),
        ["col.avg"] = ("Ort. (ms)", "Avg (ms)"),
        ["col.avg.tooltip"] = ("Bu endpoint'e ait tüm isteklerin ortalama yanıt süresi.", "Average response time across all of this endpoint's requests."),
        ["col.p50"] = ("P50 (ms)", "P50 (ms)"),
        ["col.p50.tooltip"] = ("Medyan: İsteklerin yarısı bu süreden kısa sürede tamamlandı.", "Median: half of all requests completed faster than this time."),
        ["col.p90"] = ("P90 (ms)", "P90 (ms)"),
        ["col.p90.tooltip"] = ("İsteklerin %90'ı bu süreden kısa sürede tamamlandı.", "90% of requests completed faster than this time."),
        ["col.p99"] = ("P99 (ms)", "P99 (ms)"),
        ["col.p99.tooltip"] = ("İsteklerin %99'u bu süreden kısa sürede tamamlandı; en yavaş %1'lik dilimi temsil eder.", "99% of requests completed faster than this time; represents the slowest 1%."),
        ["col.err4xx"] = ("4xx", "4xx"),
        ["col.err4xx.tooltip"] = ("İstemci hataları (ör. 404 Bulunamadı, 400 Geçersiz İstek).", "Client errors (e.g. 404 Not Found, 400 Bad Request)."),
        ["col.err5xx"] = ("5xx", "5xx"),
        ["col.err5xx.tooltip"] = ("Sunucu hataları (ör. 500 İç Sunucu Hatası, 503 Servis Kullanılamıyor).", "Server errors (e.g. 500 Internal Server Error, 503 Service Unavailable)."),
        ["col.errorRateShort"] = ("Hata %", "Error %"),
        ["col.score"] = ("Skor", "Score"),
        ["col.slope"] = ("Trend (ms/saat)", "Trend (ms/hour)"),
        ["col.slope.tooltip"] = ("Saatlik ortalama yanıt süresine uygulanan lineer regresyonun eğimi.", "Slope of a linear regression fit to hourly average response times."),
        ["col.time"] = ("Saat (UTC)", "Time (UTC)"),
        ["col.type"] = ("Tür", "Type"),
        ["col.observed"] = ("Gözlenen", "Observed"),
        ["col.observed.tooltip"] = ("O saatte gerçekleşen gözlenen değer.", "The value actually observed in that hour."),
        ["col.expected"] = ("Beklenen", "Expected"),
        ["col.expected.tooltip"] = ("Dönemin istatistiksel ortalamasına göre beklenen değer.", "The value expected based on the period's statistical average."),
        ["col.deviationScore"] = ("Sapma (z)", "Deviation (z)"),
        ["col.deviationScore.tooltip"] = ("Standart sapma cinsinden ortalamadan uzaklık (z-skoru). |z| ≥ 2.5 anomali eşiğidir.", "Distance from the average in standard deviations (z-score). |z| ≥ 2.5 is the anomaly threshold."),
        ["col.description"] = ("Açıklama", "Description"),
        ["col.query"] = ("Sorgu Dizesi", "Query String"),
        ["col.query.tooltip"] = ("URL'nin ? işaretinden sonraki sorgu parametreleri.", "The query parameters after the ? in the URL."),
        ["col.status"] = ("Durum Kodu", "Status Code"),
        ["col.status.tooltip"] = ("HTTP yanıt durum kodu (ör. 200, 404, 500).", "HTTP response status code (e.g. 200, 404, 500)."),
        ["col.subStatus"] = ("Alt Durum", "Substatus"),
        ["col.subStatus.tooltip"] = ("IIS'e özgü alt durum kodu (ör. 401.2), sorunu daha ayrıntılı belirtir.", "IIS-specific substatus code (e.g. 401.2) that narrows down the cause."),
        ["col.timeTakenMs"] = ("Yanıt Süresi (ms)", "Response Time (ms)"),
        ["col.clientIp"] = ("İstemci IP", "Client IP"),
        ["col.clientIp.tooltip"] = ("İsteği gönderen istemcinin IP adresi.", "The IP address of the client that made the request."),
        ["col.port"] = ("Port", "Port"),
        ["col.username"] = ("Kullanıcı", "Username"),
        ["col.username.tooltip"] = ("Kimliği doğrulanmış istek için kullanıcı adı (varsa).", "Authenticated username for the request, if any."),
        ["col.userAgent"] = ("Tarayıcı / İstemci (User-Agent)", "Browser / Client (User-Agent)"),
        ["col.referer"] = ("Yönlendiren (Referer)", "Referer"),

        // Raw data tab: filters -------------------------------------------------------------------
        ["rawdata.filter.path"] = ("Yol İçeriyor", "Path Contains"),
        ["rawdata.filter.pathPlaceholder"] = ("ör. /api/orders", "e.g. /api/orders"),
        ["rawdata.filter.method"] = ("Method", "Method"),
        ["rawdata.filter.allMethods"] = ("Tümü", "All"),
        ["rawdata.filter.status"] = ("Durum Sınıfı", "Status Class"),
        ["rawdata.filter.allStatuses"] = ("Tümü", "All"),
        ["rawdata.filter.minResponseTime"] = ("Min. Yanıt Süresi (ms)", "Min. Response Time (ms)"),
        ["rawdata.filter.clientIp"] = ("İstemci IP İçeriyor", "Client IP Contains"),
        ["rawdata.filter.dateFrom"] = ("Başlangıç (UTC)", "From (UTC)"),
        ["rawdata.filter.dateTo"] = ("Bitiş (UTC)", "To (UTC)"),
        ["rawdata.filter.apply"] = ("Filtrele", "Apply Filter"),
        ["rawdata.filter.clear"] = ("Temizle", "Clear"),
        ["rawdata.showingCount"] = ("{0:N0} / {1:N0} kayıt gösteriliyor", "Showing {0:N0} of {1:N0} records"),
        ["rawdata.noData"] = ("Önce bir log analizi çalıştırın.", "Run a log analysis first."),

        // Anomaly / trend labels ------------------------------------------------------------------
        ["anomaly.trafficSpike"] = ("Trafik Sıçraması", "Traffic Spike"),
        ["anomaly.trafficDrop"] = ("Trafik Düşüşü", "Traffic Drop"),
        ["anomaly.errorSpike"] = ("Hata Oranı Sıçraması", "Error Rate Spike"),
        ["trend.degrading"] = ("▲ Yavaşlıyor", "▲ Degrading"),
        ["trend.stable"] = ("-", "-"),

        // Report (Excel / PDF) shared -----------------------------------------------------------
        ["report.title"] = ("IIS Log Analiz Raporu", "IIS Log Analysis Report"),
        ["report.generatedAt"] = ("Oluşturulma (UTC)", "Generated (UTC)"),
        ["report.period"] = ("Dönem (UTC)", "Period (UTC)"),
        ["report.source"] = ("Kaynak", "Source"),
        ["report.periodStart"] = ("Dönem Başlangıcı (UTC)", "Period Start (UTC)"),
        ["report.periodEnd"] = ("Dönem Bitişi (UTC)", "Period End (UTC)"),
        ["report.totalRequests"] = ("Toplam İstek", "Total Requests"),
        ["report.client4xx"] = ("4xx Hata Sayısı", "4xx Error Count"),
        ["report.server5xx"] = ("5xx Hata Sayısı", "5xx Error Count"),
        ["report.overallErrorRate"] = ("Genel Hata Oranı (%)", "Overall Error Rate (%)"),
        ["report.avgResponseTime"] = ("Ortalama Yanıt Süresi (ms)", "Average Response Time (ms)"),
        ["report.distinctEndpoints"] = ("Farklı Endpoint Sayısı", "Distinct Endpoints"),
        ["report.anomalyCount"] = ("Tespit Edilen Anomali Sayısı", "Detected Anomalies"),
        ["report.degradingCount"] = ("Yavaşlama Trendi Gösteren Endpoint", "Endpoints With a Degrading Trend"),
        ["report.parseWarnings"] = ("Ayrıştırma Uyarısı", "Parse Warnings"),

        ["report.sheet.summary"] = ("Özet", "Summary"),
        ["report.sheet.allEndpoints"] = ("Tüm Endpoint'ler", "All Endpoints"),
        ["report.sheet.problematic"] = ("Sorunlu Endpoint'ler", "Problem Endpoints"),
        ["report.sheet.hourlyTraffic"] = ("Saatlik Trafik", "Hourly Traffic"),
        ["report.sheet.anomalies"] = ("Anomaliler", "Anomalies"),
        ["report.sheet.glossary"] = ("Terimler Sözlüğü", "Glossary"),

        ["report.section.problematic"] = ("En Sorunlu Endpoint'ler", "Most Problematic Endpoints"),
        ["report.section.slow"] = ("En Yavaş Endpoint'ler (P95)", "Slowest Endpoints (P95)"),
        ["report.section.errors"] = ("En Çok Hata Veren Endpoint'ler", "Endpoints With the Most Errors"),
        ["report.section.degrading"] = ("Zamanla Yavaşlayan Endpoint'ler", "Endpoints Slowing Down Over Time"),
        ["report.section.anomalies"] = ("Trafik / Hata Anomalileri", "Traffic / Error Anomalies"),
        ["report.section.glossary"] = ("Terimler Sözlüğü", "Glossary"),
        ["report.noProblematic"] = ("Eşik değeri karşılayan endpoint bulunamadı.", "No endpoints met the reporting threshold."),
        ["report.executiveSummary"] = ("Yönetici Özeti", "Executive Summary"),

        // Glossary --------------------------------------------------------------------------------
        ["glossary.p50.term"] = ("P50 (Medyan)", "P50 (Median)"),
        ["glossary.p50.def"] = ("İsteklerin %50'si bu süreden kısa sürede tamamlanır.", "50% of requests complete faster than this time."),
        ["glossary.p90.term"] = ("P90", "P90"),
        ["glossary.p90.def"] = ("İsteklerin %90'ı bu süreden kısa sürede tamamlanır.", "90% of requests complete faster than this time."),
        ["glossary.p95.term"] = ("P95", "P95"),
        ["glossary.p95.def"] = ("İsteklerin %95'i bu süreden kısa sürede tamamlanır; sektörde tipik bir SLA ölçütüdür.", "95% of requests complete faster than this time; a common SLA benchmark."),
        ["glossary.p99.term"] = ("P99", "P99"),
        ["glossary.p99.def"] = ("İsteklerin %99'u bu süreden kısa sürede tamamlanır; en yavaş %1'lik dilimi temsil eder.", "99% of requests complete faster than this time; represents the slowest 1%."),
        ["glossary.errorRate.term"] = ("Hata Oranı", "Error Rate"),
        ["glossary.errorRate.def"] = ("4xx (istemci) ve 5xx (sunucu) durum kodlu isteklerin toplam isteklere oranı.", "The share of requests that returned a 4xx (client) or 5xx (server) status code."),
        ["glossary.anomaly.term"] = ("Anomali", "Anomaly"),
        ["glossary.anomaly.def"] = ("Saatlik trafik hacminin veya hata oranının istatistiksel ortalamadan belirgin şekilde (z-skoru ≥ 2.5) saptığı zaman dilimi.", "An hour where traffic volume or error rate deviates significantly (z-score ≥ 2.5) from the statistical average."),
        ["glossary.trend.term"] = ("Trend / Yavaşlama", "Trend / Degradation"),
        ["glossary.trend.def"] = ("Saatlik ortalama yanıt sürelerine uygulanan lineer regresyonun eğimi; belirgin pozitif eğim performans düşüşüne işaret eder.", "The slope of a linear regression fit to hourly average response times; a clearly positive slope indicates worsening performance."),
        ["glossary.problemScore.term"] = ("Problem Skoru", "Problem Score"),
        ["glossary.problemScore.def"] = ("Hata oranı (%55), P95 gecikmesi (%30) ve yavaşlama trendinin (%15) ağırlıklı birleşimiyle hesaplanan 0-100 arası öncelik puanı.", "A 0-100 priority score combining error rate (55%), P95 latency (30%) and a degrading-trend flag (15%)."),
        ["glossary.endpoint.term"] = ("Endpoint", "Endpoint"),
        ["glossary.endpoint.def"] = ("HTTP metodu ve normalize edilmiş yol (ör. /api/orders/{id}) ile tanımlanan istek grubu; sayısal ID'ler {id} olarak birleştirilir.", "A request group identified by HTTP method and normalized path (e.g. /api/orders/{id}); numeric IDs are collapsed to {id}."),
    };

    public static string T(string key, AppLanguage language)
    {
        return Map.TryGetValue(key, out var value) ? (language == AppLanguage.Turkish ? value.Tr : value.En) : key;
    }

    public static string F(string key, AppLanguage language, params object?[] args)
    {
        var format = T(key, language);
        return string.Format(language.ToCultureInfo(), format, args);
    }
}
