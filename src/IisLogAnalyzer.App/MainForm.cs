using System.Diagnostics;
using System.Globalization;
using IisLogAnalyzer.App.Controls;
using IisLogAnalyzer.Core.Analysis;
using IisLogAnalyzer.Core.Export;
using IisLogAnalyzer.Core.Localization;
using IisLogAnalyzer.Core.Models;
using IisLogAnalyzer.Core.Parsing;
using ScottPlot.WinForms;

namespace IisLogAnalyzer.App;

public sealed class MainForm : Form
{
    private static readonly Color BrandDark = Color.FromArgb(37, 47, 63);
    private static readonly Color BrandAccent = Color.FromArgb(0, 120, 212);
    private static readonly Color DangerColor = Color.FromArgb(217, 83, 79);
    private static readonly Color WarningColor = Color.FromArgb(240, 173, 78);

    // (Column Name, Header key, Tooltip key or null, fill width, numeric format or null for text)
    private static readonly (string Name, string HeaderKey, string? TooltipKey, int Width, string? Format)[] ProblematicColumns =
    [
        ("Rank", "col.rank", "col.rank.tooltip", 50, "N0"),
        ("Method", "col.method", "col.method.tooltip", 70, null),
        ("Path", "col.path", "col.path.tooltip", 320, null),
        ("Score", "col.problemScore", "col.problemScore.tooltip", 100, "N1"),
        ("ErrorRate", "col.errorRatePct", "col.errorRatePct.tooltip", 100, "N2"),
        ("P95", "col.p95", "col.p95.tooltip", 90, "N0"),
        ("Trend", "col.trend", "col.trend.tooltip", 90, null),
        ("Requests", "col.requests", "col.requests.tooltip", 100, "N0"),
    ];

    private static readonly (string Name, string HeaderKey, string? TooltipKey, int Width, string? Format)[] AllEndpointsColumns =
    [
        ("Method", "col.method", "col.method.tooltip", 70, null),
        ("Path", "col.path", "col.path.tooltip", 300, null),
        ("Requests", "col.requests", "col.requests.tooltip", 80, "N0"),
        ("Avg", "col.avg", "col.avg.tooltip", 90, "N0"),
        ("P50", "col.p50", "col.p50.tooltip", 80, "N0"),
        ("P90", "col.p90", "col.p90.tooltip", 80, "N0"),
        ("P95", "col.p95", "col.p95.tooltip", 80, "N0"),
        ("P99", "col.p99", "col.p99.tooltip", 80, "N0"),
        ("Err4xx", "col.err4xx", "col.err4xx.tooltip", 60, "N0"),
        ("Err5xx", "col.err5xx", "col.err5xx.tooltip", 60, "N0"),
        ("ErrorRate", "col.errorRateShort", "col.errorRatePct.tooltip", 80, "N2"),
        ("Score", "col.score", "col.problemScore.tooltip", 70, "N1"),
    ];

    private static readonly (string Name, string HeaderKey, string? TooltipKey, int Width, string? Format)[] DegradingColumns =
    [
        ("Method", "col.method", "col.method.tooltip", 70, null),
        ("Path", "col.path", "col.path.tooltip", 340, null),
        ("Slope", "col.slope", "col.slope.tooltip", 130, "+0.0;-0.0"),
        ("Requests", "col.requests", "col.requests.tooltip", 110, "N0"),
        ("P95", "col.p95", "col.p95.tooltip", 100, "N0"),
    ];

    private static readonly (string Name, string HeaderKey, string? TooltipKey, int Width, string? Format)[] AnomalyColumns =
    [
        ("Time", "col.time", null, 130, null),
        ("Type", "col.type", null, 140, null),
        ("Observed", "col.observed", "col.observed.tooltip", 90, "N1"),
        ("Expected", "col.expected", "col.expected.tooltip", 90, "N1"),
        ("Score", "col.deviationScore", "col.deviationScore.tooltip", 90, "N2"),
        ("Description", "col.description", null, 400, null),
    ];

    // (Column Name, Header key, Tooltip key or null, pixel width, unused for raw grid but kept for spec-reuse)
    private static readonly (string Name, string HeaderKey, string? TooltipKey, int Width, string? Format)[] RawDataColumns =
    [
        ("Time", "col.time", null, 150, null),
        ("Method", "col.method", "col.method.tooltip", 70, null),
        ("Path", "col.path", "col.path.tooltip", 260, null),
        ("Query", "col.query", "col.query.tooltip", 160, null),
        ("Status", "col.status", "col.status.tooltip", 80, "N0"),
        ("SubStatus", "col.subStatus", "col.subStatus.tooltip", 80, "N0"),
        ("TimeTaken", "col.timeTakenMs", null, 130, "N0"),
        ("ClientIp", "col.clientIp", "col.clientIp.tooltip", 120, null),
        ("Port", "col.port", null, 60, "N0"),
        ("Username", "col.username", "col.username.tooltip", 100, null),
        ("UserAgent", "col.userAgent", null, 240, null),
        ("Referer", "col.referer", null, 180, null),
    ];

    private readonly List<string> _logFiles = new();
    private readonly List<LogEntry> _rawEntries = new();
    private readonly ToolTip _toolTip = new() { AutoPopDelay = 15000, InitialDelay = 300, ReshowDelay = 100 };
    private readonly List<(Label TitleLabel, Label ValueLabel, string TitleKey, string TooltipKey)> _cards = new();
    private readonly List<(Label Label, string Key)> _rawFilterLabels = new();

    private LogEntry[] _filteredRawEntries = [];
    private string? _rawSortColumn;
    private bool _rawSortAscending = true;

