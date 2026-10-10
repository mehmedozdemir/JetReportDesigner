using JetReportDesigner.Client;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RaporOnizleyici.Services;

namespace RaporOnizleyici;

/// <summary>Add, edit, delete and test the systems (UAT, Prod…) the previewer can connect to.</summary>
public sealed class SystemsDialog : ContentDialog
{
    private readonly List<SystemEntry> _items;
    private readonly ListView _list = new() { Width = 200, Height = 300, SelectionMode = ListViewSelectionMode.Single };
    private readonly TextBox _name = new() { Header = "Ad", PlaceholderText = "UAT, Prod…" };
    private readonly TextBox _url = new() { Header = "Adres", PlaceholderText = "http://raporlar.firma.local:8081" };
    private readonly PasswordBox _key = new() { Header = "API anahtarı", PlaceholderText = "jrd_…" };
    private readonly TextBlock _testResult = new() { TextWrapping = TextWrapping.Wrap, MaxWidth = 380 };
    private SystemEntry? _editing;
    private bool _loading;

    public SystemsDialog(IEnumerable<SystemEntry> systems, SystemEntry? selected)
    {
        Title = "Sistemler";
        PrimaryButtonText = "Kaydet";
        CloseButtonText = "Vazgeç";
        DefaultButton = ContentDialogButton.Primary;

        _items = systems.Select(s => new SystemEntry { Id = s.Id, Name = s.Name, Url = s.Url, ApiKey = s.ApiKey }).ToList();

        var add = new Button { Content = "+ Yeni sistem", HorizontalAlignment = HorizontalAlignment.Stretch };
        add.Click += (_, _) =>
        {
            var s = new SystemEntry { Name = "Yeni sistem" };
            _items.Add(s);
            RefreshList(s);
        };

        var left = new StackPanel { Spacing = 8, Width = 200 };
        left.Children.Add(_list);
        left.Children.Add(add);

        var delete = new Button { Content = "Sil" };
        delete.Click += (_, _) =>
        {
            if (_editing is null)
            {
                return;
            }

            var index = _items.IndexOf(_editing);
            _items.Remove(_editing);
            _editing = null;
            RefreshList(_items.Count == 0 ? null : _items[Math.Min(index, _items.Count - 1)]);
        };
        var test = new Button { Content = "Bağlantıyı dene" };
        test.Click += async (_, _) => await TestAsync();

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        buttons.Children.Add(test);
        buttons.Children.Add(delete);

        var right = new StackPanel { Spacing = 10, Width = 400 };
        right.Children.Add(_name);
        right.Children.Add(_url);
        right.Children.Add(_key);
        right.Children.Add(buttons);
        right.Children.Add(_testResult);
        right.Children.Add(new TextBlock
        {
            Text = "API anahtarı bu bilgisayarda yalnızca sizin Windows hesabınızın çözebileceği şekilde şifreli saklanır. Anahtarı sunucudaki Organizasyon → API anahtarları ekranından oluşturun.",
            TextWrapping = TextWrapping.Wrap, FontSize = 12, Opacity = 0.65,
        });

        var layout = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 20 };
        layout.Children.Add(left);
        layout.Children.Add(right);
        Content = layout;

        _list.SelectionChanged += (_, _) => Edit(_list.SelectedItem is ListViewItem { Tag: SystemEntry s } ? s : null);
        _name.TextChanged += (_, _) => { if (!_loading && _editing is not null) { _editing.Name = _name.Text; UpdateListTitle(); } };
        _url.TextChanged += (_, _) => { if (!_loading && _editing is not null) { _editing.Url = _url.Text; } };
        _key.PasswordChanged += (_, _) => { if (!_loading && _editing is not null) { _editing.ApiKey = _key.Password; } };

        Selected = selected is null ? null : _items.FirstOrDefault(s => s.Id == selected.Id);
        RefreshList(Selected ?? _items.FirstOrDefault());
        if (_items.Count == 0)
        {
            add.Focus(FocusState.Programmatic);
        }

        PrimaryButtonClick += (_, args) =>
        {
            var bad = _items.FirstOrDefault(s => string.IsNullOrWhiteSpace(s.Name) || !Uri.TryCreate(s.Url.Trim(), UriKind.Absolute, out var u) || u.Scheme is not ("http" or "https"));
            if (bad is not null)
            {
                _testResult.Text = $"“{bad.Name}” için ad ve http(s):// ile başlayan geçerli bir adres girin.";
                RefreshList(bad);
                args.Cancel = true;
            }
        };
    }

    public IReadOnlyList<SystemEntry> Result => _items;

    public SystemEntry? Selected { get; private set; }

    private void RefreshList(SystemEntry? select)
    {
        _list.Items.Clear();
        foreach (var s in _items)
        {
            _list.Items.Add(new ListViewItem { Content = s.Name, Tag = s });
        }

        var item = _list.Items.OfType<ListViewItem>().FirstOrDefault(i => ReferenceEquals(i.Tag, select));
        _list.SelectedItem = item;
        if (item is null)
        {
            Edit(null);
        }
    }

    private void UpdateListTitle()
    {
        if (_list.SelectedItem is ListViewItem item && _editing is not null)
        {
            item.Content = string.IsNullOrWhiteSpace(_editing.Name) ? "(adsız)" : _editing.Name;
        }
    }

    private void Edit(SystemEntry? s)
    {
        _loading = true;
        _editing = s;
        Selected = s;
        _name.Text = s?.Name ?? string.Empty;
        _url.Text = s?.Url ?? string.Empty;
        _key.Password = s?.ApiKey ?? string.Empty;
        var enabled = s is not null;
        _name.IsEnabled = _url.IsEnabled = _key.IsEnabled = enabled;
        _testResult.Text = string.Empty;
        _loading = false;
    }

    private async Task TestAsync()
    {
        if (_editing is null)
        {
            return;
        }

        _testResult.Text = "Deneniyor…";
        try
        {
            using var client = new JetReportClient(new JetReportClientOptions { BaseUrl = _editing.Url.Trim(), ApiKey = _editing.ApiKey.Trim(), Timeout = TimeSpan.FromSeconds(15) });
            var reports = await client.ListReportsAsync();
            _testResult.Text = $"Bağlandı — {reports.Count} rapor görünüyor.";
        }
        catch (ReportClientException ex)
        {
            _testResult.Text = $"{(int)ex.StatusCode}: {ex.Message}";
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or ArgumentException or UriFormatException)
        {
            _testResult.Text = ex is TaskCanceledException ? "Zaman aşımı: adrese ulaşılamadı." : ex.Message;
        }
    }
}
