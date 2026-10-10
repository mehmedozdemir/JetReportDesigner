using System.Globalization;
using System.Runtime.InteropServices.WindowsRuntime;
using JetReportDesigner.Client;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using RaporOnizleyici.Services;
using Windows.Graphics;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using WinRT.Interop;

namespace RaporOnizleyici;

public sealed partial class MainWindow : Window
{
    private readonly EnvironmentStore _store = new();
    private readonly Dictionary<string, Func<object?>> _parameterReaders = new(StringComparer.Ordinal);
    private readonly List<SourceModel> _sources = [];
    private readonly List<(Image Image, double Pixels, double Dpi)> _pages = [];
    private readonly Dictionary<string, Func<bool>> _commitJson = new(StringComparer.Ordinal);

    private JetReportClient? _client;
    private List<ReportInfo> _reports = [];
    private ReportInfo? _current;
    private ReportSchema? _schema;
    private int _loadToken;

    public MainWindow()
    {
        InitializeComponent();
        Title = "Rapor Önizleyici";
        if (AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
        {
            presenter.Maximize();
        }

        _store.Load();
        FillSystemCombo(_store.LastSelected);
        if (_store.Systems.Count == 0)
        {
            Root.Loaded += async (_, _) => await ManageSystemsAsync();
        }
    }

    // ---------------------------------------------------------------- systems

    private void FillSystemCombo(Guid? select)
    {
        SystemCombo.SelectionChanged -= SystemCombo_SelectionChanged;
        SystemCombo.Items.Clear();
        foreach (var s in _store.Systems)
        {
            SystemCombo.Items.Add(s);
        }

        SystemCombo.SelectionChanged += SystemCombo_SelectionChanged;
        var pick = _store.Systems.FirstOrDefault(s => s.Id == select) ?? _store.Systems.FirstOrDefault();
        SystemCombo.SelectedItem = pick;
        if (pick is null)
        {
            SetClient(null);
        }
    }

    private async void SystemCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SystemCombo.SelectedItem is not SystemEntry system)
        {
            return;
        }