    private AppLanguage _language = AppLanguage.Turkish;

    private Button _btnOpenFiles = null!;
    private Button _btnOpenFolder = null!;
    private Button _btnAnalyze = null!;
    private Button _btnExportExcel = null!;
    private Button _btnExportPdf = null!;
    private ComboBox _cmbLanguage = null!;
    private Label _lblFiles = null!;
    private Label _lblStatus = null!;
    private ProgressBar _progressBar = null!;
    private TabControl _tabControl = null!;
    private TabPage _tabSummary = null!, _tabProblematic = null!, _tabAllEndpoints = null!, _tabDegrading = null!, _tabAnomalies = null!, _tabRawData = null!;

    private FormsPlot _trafficPlot = null!;
    private FormsPlot _responseTimePlot = null!;

    private FastDataGridView _gridProblematic = null!;
    private FastDataGridView _gridAllEndpoints = null!;
    private FastDataGridView _gridDegrading = null!;
    private FastDataGridView _gridAnomalies = null!;
    private FastDataGridView _gridRawData = null!;

    private TextBox _txtPathFilter = null!;
    private ComboBox _cmbMethodFilter = null!;
    private ComboBox _cmbStatusFilter = null!;
    private TextBox _txtMinResponseTime = null!;
    private TextBox _txtClientIpFilter = null!;
    private DateTimePicker _dtpFrom = null!;
    private DateTimePicker _dtpTo = null!;
    private Button _btnApplyRawFilter = null!;
    private Button _btnClearRawFilter = null!;
    private Label _lblRawCount = null!;

    private AnalysisReport? _currentReport;
    private bool _isBusy;
    private double _lastAnalysisElapsedSeconds;

    public MainForm()
    {
        StartPosition = FormStartPosition.CenterScreen;
        Width = 1320;
        Height = 860;
        MinimumSize = new Size(1100, 720);
        Font = new Font("Segoe UI", 9F);
        BackColor = Color.FromArgb(240, 242, 245);

        BuildLayout();
        ApplyLanguage();
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1,
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        Controls.Add(root);

        root.Controls.Add(BuildToolbar(), 0, 0);
        root.Controls.Add(BuildTabs(), 0, 1);
        root.Controls.Add(BuildStatusBar(), 0, 2);
    }

    private Control BuildToolbar()
    {
        var bar = new Panel { Dock = DockStyle.Fill, BackColor = BrandDark, Padding = new Padding(12, 8, 12, 8) };

        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = false,
        };
        bar.Controls.Add(flow);

        _btnOpenFiles = CreateToolbarButton();
        _btnOpenFiles.Click += OnOpenFilesClick;
        _btnOpenFolder = CreateToolbarButton();
        _btnOpenFolder.Click += OnOpenFolderClick;
        _btnAnalyze = CreateToolbarButton(BrandAccent);
        _btnAnalyze.Enabled = false;
        _btnAnalyze.Click += OnAnalyzeClick;
        _btnExportExcel = CreateToolbarButton();
        _btnExportExcel.Enabled = false;
        _btnExportExcel.Click += OnExportExcelClick;
        _btnExportPdf = CreateToolbarButton();
        _btnExportPdf.Enabled = false;
        _btnExportPdf.Click += OnExportPdfClick;

        flow.Controls.Add(_btnOpenFiles);
        flow.Controls.Add(_btnOpenFolder);
        flow.Controls.Add(Spacer());
        flow.Controls.Add(_btnAnalyze);
        flow.Controls.Add(Spacer());
        flow.Controls.Add(_btnExportExcel);
        flow.Controls.Add(_btnExportPdf);

        _lblFiles = new Label
        {
            AutoSize = false,
            Width = 300,
            Height = 36,
            ForeColor = Color.Gainsboro,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(16, 6, 0, 0),
        };
        flow.Controls.Add(_lblFiles);