        _store.LastSelected = system.Id;
        _store.Save();
        SetClient(system);
        await LoadReportsAsync();
    }

    private async void ManageSystems_Click(object sender, RoutedEventArgs e) => await ManageSystemsAsync();

    private async Task ManageSystemsAsync()
    {
        var dialog = new SystemsDialog(_store.Systems, SystemCombo.SelectedItem as SystemEntry) { XamlRoot = Root.XamlRoot };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        _store.Systems.Clear();
        _store.Systems.AddRange(dialog.Result);
        _store.Save();
        FillSystemCombo(dialog.Selected?.Id ?? _store.LastSelected);
    }

    private void SetClient(SystemEntry? system)
    {
        _client?.Dispose();
        _client = null;
        if (system is null || string.IsNullOrWhiteSpace(system.Url) || string.IsNullOrWhiteSpace(system.ApiKey))
        {
            SystemInfo.Text = system is null ? string.Empty : $"{system.Url} — API anahtarı eksik, “Sistemleri yönet” ile girin.";
            return;
        }

        _client = new JetReportClient(new JetReportClientOptions { BaseUrl = system.Url.Trim(), ApiKey = system.ApiKey.Trim(), Timeout = TimeSpan.FromMinutes(3) });
        SystemInfo.Text = system.Url;
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await LoadReportsAsync();

    // ---------------------------------------------------------------- reports

    private async Task LoadReportsAsync()
    {
        ClearReportSelection();
        _reports = [];
        RenderReportList();
        if (_client is null)
        {
            return;
        }

        await RunAsync(async () =>
        {
            _reports = (await _client.ListReportsAsync()).ToList();
            RenderReportList();
            Notify($"{_reports.Count} rapor yüklendi.", InfoBarSeverity.Success, autoClose: true);
        });
    }

    private void RenderReportList()
    {
        var q = SearchBox.Text.Trim();
        ReportList.SelectionChanged -= ReportList_SelectionChanged;
        ReportList.Items.Clear();
        foreach (var r in _reports.Where(r => q.Length == 0
                     || r.Name.Contains(q, StringComparison.CurrentCultureIgnoreCase)
                     || r.Code.Contains(q, StringComparison.OrdinalIgnoreCase)))
        {
            var panel = new StackPanel { Spacing = 0, Padding = new Thickness(0, 4, 0, 4) };
            panel.Children.Add(new TextBlock { Text = r.Name, TextTrimming = TextTrimming.CharacterEllipsis });
            panel.Children.Add(new TextBlock { Text = r.Code, FontSize = 11, Opacity = 0.6, TextTrimming = TextTrimming.CharacterEllipsis });
            ReportList.Items.Add(new ListViewItem { Content = panel, Tag = r });
        }

        ReportList.SelectionChanged += ReportList_SelectionChanged;
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => RenderReportList();

    private async void ReportList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ReportList.SelectedItem is not ListViewItem { Tag: ReportInfo info } || _client is null)
        {
            return;
        }

        _current = info;
        var token = ++_loadToken;
        ReportTitle.Text = info.Name;
        ReportCode.Text = info.Code;
        InputHost.Children.Clear();
        ClearPreview();
        PreviewButton.IsEnabled = false;
        SaveButton.IsEnabled = false;

        await RunAsync(async () =>
        {
            var schema = await _client.GetSchemaAsync(info.Code);
            if (token != _loadToken)
            {
                return; // the user already picked another report
            }

            _schema = schema;
            BuildInputs(schema);
            PreviewButton.IsEnabled = true;
            SaveButton.IsEnabled = true;
        });
    }

    private void ClearReportSelection()
    {
        _current = null;
        _schema = null;
        _sources.Clear();
        _parameterReaders.Clear();
        _commitJson.Clear();
        InputHost.Children.Clear();
        ReportTitle.Text = "Bir rapor seçin";
        ReportCode.Text = string.Empty;
        PreviewButton.IsEnabled = false;
        SaveButton.IsEnabled = false;
        ClearPreview();
    }

    // ---------------------------------------------------------------- dynamic inputs

    private void BuildInputs(ReportSchema schema)
    {
        InputHost.Children.Clear();
        _sources.Clear();
        _parameterReaders.Clear();
        _commitJson.Clear();

        if (schema.Parameters.Count > 0)
        {
            InputHost.Children.Add(SectionTitle("Parametreler"));
            foreach (var p in schema.Parameters)
            {
                InputHost.Children.Add(BuildParameter(p));
            }
        }

        if (schema.DataSources.Count > 0)
        {
            InputHost.Children.Add(SectionTitle("Veri kaynakları"));
            var first = true;
            foreach (var ds in schema.DataSources)
            {
                var model = new SourceModel(ds);
                _sources.Add(model);
                InputHost.Children.Add(BuildSource(model, first));
                first = false;
            }
        }

        if (schema.Parameters.Count == 0 && schema.DataSources.Count == 0)
        {
            InputHost.Children.Add(new TextBlock { Text = "Bu rapor dışarıdan parametre veya veri almıyor.", Opacity = 0.7, TextWrapping = TextWrapping.Wrap });
        }
    }

    private static TextBlock SectionTitle(string text) =>
        new() { Text = text, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 15 };

    private FrameworkElement BuildParameter(ReportParameterInfo p)
    {
        var type = p.Type.ToLowerInvariant();
        var header = p.Required ? p.Label + " *" : p.Label;
        FrameworkElement control;

        if (p.AllowedValues.Count > 0)
        {
            var combo = new ComboBox { Header = header, HorizontalAlignment = HorizontalAlignment.Stretch };
            foreach (var v in p.AllowedValues)
            {
                combo.Items.Add(v);
            }

            combo.SelectedItem = p.DefaultValue is not null && p.AllowedValues.Contains(p.DefaultValue) ? p.DefaultValue : null;
            _parameterReaders[p.Name] = () => combo.SelectedItem is string s ? Typed(type, s) : null;
            control = combo;
        }
        else if (type == "boolean")
        {
            var toggle = new ToggleSwitch { Header = header, IsOn = string.Equals(p.DefaultValue, "true", StringComparison.OrdinalIgnoreCase) };
            _parameterReaders[p.Name] = () => toggle.IsOn;
            control = toggle;
        }
        else if (type == "number")
        {
            var box = new NumberBox { Header = header, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Hidden, HorizontalAlignment = HorizontalAlignment.Stretch };
            box.Value = SourceModel.TryParseNumber(p.DefaultValue ?? string.Empty, out var d) ? d : double.NaN;
            _parameterReaders[p.Name] = () => double.IsNaN(box.Value) ? null : box.Value;
            control = box;
        }
        else if (type is "date" or "datetime")
        {
            var picker = new CalendarDatePicker { Header = header, HorizontalAlignment = HorizontalAlignment.Stretch };
            if (DateTime.TryParse(p.DefaultValue, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            {
                picker.Date = new DateTimeOffset(dt);
            }

            _parameterReaders[p.Name] = () => picker.Date is { } d ? d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : null;
            control = picker;
        }
        else
        {
            var box = new TextBox { Header = header, Text = p.DefaultValue ?? string.Empty };
            _parameterReaders[p.Name] = () => string.IsNullOrEmpty(box.Text) ? null : box.Text;
            control = box;
        }

        return control;
    }

    private static object? Typed(string type, string value) =>
        type == "number" && SourceModel.TryParseNumber(value, out var d) ? d
        : type == "boolean" && bool.TryParse(value, out var b) ? b
        : value;

    private FrameworkElement BuildSource(SourceModel m, bool expanded)
    {
        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        header.Children.Add(new TextBlock { Text = m.Name, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        header.Children.Add(new TextBlock { Text = $"{m.Kind} · {m.Columns.Count} alan", Opacity = 0.6, VerticalAlignment = VerticalAlignment.Center, FontSize = 12 });

        var body = new StackPanel { Spacing = 8 };
        var send = new CheckBox
        {
            Content = m.Kind.Equals("json", StringComparison.OrdinalIgnoreCase)
                ? "Bu kaynak için kendi verimi gönder"
                : "Sunucudaki sorgu yerine kendi verimi gönder",
            IsChecked = m.Send,
        };
        send.Checked += (_, _) => m.Send = true;
        send.Unchecked += (_, _) => m.Send = false;
        body.Children.Add(send);

        var jsonToggle = new ToggleButton { Content = "JSON olarak düzenle", HorizontalAlignment = HorizontalAlignment.Left };
        body.Children.Add(jsonToggle);

        var editor = new ContentControl { HorizontalContentAlignment = HorizontalAlignment.Stretch };
        body.Children.Add(editor);

        TextBox? jsonBox = null;
        List<Dictionary<string, string>> snapshot = [];

        void ShowTable()
        {
            editor.Content = BuildTable(m, send);
        }

        // Applies what is in the JSON box (if the JSON view is open) to the model; false when the JSON is invalid.
        bool Commit()
        {
            if (jsonBox is null)
            {
                return true;
            }

            var text = RestoreShortened(jsonBox.Text, snapshot, m);
            if (!m.TryLoadJson(text, out var error))
            {
                Notify($"“{m.Name}” JSON'u geçersiz: {error}", InfoBarSeverity.Error);
                return false;
            }

            return true;
        }

        _commitJson[m.Name] = Commit;

        jsonToggle.Checked += (_, _) =>
        {
            snapshot = m.Rows.Select(r => new Dictionary<string, string>(r)).ToList();
            jsonBox = new TextBox
            {
                AcceptsReturn = true, TextWrapping = TextWrapping.NoWrap, FontFamily = new FontFamily("Consolas"), MinHeight = 220, MaxHeight = 420,
                Text = ShortenImages(m),
            };
            ScrollViewer.SetHorizontalScrollBarVisibility(jsonBox, ScrollBarVisibility.Auto);
            ScrollViewer.SetVerticalScrollBarVisibility(jsonBox, ScrollBarVisibility.Auto);
            editor.Content = jsonBox;
            m.Send = true;
            send.IsChecked = true;
        };
        jsonToggle.Unchecked += (_, _) =>
        {
            if (!Commit())
            {
                jsonToggle.IsChecked = true;
                return;
            }

            jsonBox = null;
            ShowTable();
        };

        ShowTable();
        return new Expander
        {
            Header = header,
            Content = body,
            IsExpanded = expanded,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
        };
    }

    private const int ImageShortenLength = 120;

    // Photos are huge base64 strings; the JSON view shows a short marker instead so the text box stays responsive.
    private static string ShortenImages(SourceModel m)
    {
        var clone = new SourceModel(new ReportDataSourceInfo(m.Name, m.Kind, m.Columns.Select(c => new ReportFieldInfo(c, m.Types.GetValueOrDefault(c, "string"))).ToList(), null));
        clone.Rows.Clear();
        foreach (var row in m.Rows)
        {
            clone.Rows.Add(row.ToDictionary(kv => kv.Key, kv => kv.Value.Length > ImageShortenLength && kv.Value.StartsWith("data:", StringComparison.Ordinal)
                ? $"{kv.Value[..30]}…[{kv.Value.Length / 1024} KB]" : kv.Value));
        }

        return clone.ToJson(true);
    }

    private static string RestoreShortened(string json, List<Dictionary<string, string>> snapshot, SourceModel m)
    {
        if (!json.Contains("…[", StringComparison.Ordinal))
        {
            return json;
        }

        // Put the real data URIs back where the user left a marker untouched (same row and column as before).
        var probe = new SourceModel(new ReportDataSourceInfo(m.Name, m.Kind, [], null));
        if (!probe.TryLoadJson(json, out _))
        {
            return json;
        }

        for (var i = 0; i < probe.Rows.Count && i < snapshot.Count; i++)
        {
            foreach (var key in probe.Rows[i].Keys.ToList())
            {
                if (probe.Rows[i][key].Contains("…[", StringComparison.Ordinal) && snapshot[i].TryGetValue(key, out var original))
                {
                    probe.Rows[i][key] = original;
                }
            }
        }

        foreach (var c in probe.Columns)
        {
            probe.Types[c] = "string";
        }

        return probe.ToJson(false);
    }

    private FrameworkElement BuildTable(SourceModel m, CheckBox send)
    {
        const double cellWidth = 150;
        var rowsPanel = new StackPanel { Spacing = 4 };

        void Rebuild()
        {
            rowsPanel.Children.Clear();

            if (m.Rows.Count == 1)
            {
                // One record reads better as a form (labels above the fields) than as a one-row table.
                var only = m.Rows[0];
                foreach (var c in m.Columns)
                {
                    var field = new StackPanel { Spacing = 2 };
                    field.Children.Add(new TextBlock { Text = c, FontSize = 12, Opacity = 0.75 });
                    field.Children.Add(m.IsImageField(c) ? ImageCell(m, only, c, 360, send) : TextCell(m, only, c, 360, send));
                    rowsPanel.Children.Add(field);
                }

                var addMore = new Button { Content = "+ Satır ekle (birden fazla kayıt)", HorizontalAlignment = HorizontalAlignment.Left };
                addMore.Click += (_, _) =>
                {
                    m.Rows.Add(m.NewRow());
                    send.IsChecked = true;
                    Rebuild();
                };
                rowsPanel.Children.Add(addMore);
                return;
            }

            var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            foreach (var c in m.Columns)
            {
                header.Children.Add(new TextBlock { Text = c, Width = cellWidth, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis });
            }

            rowsPanel.Children.Add(header);

            foreach (var row in m.Rows.ToList())
            {
                var line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
                foreach (var c in m.Columns)
                {
                    line.Children.Add(m.IsImageField(c) ? ImageCell(m, row, c, cellWidth, send) : TextCell(m, row, c, cellWidth, send));
                }

                var del = new Button { Content = "✕", Padding = new Thickness(8, 4, 8, 4), VerticalAlignment = VerticalAlignment.Top };
                ToolTipService.SetToolTip(del, "Satırı sil");
                del.Click += (_, _) =>
                {
                    m.Rows.Remove(row);
                    if (m.Rows.Count == 0)
                    {
                        m.Rows.Add(m.NewRow());
                    }

                    Rebuild();
                };
                line.Children.Add(del);
                rowsPanel.Children.Add(line);
            }

            var add = new Button { Content = "+ Satır ekle" };
            add.Click += (_, _) =>
            {
                m.Rows.Add(m.NewRow());
                send.IsChecked = true;
                Rebuild();
            };
            rowsPanel.Children.Add(add);
        }

        Rebuild();
        return new ScrollViewer
        {
            Content = rowsPanel,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalScrollMode = ScrollMode.Auto,
            Padding = new Thickness(0, 0, 0, 12),
        };
    }

    private static FrameworkElement TextCell(SourceModel m, Dictionary<string, string> row, string column, double width, CheckBox send)
    {
        var type = m.Types.GetValueOrDefault(column, "string").ToLowerInvariant();
        if (type == "boolean")
        {
            var cb = new CheckBox { Width = width, IsChecked = string.Equals(row[column], "true", StringComparison.OrdinalIgnoreCase) };
            cb.Checked += (_, _) => { row[column] = "true"; send.IsChecked = true; };
            cb.Unchecked += (_, _) => { row[column] = "false"; send.IsChecked = true; };
            return cb;
        }

        var box = new TextBox { Width = width, Text = row[column], PlaceholderText = type is "date" or "datetime" ? "yyyy-aa-gg" : string.Empty };
        box.TextChanged += (_, _) =>
        {
            row[column] = box.Text;
            if (send.IsChecked != true)
            {
                send.IsChecked = true;
            }
        };
        return box;
    }

    private FrameworkElement ImageCell(SourceModel m, Dictionary<string, string> row, string column, double width, CheckBox send)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Width = width };
        var thumb = new Image { Width = 30, Height = 40, Stretch = Stretch.UniformToFill };
        var pick = new Button { Content = "Fotoğraf seç…" };
        var clear = new Button { Content = "✕", Padding = new Thickness(6, 4, 6, 4) };
        ToolTipService.SetToolTip(clear, "Temizle");

        async void ShowThumb()
        {
            var value = row[column];
            thumb.Source = null;
            clear.Visibility = value.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
            pick.Content = value.Length > 0 ? "Değiştir…" : "Fotoğraf seç…";
            thumb.Visibility = value.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
            if (value.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && value.IndexOf("base64,", StringComparison.Ordinal) is var i and > 0)
            {
                try
                {
                    var bmp = new BitmapImage();
                    using var s = new InMemoryRandomAccessStream();
                    await s.WriteAsync(Convert.FromBase64String(value[(i + 7)..]).AsBuffer());
                    s.Seek(0);
                    await bmp.SetSourceAsync(s);
                    thumb.Source = bmp;
                }
                catch (Exception ex) when (ex is FormatException or ArgumentException or System.Runtime.InteropServices.COMException)
                {
                    // not an image we can show; it is still sent as typed
                }
            }
        }

        pick.Click += async (_, _) =>
        {
            var picker = new FileOpenPicker();
            foreach (var ext in new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp" })
            {
                picker.FileTypeFilter.Add(ext);
            }

            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
            var file = await picker.PickSingleFileAsync();
            if (file is null)
            {
                return;
            }

            var bytes = (await FileIO.ReadBufferAsync(file)).ToArray();
            var mime = file.FileType.ToLowerInvariant() switch { ".png" => "image/png", ".gif" => "image/gif", ".bmp" => "image/bmp", ".webp" => "image/webp", _ => "image/jpeg" };
            row[column] = $"data:{mime};base64,{Convert.ToBase64String(bytes)}";
            send.IsChecked = true;
            ShowThumb();
        };
        clear.Click += (_, _) => { row[column] = string.Empty; ShowThumb(); };

        panel.Children.Add(thumb);
        panel.Children.Add(pick);
        panel.Children.Add(clear);
        ShowThumb();
        return panel;
    }

    // ---------------------------------------------------------------- render

    private bool TryCollect(out ReportData? data, out Dictionary<string, object> parameters)
    {
        data = null;
        parameters = new Dictionary<string, object>(StringComparer.Ordinal);

        foreach (var commit in _commitJson.Values)
        {
            if (!commit())
            {
                return false;
            }
        }

        foreach (var (name, read) in _parameterReaders)
        {
            if (read() is { } v)
            {
                parameters[name] = v;
            }
        }

        var send = _sources.Where(s => s.Send).ToList();
        if (send.Count > 0)
        {
            data = new ReportData();
            foreach (var s in send)
            {
                data.AddJson(s.Name, s.ToJson(false));
            }
        }

        return true;
    }

    private async void Preview_Click(object sender, RoutedEventArgs e)
    {
        if (_client is null || _current is null || !TryCollect(out var data, out var parameters))
        {
            return;
        }

        var dpi = int.Parse((string)((ComboBoxItem)DpiCombo.SelectedItem).Tag, CultureInfo.InvariantCulture);
        await RunAsync(async () =>
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var pages = await _client.RenderPagesAsync(_current.Code, data, parameters, ReportFormat.Png, dpi);
            ClearPreview();
            foreach (var page in pages)
            {
                var bmp = new BitmapImage();
                using var stream = new InMemoryRandomAccessStream();
                await stream.WriteAsync(page.Content.AsBuffer());
                stream.Seek(0);
                await bmp.SetSourceAsync(stream);
                var image = new Image { Source = bmp, Stretch = Stretch.Uniform };
                _pages.Add((image, bmp.PixelWidth, dpi));
                PagesHost.Children.Add(new Border
                {
                    Child = image,
                    Background = new SolidColorBrush(Microsoft.UI.Colors.White),
                    BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Gray),
                    BorderThickness = new Thickness(1),
                    HorizontalAlignment = HorizontalAlignment.Left,
                });
            }

            ApplyZoom();
            PageInfo.Text = $"{pages.Count} sayfa · {watch.ElapsedMilliseconds} ms";
            Notify("Önizleme hazır.", InfoBarSeverity.Success, autoClose: true);
        });
    }

    private void ClearPreview()
    {
        PagesHost.Children.Clear();
        _pages.Clear();
        PageInfo.Text = string.Empty;
    }

    private void Zoom_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e) => ApplyZoom();

    private void ApplyZoom()
    {
        foreach (var (image, pixels, dpi) in _pages)
        {
            image.Width = pixels / (dpi / 96.0) * (ZoomSlider.Value / 100.0);
        }
    }

    // ---------------------------------------------------------------- save

    private async void SavePdf_Click(object sender, RoutedEventArgs e) => await SaveAsync(ReportFormat.Pdf, "PDF", ".pdf");

    private async void SaveXlsx_Click(object sender, RoutedEventArgs e) => await SaveAsync(ReportFormat.Xlsx, "Excel", ".xlsx");

    private async void SaveHtml_Click(object sender, RoutedEventArgs e) => await SaveAsync(ReportFormat.Html, "HTML", ".html");

    private async Task SaveAsync(ReportFormat format, string label, string extension)
    {
        if (_client is null || _current is null || !TryCollect(out var data, out var parameters))
        {
            return;
        }

        await RunAsync(async () =>
        {
            var result = await _client.RenderAsync(_current.Code, data, parameters, format);
            var file = await PickSaveAsync(label, extension, Path.GetFileNameWithoutExtension(result.FileName));
            if (file is null)
            {
                return;
            }

            await FileIO.WriteBytesAsync(file, result.Content);
            Notify($"{file.Name} kaydedildi.", InfoBarSeverity.Success, autoClose: true);
            await Windows.System.Launcher.LaunchFileAsync(file);
        });
    }

    private async void SaveJson_Click(object sender, RoutedEventArgs e)
    {
        if (_current is null || !TryCollect(out var data, out var parameters))
        {
            return;
        }

        // The exact request body the application would send: handy to hand to whoever integrates the report.
        var body = new System.Text.StringBuilder("{\n  \"parameters\": ")
            .Append(System.Text.Json.JsonSerializer.Serialize(parameters))
            .Append(",\n  \"data\": {");
        var first = true;
        foreach (var s in _sources.Where(s => s.Send))
        {
            body.Append(first ? "\n    " : ",\n    ").Append('"').Append(s.Name).Append("\": ").Append(s.ToJson(false));
            first = false;
        }

        body.Append("\n  }\n}\n");
        var file = await PickSaveAsync("JSON", ".json", _current.Code + "-veri");
        if (file is not null)
        {
            await FileIO.WriteTextAsync(file, body.ToString());
            Notify($"{file.Name} kaydedildi.", InfoBarSeverity.Success, autoClose: true);
        }

        _ = data;
    }

    private async Task<StorageFile?> PickSaveAsync(string label, string extension, string suggestedName)
    {
        var picker = new FileSavePicker { SuggestedFileName = suggestedName };
        picker.FileTypeChoices.Add(label, new List<string> { extension });
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        return await picker.PickSaveFileAsync();
    }

    // ---------------------------------------------------------------- helpers

    private async Task RunAsync(Func<Task> work)
    {
        Busy.IsActive = true;
        Status.IsOpen = false;
        try
        {
            await work();
        }
        catch (ReportClientException ex)
        {
            Notify($"{(int)ex.StatusCode} — {ex.Message}", InfoBarSeverity.Error);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException or IOException)
        {
            Notify(ex is TaskCanceledException ? "İstek zaman aşımına uğradı." : ex.Message, InfoBarSeverity.Error);
        }
        finally
        {
            Busy.IsActive = false;
        }
    }

    private void Notify(string message, InfoBarSeverity severity, bool autoClose = false)
    {
        Status.Severity = severity;
        Status.Message = message;
        Status.IsOpen = true;
        if (autoClose)
        {
            var timer = DispatcherQueue.CreateTimer();
            timer.Interval = TimeSpan.FromSeconds(4);
            timer.IsRepeating = false;
            timer.Tick += (_, _) => { if (Status.Message == message) { Status.IsOpen = false; } };
            timer.Start();
        }
    }
}