        _cmbLanguage = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 110,
            Height = 36,
            Margin = new Padding(16, 6, 0, 0),
        };
        _cmbLanguage.Items.AddRange(["Türkçe", "English"]);
        _cmbLanguage.SelectedIndex = 0;
        _cmbLanguage.SelectedIndexChanged += OnLanguageChanged;
        flow.Controls.Add(_cmbLanguage);

        return bar;
    }

    private static Control Spacer() => new Panel { Width = 16, Height = 1 };

    private static Button CreateToolbarButton(Color? accent = null)
    {
        return new Button
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Height = 36,
            Padding = new Padding(12, 0, 12, 0),
            FlatStyle = FlatStyle.Flat,
            BackColor = accent ?? Color.FromArgb(60, 72, 92),
            ForeColor = Color.White,
            Margin = new Padding(0, 0, 8, 0),
            Cursor = Cursors.Hand,
            FlatAppearance = { BorderSize = 0 },
        };
    }

    private Control BuildTabs()
    {
        _tabControl = new TabControl { Dock = DockStyle.Fill, Padding = new Point(16, 8) };

        _tabSummary = BuildSummaryTab();
        _tabProblematic = BuildGridTab(out _gridProblematic);
        _tabAllEndpoints = BuildGridTab(out _gridAllEndpoints);
        _tabDegrading = BuildGridTab(out _gridDegrading);
        _tabAnomalies = BuildGridTab(out _gridAnomalies);
        _tabRawData = BuildRawDataTab();

        _tabControl.TabPages.AddRange([_tabSummary, _tabProblematic, _tabAllEndpoints, _tabDegrading, _tabAnomalies, _tabRawData]);

        BuildGridColumns(_gridProblematic, ProblematicColumns);
        BuildGridColumns(_gridAllEndpoints, AllEndpointsColumns);
        BuildGridColumns(_gridDegrading, DegradingColumns);
        BuildGridColumns(_gridAnomalies, AnomalyColumns);
        BuildRawGridColumns();
        _gridProblematic.CellFormatting += (_, e) => HighlightByProblemScore(_gridProblematic, e);

        return _tabControl;
    }

    private TabPage BuildSummaryTab()
    {
        var page = new TabPage { BackColor = Color.FromArgb(240, 242, 245), Padding = new Padding(16) };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 156));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        page.Controls.Add(layout);

        var cardsPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 1 };
        for (int i = 0; i < 5; i++)
            cardsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        layout.Controls.Add(cardsPanel, 0, 0);

        cardsPanel.Controls.Add(BuildCard("card.totalRequests.title", "card.totalRequests.tooltip", BrandAccent), 0, 0);
        cardsPanel.Controls.Add(BuildCard("card.errorRate.title", "card.errorRate.tooltip", DangerColor), 1, 0);
        cardsPanel.Controls.Add(BuildCard("card.avgResponse.title", "card.avgResponse.tooltip", BrandDark), 2, 0);
        cardsPanel.Controls.Add(BuildCard("card.anomalyCount.title", "card.anomalyCount.tooltip", WarningColor), 3, 0);
        cardsPanel.Controls.Add(BuildCard("card.degradingCount.title", "card.degradingCount.tooltip", WarningColor), 4, 0);

        var trafficPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(8), Margin = new Padding(0, 12, 0, 6) };
        _trafficPlot = new FormsPlot { Dock = DockStyle.Fill };
        trafficPanel.Controls.Add(_trafficPlot);
        layout.Controls.Add(trafficPanel, 0, 1);

        var responsePanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(8), Margin = new Padding(0, 6, 0, 0) };
        _responseTimePlot = new FormsPlot { Dock = DockStyle.Fill };
        responsePanel.Controls.Add(_responseTimePlot);
        layout.Controls.Add(responsePanel, 0, 2);

        return page;
    }

    private Control BuildCard(string titleKey, string tooltipKey, Color accent)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Margin = new Padding(0, 0, 12, 0),
        };
        panel.Paint += (_, e) =>
        {
            using var pen = new Pen(accent, 4);
            e.Graphics.DrawLine(pen, 0, 2, 0, panel.Height - 2);
        };

        var titleLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 40,
            Padding = new Padding(14, 8, 8, 0),
            ForeColor = Color.Gray,
            Font = new Font("Segoe UI", 8.5F),
            TextAlign = ContentAlignment.TopLeft,
            AutoEllipsis = true,
        };
        var valueLabel = new Label
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(14, 0, 8, 12),
            Font = new Font("Segoe UI", 16F, FontStyle.Bold),
            ForeColor = BrandDark,
            TextAlign = ContentAlignment.BottomLeft,
            Text = "-",
        };

        panel.Controls.Add(valueLabel);
        panel.Controls.Add(titleLabel);

        _cards.Add((titleLabel, valueLabel, titleKey, tooltipKey));
        return panel;
    }

    private TabPage BuildGridTab(out FastDataGridView grid)
    {
        var page = new TabPage { BackColor = Color.White, Padding = new Padding(8) };
        grid = new FastDataGridView { Dock = DockStyle.Fill, ShowCellToolTips = true };
        page.Controls.Add(grid);
        return page;
    }

    private TabPage BuildRawDataTab()
    {
        var page = new TabPage { BackColor = Color.White, Padding = new Padding(8) };
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        page.Controls.Add(layout);

        layout.Controls.Add(BuildRawDataFilterBar(), 0, 0);

        _lblRawCount = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.Gray,
            Font = new Font("Segoe UI", 8.5F),
        };
        layout.Controls.Add(_lblRawCount, 0, 1);

        _gridRawData = new FastDataGridView
        {
            Dock = DockStyle.Fill,
            ShowCellToolTips = true,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
            VirtualMode = true,
            AllowUserToOrderColumns = false,
        };
        _gridRawData.CellValueNeeded += OnRawGridCellValueNeeded;
        _gridRawData.ColumnHeaderMouseClick += OnRawGridColumnHeaderClick;
        layout.Controls.Add(_gridRawData, 0, 2);

        return page;
    }

    private Control BuildRawDataFilterBar()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoScroll = true,
            Padding = new Padding(0, 2, 0, 2),
        };

        _txtPathFilter = CreateFilterTextBox(170);
        _cmbMethodFilter = CreateFilterCombo(90);
        _cmbStatusFilter = CreateFilterCombo(80);
        _txtMinResponseTime = CreateFilterTextBox(90);
        _txtClientIpFilter = CreateFilterTextBox(130);
        _dtpFrom = CreateFilterDateTimePicker();
        _dtpTo = CreateFilterDateTimePicker();

        _cmbMethodFilter.SelectedIndexChanged += (_, _) => ApplyRawDataFilters();
        _cmbStatusFilter.SelectedIndexChanged += (_, _) => ApplyRawDataFilters();
        _dtpFrom.ValueChanged += (_, _) => ApplyRawDataFilters();
        _dtpTo.ValueChanged += (_, _) => ApplyRawDataFilters();

        void ApplyOnEnter(TextBox box) => box.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;
            ApplyRawDataFilters();
        };
        ApplyOnEnter(_txtPathFilter);
        ApplyOnEnter(_txtMinResponseTime);
        ApplyOnEnter(_txtClientIpFilter);

        AddLabeledFilter(panel, "rawdata.filter.path", _txtPathFilter);
        AddLabeledFilter(panel, "rawdata.filter.method", _cmbMethodFilter);
        AddLabeledFilter(panel, "rawdata.filter.status", _cmbStatusFilter);
        AddLabeledFilter(panel, "rawdata.filter.minResponseTime", _txtMinResponseTime);
        AddLabeledFilter(panel, "rawdata.filter.clientIp", _txtClientIpFilter);
        AddLabeledFilter(panel, "rawdata.filter.dateFrom", _dtpFrom);
        AddLabeledFilter(panel, "rawdata.filter.dateTo", _dtpTo);

        _btnApplyRawFilter = CreateToolbarButton(BrandAccent);
        _btnApplyRawFilter.Click += (_, _) => ApplyRawDataFilters();
        _btnClearRawFilter = CreateToolbarButton();
        _btnClearRawFilter.Click += (_, _) => { ClearRawDataFilterInputs(); ApplyRawDataFilters(); };

        var buttonsWrap = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, Margin = new Padding(0, 17, 0, 0) };
        buttonsWrap.Controls.Add(_btnApplyRawFilter);
        buttonsWrap.Controls.Add(_btnClearRawFilter);
        panel.Controls.Add(buttonsWrap);

        return panel;
    }

    private void AddLabeledFilter(FlowLayoutPanel panel, string labelKey, Control control)
    {
        var wrap = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, Margin = new Padding(0, 0, 14, 4) };
        var label = new Label { AutoSize = true, Font = new Font("Segoe UI", 7.5F), ForeColor = Color.Gray, Margin = new Padding(2, 0, 0, 2) };
        _rawFilterLabels.Add((label, labelKey));
        wrap.Controls.Add(label);
        wrap.Controls.Add(control);
        panel.Controls.Add(wrap);
    }

    private static TextBox CreateFilterTextBox(int width) => new() { Width = width, Height = 24 };

    private static ComboBox CreateFilterCombo(int width) => new() { Width = width, DropDownStyle = ComboBoxStyle.DropDownList };

    private static DateTimePicker CreateFilterDateTimePicker() => new()
    {
        Width = 155,
        Format = DateTimePickerFormat.Custom,
        CustomFormat = "yyyy-MM-dd HH:mm",
    };

    private void BuildRawGridColumns()
    {
        _gridRawData.Columns.Clear();
        foreach (var s in RawDataColumns)
        {
            var col = new DataGridViewTextBoxColumn { Name = s.Name, Width = s.Width };
            if (s.Format is not null)
                col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            _gridRawData.Columns.Add(col);
        }
    }

    private void OnRawGridCellValueNeeded(object? sender, DataGridViewCellValueEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= _filteredRawEntries.Length)
            return;

        var entry = _filteredRawEntries[e.RowIndex];
        var culture = _language.ToCultureInfo();
        e.Value = _gridRawData.Columns[e.ColumnIndex].Name switch
        {
            "Time" => entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
            "Method" => entry.Method,
            "Path" => entry.UriStem,
            "Query" => entry.UriQuery ?? string.Empty,
            "Status" => entry.StatusCode.ToString(culture),
            "SubStatus" => entry.SubStatusCode.ToString(culture),
            "TimeTaken" => entry.TimeTakenMs.ToString("N0", culture),
            "ClientIp" => entry.ClientIp ?? string.Empty,
            "Port" => entry.Port?.ToString(culture) ?? string.Empty,
            "Username" => entry.Username ?? string.Empty,
            "UserAgent" => entry.UserAgent ?? string.Empty,
            "Referer" => entry.Referer ?? string.Empty,
            _ => string.Empty,
        };
    }

    private void OnRawGridColumnHeaderClick(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (e.RowIndex != -1 || e.ColumnIndex < 0 || _filteredRawEntries.Length == 0)
            return;

        var columnName = _gridRawData.Columns[e.ColumnIndex].Name;
        if (_rawSortColumn == columnName)
            _rawSortAscending = !_rawSortAscending;
        else
        {
            _rawSortColumn = columnName;
            _rawSortAscending = true;
        }

        SortFilteredRawEntries();
        UpdateRawSortGlyphs();
        _gridRawData.Invalidate();
    }

    private void SortFilteredRawEntries()
    {
        if (_rawSortColumn is null || _filteredRawEntries.Length == 0)
            return;

        IComparable KeySelector(LogEntry e) => _rawSortColumn switch
        {
            "Time" => e.Timestamp,
            "Method" => e.Method,
            "Path" => e.UriStem,
            "Query" => e.UriQuery ?? string.Empty,
            "Status" => e.StatusCode,
            "SubStatus" => e.SubStatusCode,
            "TimeTaken" => e.TimeTakenMs,
            "ClientIp" => e.ClientIp ?? string.Empty,
            "Port" => e.Port ?? 0,
            "Username" => e.Username ?? string.Empty,
            "UserAgent" => e.UserAgent ?? string.Empty,
            "Referer" => e.Referer ?? string.Empty,
            _ => e.Timestamp,
        };

        _filteredRawEntries = _rawSortAscending
            ? _filteredRawEntries.OrderBy(KeySelector).ToArray()
            : _filteredRawEntries.OrderByDescending(KeySelector).ToArray();
    }

    private void UpdateRawSortGlyphs()
    {
        foreach (DataGridViewColumn col in _gridRawData.Columns)
            col.HeaderCell.SortGlyphDirection = SortOrder.None;

        if (_rawSortColumn is not null)
            _gridRawData.Columns[_rawSortColumn].HeaderCell.SortGlyphDirection = _rawSortAscending ? SortOrder.Ascending : SortOrder.Descending;
    }

    private void PopulateRawDataTab(AnalysisReport report)
    {
        _cmbMethodFilter.Items.Clear();
        _cmbMethodFilter.Items.Add(Loc.T("rawdata.filter.allMethods", _language));
        foreach (var m in _rawEntries.Select(en => en.Method).Distinct().OrderBy(m => m, StringComparer.Ordinal))
            _cmbMethodFilter.Items.Add(m);
        _cmbMethodFilter.SelectedIndex = 0;

        _cmbStatusFilter.Items.Clear();
        _cmbStatusFilter.Items.Add(Loc.T("rawdata.filter.allStatuses", _language));
        _cmbStatusFilter.Items.AddRange(["2xx", "3xx", "4xx", "5xx"]);
        _cmbStatusFilter.SelectedIndex = 0;

        var minDate = report.PeriodStartUtc ?? DateTime.UtcNow.AddDays(-1);
        var maxDate = report.PeriodEndUtc ?? DateTime.UtcNow;
        _dtpFrom.MinDate = minDate.AddDays(-1);
        _dtpFrom.MaxDate = maxDate.AddDays(1);
        _dtpTo.MinDate = minDate.AddDays(-1);
        _dtpTo.MaxDate = maxDate.AddDays(1);
        _dtpFrom.Value = minDate;
        _dtpTo.Value = maxDate;

        _txtPathFilter.Clear();
        _txtClientIpFilter.Clear();
        _txtMinResponseTime.Clear();

        _rawSortColumn = "Time";
        _rawSortAscending = true;
        UpdateRawSortGlyphs();

        ApplyRawDataFilters();
    }

    private void ClearRawDataFilterInputs()
    {
        _txtPathFilter.Clear();
        _txtClientIpFilter.Clear();
        _txtMinResponseTime.Clear();
        if (_cmbMethodFilter.Items.Count > 0) _cmbMethodFilter.SelectedIndex = 0;
        if (_cmbStatusFilter.Items.Count > 0) _cmbStatusFilter.SelectedIndex = 0;

        var minDate = _currentReport?.PeriodStartUtc;
        var maxDate = _currentReport?.PeriodEndUtc;
        if (minDate is not null) _dtpFrom.Value = minDate.Value;
        if (maxDate is not null) _dtpTo.Value = maxDate.Value;
    }

    private void ApplyRawDataFilters()
    {
        if (_rawEntries.Count == 0)
        {
            _filteredRawEntries = [];
            _gridRawData.RowCount = 0;
            _lblRawCount.Text = Loc.T("rawdata.noData", _language);
            _gridRawData.Invalidate();
            return;
        }

        var pathFilter = _txtPathFilter.Text.Trim();
        var ipFilter = _txtClientIpFilter.Text.Trim();
        var method = _cmbMethodFilter.SelectedIndex > 0 ? (string)_cmbMethodFilter.SelectedItem! : null;
        var statusClass = _cmbStatusFilter.SelectedIndex > 0 ? (string)_cmbStatusFilter.SelectedItem! : null;
        _ = long.TryParse(_txtMinResponseTime.Text.Trim(), NumberStyles.Integer, _language.ToCultureInfo(), out var minResponseTime);
        var from = _dtpFrom.Value;
        var to = _dtpTo.Value;

        IEnumerable<LogEntry> query = _rawEntries;

        if (!string.IsNullOrEmpty(pathFilter))
            query = query.Where(en => en.UriStem.Contains(pathFilter, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(ipFilter))
            query = query.Where(en => en.ClientIp is not null && en.ClientIp.Contains(ipFilter, StringComparison.OrdinalIgnoreCase));
        if (method is not null)
            query = query.Where(en => en.Method == method);
        if (statusClass is not null)
        {
            var digit = statusClass[0] - '0';
            query = query.Where(en => en.StatusCode / 100 == digit);
        }
        if (minResponseTime > 0)
            query = query.Where(en => en.TimeTakenMs >= minResponseTime);
        query = query.Where(en => en.Timestamp >= from && en.Timestamp <= to);

        _filteredRawEntries = query.ToArray();
        SortFilteredRawEntries();

        _gridRawData.RowCount = _filteredRawEntries.Length;
        _lblRawCount.Text = Loc.F("rawdata.showingCount", _language, _filteredRawEntries.Length, _rawEntries.Count);
        _gridRawData.Invalidate();
    }

    /// <summary>Re-renders the raw data grid/count label without touching the current
    /// filter selection or sort order — used when the language changes.</summary>
    private void RefreshRawDataView()
    {
        _lblRawCount.Text = _rawEntries.Count == 0
            ? Loc.T("rawdata.noData", _language)
            : Loc.F("rawdata.showingCount", _language, _filteredRawEntries.Length, _rawEntries.Count);
        _gridRawData.Invalidate();
    }

    private Control BuildStatusBar()
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(230, 232, 236), Padding = new Padding(12, 4, 12, 4) };

        _lblStatus = new Label { Dock = DockStyle.Left, AutoSize = true, TextAlign = ContentAlignment.MiddleLeft };
        _progressBar = new ProgressBar { Dock = DockStyle.Right, Width = 220, Height = 16, Style = ProgressBarStyle.Continuous, Visible = false };

        panel.Controls.Add(_progressBar);
        panel.Controls.Add(_lblStatus);
        return panel;
    }

    // ---- Grid column setup ----------------------------------------------------

    private static void BuildGridColumns(FastDataGridView grid, (string Name, string HeaderKey, string? TooltipKey, int Width, string? Format)[] specs)
    {
        grid.Columns.Clear();
        foreach (var s in specs)
        {
            DataGridViewTextBoxColumn col = s.Format is null
                ? new DataGridViewTextBoxColumn { Name = s.Name, FillWeight = s.Width }
                : new DataGridViewTextBoxColumn
                {
                    Name = s.Name,
                    FillWeight = s.Width,
                    DefaultCellStyle = { Format = s.Format, Alignment = DataGridViewContentAlignment.MiddleRight },
                };
            grid.Columns.Add(col);
        }
    }

    private void ApplyGridLocalization(FastDataGridView grid, (string Name, string HeaderKey, string? TooltipKey, int Width, string? Format)[] specs)
    {
        foreach (var s in specs)
        {
            var col = grid.Columns[s.Name];
            col.HeaderText = Loc.T(s.HeaderKey, _language);
            col.HeaderCell.ToolTipText = s.TooltipKey is null ? string.Empty : Loc.T(s.TooltipKey, _language);
        }
    }

    private void HighlightByProblemScore(FastDataGridView grid, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= grid.Rows.Count) return;
        var scoreCell = grid.Rows[e.RowIndex].Cells["Score"];
        if (scoreCell.Value is double score)
        {
            var row = grid.Rows[e.RowIndex];
            row.DefaultCellStyle.BackColor = score switch
            {
                >= 70 => Color.FromArgb(250, 218, 216),
                >= 40 => Color.FromArgb(253, 236, 210),
                _ => e.RowIndex % 2 == 1 ? Color.FromArgb(247, 248, 250) : Color.White,
            };
        }
    }

    // ---- Localization -----------------------------------------------------------

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        _language = _cmbLanguage.SelectedIndex == 0 ? AppLanguage.Turkish : AppLanguage.English;
        ApplyLanguage();
    }

    private void ApplyLanguage()
    {
        CultureInfo.CurrentCulture = _language.ToCultureInfo();

        Text = Loc.T("app.title", _language);
        _btnOpenFiles.Text = Loc.T("toolbar.openFiles", _language);
        _btnOpenFolder.Text = Loc.T("toolbar.openFolder", _language);
        _btnAnalyze.Text = Loc.T("toolbar.analyze", _language);
        _btnExportExcel.Text = Loc.T("toolbar.exportExcel", _language);
        _btnExportPdf.Text = Loc.T("toolbar.exportPdf", _language);

        _tabSummary.Text = Loc.T("tab.summary", _language);
        _tabProblematic.Text = Loc.T("tab.problematic", _language);
        _tabAllEndpoints.Text = Loc.T("tab.allEndpoints", _language);
        _tabDegrading.Text = Loc.T("tab.degrading", _language);
        _tabAnomalies.Text = Loc.T("tab.anomalies", _language);
        _tabRawData.Text = Loc.T("tab.rawData", _language);

        foreach (var (label, key) in _rawFilterLabels)
            label.Text = Loc.T(key, _language);
        _btnApplyRawFilter.Text = Loc.T("rawdata.filter.apply", _language);
        _btnClearRawFilter.Text = Loc.T("rawdata.filter.clear", _language);
        if (_cmbMethodFilter.Items.Count > 0) _cmbMethodFilter.Items[0] = Loc.T("rawdata.filter.allMethods", _language);
        if (_cmbStatusFilter.Items.Count > 0) _cmbStatusFilter.Items[0] = Loc.T("rawdata.filter.allStatuses", _language);
        ApplyGridLocalization(_gridRawData, RawDataColumns);
        RefreshRawDataView();

        foreach (var (titleLabel, _, titleKey, tooltipKey) in _cards)
        {
            titleLabel.Text = Loc.T(titleKey, _language);
            _toolTip.SetToolTip(titleLabel, Loc.T(tooltipKey, _language));
        }

        ApplyGridLocalization(_gridProblematic, ProblematicColumns);
        ApplyGridLocalization(_gridAllEndpoints, AllEndpointsColumns);
        ApplyGridLocalization(_gridDegrading, DegradingColumns);
        ApplyGridLocalization(_gridAnomalies, AnomalyColumns);

        RefreshFilesLabel();
        RefreshStatus();

        if (_currentReport is not null)
            RenderReport(_currentReport);
        else
        {
            InitEmptyChart(_trafficPlot, "chart.traffic.title", "chart.traffic.ylabel");
            InitEmptyChart(_responseTimePlot, "chart.responseTime.title", "chart.responseTime.ylabel");
        }
    }

    private void InitEmptyChart(FormsPlot plot, string titleKey, string ylabelKey)
    {
        plot.Plot.Clear();
        plot.Plot.Title(Loc.T(titleKey, _language));
        plot.Plot.YLabel(Loc.T(ylabelKey, _language));
        plot.Refresh();
    }

    // ---- File selection ---------------------------------------------------------

    private void OnOpenFilesClick(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Multiselect = true,
            Filter = Loc.T("dialog.openFiles.filter", _language),
            Title = Loc.T("dialog.openFiles.title", _language),
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            SetLogFiles(dialog.FileNames);
    }

    private void OnOpenFolderClick(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog { Description = Loc.T("dialog.openFolder.description", _language) };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        var files = Directory.EnumerateFiles(dialog.SelectedPath, "*.log", SearchOption.AllDirectories).ToArray();
        if (files.Length == 0)
        {
            MessageBox.Show(this, Loc.T("dialog.noLogsInFolder", _language), Loc.T("dialog.info", _language), MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        SetLogFiles(files);
    }

    private void SetLogFiles(IEnumerable<string> files)
    {
        _logFiles.Clear();
        _logFiles.AddRange(files);
        RefreshFilesLabel();
        _btnAnalyze.Enabled = _logFiles.Count > 0;
        RefreshStatus();
    }

    private void RefreshFilesLabel()
    {
        _lblFiles.Text = _logFiles.Count switch
        {
            0 => Loc.T("files.none", _language),
            1 => Path.GetFileName(_logFiles[0]),
            _ => Loc.F("files.multiple", _language, _logFiles.Count),
        };
    }

    private void RefreshStatus()
    {
        if (_isBusy) return;

        if (_currentReport is not null)
            SetStatus(Loc.F("status.analysisComplete", _language, _currentReport.TotalRequests, _currentReport.Endpoints.Count, _lastAnalysisElapsedSeconds));
        else if (_logFiles.Count > 0)
            SetStatus(Loc.F("status.filesReady", _language, _logFiles.Count));
        else
            SetStatus(Loc.T("status.ready", _language));
    }

    // ---- Analysis ---------------------------------------------------------------

    private async void OnAnalyzeClick(object? sender, EventArgs e)
    {
        if (_isBusy || _logFiles.Count == 0)
            return;

        SetBusy(true);
        var warnings = new List<string>();
        var sw = Stopwatch.StartNew();

        try
        {
            var progress = new Progress<int>(n => SetStatus(Loc.F("status.processing", _language, n)));

            var (report, rawEntries) = await Task.Run(() =>
            {
                var parser = new W3CLogParser();
                parser.OnWarning += w => { lock (warnings) warnings.Add(w); };

                var engine = new LogAnalysisEngine();
                engine.OnProgress += n => ((IProgress<int>)progress).Report(n);

                var collected = new List<LogEntry>();

                IEnumerable<LogEntry> Tracked()
                {
                    foreach (var entry in parser.ParseFiles(_logFiles))
                    {
                        collected.Add(entry);
                        yield return entry;
                    }
                }

                var analysisReport = engine.Analyze(Tracked());
                return (analysisReport, collected);
            });

            report.ParseWarningCount = warnings.Count;
            sw.Stop();
            _lastAnalysisElapsedSeconds = sw.Elapsed.TotalSeconds;
            _currentReport = report;
            _rawEntries.Clear();
            _rawEntries.AddRange(rawEntries);
            RenderReport(report);
            PopulateRawDataTab(report);
            RefreshStatus();
            _btnExportExcel.Enabled = true;
            _btnExportPdf.Enabled = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, Loc.F("dialog.analyzeError", _language, ex.Message), Loc.T("dialog.error", _language), MessageBoxButtons.OK, MessageBoxIcon.Error);
            SetStatus(Loc.T("status.analysisFailed", _language));
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void RenderReport(AnalysisReport report)
    {
        var cardValues = new[]
        {
            report.TotalRequests.ToString("N0", _language.ToCultureInfo()),
            $"%{report.OverallErrorRatePercent.ToString("F2", _language.ToCultureInfo())}",
            $"{report.AvgResponseTimeMs.ToString("F0", _language.ToCultureInfo())} ms",
            report.Anomalies.Count.ToString("N0", _language.ToCultureInfo()),
            report.DegradingEndpoints.Count.ToString("N0", _language.ToCultureInfo()),
        };
        for (int i = 0; i < _cards.Count && i < cardValues.Length; i++)
            _cards[i].ValueLabel.Text = cardValues[i];

        RenderTrafficChart(report);
        RenderResponseTimeChart(report);
        RenderProblematicGrid(report);
        RenderAllEndpointsGrid(report);
        RenderDegradingGrid(report);
        RenderAnomaliesGrid(report);
        RefreshRawDataView();
    }

    private void RenderTrafficChart(AnalysisReport report)
    {
        _trafficPlot.Plot.Clear();
        _trafficPlot.Plot.Title(Loc.T("chart.traffic.title", _language));

        if (report.HourlyTraffic.Count > 0)
        {
            var xs = report.HourlyTraffic.Select(b => b.BucketStartUtc.ToOADate()).ToArray();
            var ys = report.HourlyTraffic.Select(b => (double)b.RequestCount).ToArray();
            var scatter = _trafficPlot.Plot.Add.Scatter(xs, ys);
            scatter.Color = ScottPlot.Colors.Blue.WithAlpha(0.7);
            scatter.LineWidth = 2;
            scatter.MarkerSize = 4;
            scatter.LegendText = Loc.T("chart.traffic.legend", _language);

            var anomalyBuckets = report.Anomalies
                .Where(a => a.Type is AnomalyType.TrafficSpike or AnomalyType.TrafficDrop)
                .Select(a => a.BucketStartUtc)
                .ToHashSet();

            if (anomalyBuckets.Count > 0)
            {
                var pairs = report.HourlyTraffic.Where(b => anomalyBuckets.Contains(b.BucketStartUtc)).ToArray();
                var axs = pairs.Select(b => b.BucketStartUtc.ToOADate()).ToArray();
                var ays = pairs.Select(b => (double)b.RequestCount).ToArray();
                var markers = _trafficPlot.Plot.Add.ScatterPoints(axs, ays);
                markers.Color = ScottPlot.Colors.Red;
                markers.MarkerSize = 10;
                markers.LegendText = Loc.T("chart.traffic.anomalyLegend", _language);
                _trafficPlot.Plot.ShowLegend();
            }

            _trafficPlot.Plot.Axes.DateTimeTicksBottom();
        }

        _trafficPlot.Plot.YLabel(Loc.T("chart.traffic.ylabel", _language));
        _trafficPlot.Refresh();
    }

    private void RenderResponseTimeChart(AnalysisReport report)
    {
        _responseTimePlot.Plot.Clear();
        _responseTimePlot.Plot.Title(Loc.T("chart.responseTime.title", _language));

        if (report.HourlyTraffic.Count > 0)
        {
            var xs = report.HourlyTraffic.Select(b => b.BucketStartUtc.ToOADate()).ToArray();
            var avg = report.HourlyTraffic.Select(b => b.AvgResponseTimeMs).ToArray();

            var scatter = _responseTimePlot.Plot.Add.Scatter(xs, avg);
            scatter.Color = ScottPlot.Colors.Orange;
            scatter.LineWidth = 2;
            scatter.MarkerSize = 4;
            scatter.LegendText = Loc.T("chart.responseTime.avglegend", _language);

            _responseTimePlot.Plot.Axes.DateTimeTicksBottom();
            _responseTimePlot.Plot.ShowLegend();
        }

        _responseTimePlot.Plot.YLabel(Loc.T("chart.responseTime.ylabel", _language));
        _responseTimePlot.Refresh();
    }

    private void RenderProblematicGrid(AnalysisReport report)
    {
        var g = _gridProblematic;
        g.Rows.Clear();
        var rank = 1;
        foreach (var ep in report.TopProblematicEndpoints)
        {
            g.Rows.Add(rank++, ep.Method, ep.NormalizedPath, ep.ProblemScore, ep.ErrorRatePercent,
                ep.P95ResponseTimeMs, ep.IsDegradingTrend ? Loc.T("trend.degrading", _language) : Loc.T("trend.stable", _language), ep.RequestCount);
        }
    }

    private void RenderAllEndpointsGrid(AnalysisReport report)
    {
        var g = _gridAllEndpoints;
        g.Rows.Clear();
        foreach (var ep in report.Endpoints)
        {
            g.Rows.Add(ep.Method, ep.NormalizedPath, ep.RequestCount, ep.AvgResponseTimeMs,
                ep.P50ResponseTimeMs, ep.P90ResponseTimeMs, ep.P95ResponseTimeMs, ep.P99ResponseTimeMs,
                ep.ClientErrorCount, ep.ServerErrorCount, ep.ErrorRatePercent, ep.ProblemScore);
        }
    }

    private void RenderDegradingGrid(AnalysisReport report)
    {
        var g = _gridDegrading;
        g.Rows.Clear();
        foreach (var ep in report.DegradingEndpoints)
            g.Rows.Add(ep.Method, ep.NormalizedPath, ep.TrendSlopeMsPerHour, ep.RequestCount, ep.P95ResponseTimeMs);
    }

    private void RenderAnomaliesGrid(AnalysisReport report)
    {
        var g = _gridAnomalies;
        g.Rows.Clear();
        foreach (var a in report.Anomalies)
        {
            var typeLabel = a.Type switch
            {
                AnomalyType.TrafficSpike => Loc.T("anomaly.trafficSpike", _language),
                AnomalyType.TrafficDrop => Loc.T("anomaly.trafficDrop", _language),
                AnomalyType.ErrorRateSpike => Loc.T("anomaly.errorSpike", _language),
                _ => a.Type.ToString(),
            };
            g.Rows.Add(a.BucketStartUtc.ToString("yyyy-MM-dd HH:mm"), typeLabel, a.ObservedValue, a.ExpectedValue, a.DeviationScore, a.Description);
        }
    }

    // ---- Export -------------------------------------------------------------------

    private void OnExportExcelClick(object? sender, EventArgs e)
    {
        if (_currentReport is null) return;

        using var dialog = new SaveFileDialog
        {
            Filter = Loc.T("dialog.saveExcel.filter", _language),
            FileName = $"IisLogRaporu_{DateTime.Now:yyyyMMdd_HHmm}.xlsx",
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            ExcelReportExporter.Export(_currentReport, dialog.FileName, _language);
            SetStatus(Loc.F("status.excelSaved", _language, dialog.FileName));
            OfferToOpen(dialog.FileName);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, Loc.F("dialog.excelExportError", _language, ex.Message), Loc.T("dialog.error", _language), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnExportPdfClick(object? sender, EventArgs e)
    {
        if (_currentReport is null) return;

        using var dialog = new SaveFileDialog
        {
            Filter = Loc.T("dialog.savePdf.filter", _language),
            FileName = $"IisLogRaporu_{DateTime.Now:yyyyMMdd_HHmm}.pdf",
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            var source = _logFiles.Count == 1 ? Path.GetFileName(_logFiles[0]) : Loc.F("files.multiple", _language, _logFiles.Count);
            PdfReportExporter.Export(_currentReport, dialog.FileName, source, _language);
            SetStatus(Loc.F("status.pdfSaved", _language, dialog.FileName));
            OfferToOpen(dialog.FileName);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, Loc.F("dialog.pdfExportError", _language, ex.Message), Loc.T("dialog.error", _language), MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OfferToOpen(string filePath)
    {
        var result = MessageBox.Show(this, Loc.T("dialog.reportReady.message", _language), Loc.T("dialog.reportReady.title", _language), MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (result == DialogResult.Yes)
            Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
    }

    // ---- Shared UI helpers ----------------------------------------------------------

    private void SetBusy(bool busy)
    {
        _isBusy = busy;
        _btnOpenFiles.Enabled = !busy;
        _btnOpenFolder.Enabled = !busy;
        _btnAnalyze.Enabled = !busy && _logFiles.Count > 0;
        _btnExportExcel.Enabled = !busy && _currentReport is not null;
        _btnExportPdf.Enabled = !busy && _currentReport is not null;
        _progressBar.Visible = busy;
        _progressBar.Style = busy ? ProgressBarStyle.Marquee : ProgressBarStyle.Continuous;
        UseWaitCursor = busy;
    }

    private void SetStatus(string text)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => _lblStatus.Text = text);
            return;
        }
        _lblStatus.Text = text;
    }
}
