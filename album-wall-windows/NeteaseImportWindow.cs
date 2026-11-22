using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AlbumWall;

internal sealed class NeteaseImportWindow : Window
{
    private readonly NeteasePlaylist _netease = new();
    private readonly TextBox _link = new();
    private readonly Button _read = new() { Content = "读取歌单", Padding = new Thickness(14, 5, 14, 5) };
    private readonly Button _cancel = new() { Content = "取消", Padding = new Thickness(14, 5, 14, 5) };
    private readonly Button _import = new() { Content = "导入选中封面", Padding = new Thickness(16, 5, 16, 5), IsEnabled = false };
    private readonly CheckBox _selectAll = new() { Content = "全选", IsEnabled = false, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, Text = "粘贴网易云音乐公开歌单的分享链接。" };
    private readonly ProgressBar _progress = new() { Height = 5, Visibility = Visibility.Collapsed };
    private readonly StackPanel _items = new();
    private readonly List<(CheckBox Checkbox, PlaylistCover Cover)> _covers = [];
    private CancellationTokenSource? _scan;
    private string? _temporaryFolder;
    private bool _saving;
    private bool _updatingChecks;
    private bool _closeAfterScan;
    private int _phase;

    public NeteaseImportWindow()
    {
        Title = "网易云歌单 · 导入封面";
        Width = 680;
        Height = 640;
        MinWidth = 450;
        MinHeight = 380;
        FontSize = 14;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var panel = new DockPanel { Margin = new Thickness(22) };
        Content = panel;
        var top = new StackPanel();
        DockPanel.SetDock(top, Dock.Top);
        panel.Children.Add(top);
        top.Children.Add(new Label { Content = "公开歌单分享链接", Target = _link, Padding = new Thickness(0, 0, 0, 6) });

        var input = new DockPanel();
        _read.Margin = new Thickness(8, 0, 0, 0);
        DockPanel.SetDock(_read, Dock.Right);
        input.Children.Add(_read);
        _link.VerticalContentAlignment = VerticalAlignment.Center;
        _link.MaxLength = 4000;
        input.Children.Add(_link);
        top.Children.Add(input);
        _status.Margin = new Thickness(0, 9, 0, 8);
        top.Children.Add(_status);
        top.Children.Add(_progress);

        var footer = new DockPanel { Margin = new Thickness(0, 14, 0, 0) };
        DockPanel.SetDock(footer, Dock.Bottom);
        panel.Children.Add(footer);
        _import.Margin = new Thickness(8, 0, 0, 0);
        DockPanel.SetDock(_import, Dock.Right);
        footer.Children.Add(_import);
        DockPanel.SetDock(_cancel, Dock.Right);
        footer.Children.Add(_cancel);
        footer.Children.Add(_selectAll);

        var listBorder = new Border
        {
            BorderThickness = new Thickness(1),
            BorderBrush = SystemColors.ControlDarkBrush,
            Child = new ScrollViewer { Content = _items, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }
        };
        panel.Children.Add(listBorder);

        _read.Click += async (_, _) => await ScanAsync();
        _link.KeyDown += async (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Enter && _read.IsEnabled)
                await ScanAsync();
        };
        _selectAll.Click += (_, _) => SelectAll();
        _import.Click += async (_, _) => await ImportAsync();
        _cancel.Click += (_, _) =>
        {
            if (_scan is not null)
            {
                _cancel.IsEnabled = false;
                _scan.Cancel();
            }
            else
                Close();
        };
        Closing += OnClosing;
        Closed += (_, _) => ClearTemporaryFolder();
        Loaded += (_, _) => _link.Focus();
    }

    private async Task ScanAsync()
    {
        if (_scan is not null || _saving)
            return;

        ClearTemporaryFolder();
        _items.Children.Clear();
        _covers.Clear();
        UpdateChecks();
        using var scan = new CancellationTokenSource();
        _scan = scan;
        SetBusy(true, "停止读取");
        try
        {
            _status.Text = "正在打开歌单…";
            _progress.IsIndeterminate = true;
            var list = await _netease.ReadPlaylistAsync(_link.Text, scan.Token);
            int total = list.Songs.Length;
            string title = string.IsNullOrEmpty(list.Title) ? "公开歌单" : list.Title;
            var result = await _netease.FindAlbumsAsync(list.Songs, ShowProgress("读取曲目", total), scan.Token);

            _temporaryFolder = Path.Combine(Path.GetTempPath(), "AlbumWall-" + Guid.NewGuid().ToString("N"));
            var prepared = await PlaylistCoverStore.PrepareAsync(_netease, result.Albums, _temporaryFolder,
                ShowProgress("加载封面", result.Albums.Length), scan.Token);
            scan.Token.ThrowIfCancellationRequested();

            foreach (var cover in prepared.Covers)
                AddCover(cover);

            _status.Text = $"{title} · 找到 {prepared.Covers.Length} 张专辑封面";
            if (list.Total > total)
                _status.Text += $"，歌单共 {list.Total} 首，页面可读到 {total} 首不同曲目";
            if (result.MissingSongs > 0)
                _status.Text += $"，{result.MissingSongs} 首曲目未解析到封面";
            if (prepared.Failed > 0)
                _status.Text += $"，{prepared.Failed} 张封面加载失败";
            if (prepared.Covers.Length == 0)
                _status.Text += "。可以重新读取，或试试另一个公开歌单。";
        }
        catch (OperationCanceledException) when (scan.IsCancellationRequested)
        {
            _status.Text = "已停止读取。";
            ClearTemporaryFolder();
        }
        catch (InvalidDataException e)
        {
            _status.Text = e.Message;
        }
        catch (Exception e) when (e is HttpRequestException or IOException or UnauthorizedAccessException or OperationCanceledException)
        {
            _status.Text = "暂时无法读取歌单。请检查网络和链接后重试。";
        }
        finally
        {
            _scan = null;
            SetBusy(false, "取消");
            UpdateChecks();
            if (_closeAfterScan)
                Close();
        }
    }

    private IProgress<int> ShowProgress(string phase, int count)
    {
        int phaseId = ++_phase;
        _progress.IsIndeterminate = false;
        _progress.Maximum = Math.Max(1, count);
        _progress.Value = 0;
        _status.Text = $"{phase} 0 / {count}";
        int shown = 0;
        return new Progress<int>(done =>
        {
            if (done <= shown || _scan is null || phaseId != _phase)
                return;
            shown = done;
            _progress.Value = done;
            _status.Text = $"{phase} {done} / {count}";
        });
    }

    private void AddCover(PlaylistCover cover)
    {
        var checkbox = new CheckBox
        {
            IsChecked = true,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10),
        };
        checkbox.Checked += (_, _) => UpdateChecks();
        checkbox.Unchecked += (_, _) => UpdateChecks();

        var row = new DockPanel { Margin = new Thickness(0, 3, 0, 3) };
        var thumbnail = new Image { Width = 40, Height = 40, Source = cover.Preview, Stretch = Stretch.UniformToFill };
        DockPanel.SetDock(thumbnail, Dock.Left);
        row.Children.Add(thumbnail);
        string title = string.IsNullOrWhiteSpace(cover.Album.Title) ? $"专辑 {cover.Album.Id}" : cover.Album.Title;
        var details = new StackPanel { Margin = new Thickness(9, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        details.Children.Add(new TextBlock { Text = title, FontWeight = FontWeights.Medium });
        if (cover.Album.Artist.Length > 0)
            details.Children.Add(new TextBlock { Text = cover.Album.Artist, Foreground = SystemColors.GrayTextBrush, FontSize = 12 });
        row.Children.Add(details);
        checkbox.Content = row;
        _covers.Add((checkbox, cover));
        _items.Children.Add(checkbox);
    }

    private void SelectAll()
    {
        _updatingChecks = true;
        foreach (var (checkbox, _) in _covers)
            checkbox.IsChecked = _selectAll.IsChecked == true;
        _updatingChecks = false;
        UpdateChecks();
    }

    private void UpdateChecks()
    {
        if (_updatingChecks)
            return;

        int selected = _covers.Count(item => item.Checkbox.IsChecked == true);
        _selectAll.IsEnabled = _covers.Count > 0 && _scan is null && !_saving;
        _selectAll.IsChecked = selected > 0 && selected == _covers.Count;
        _import.IsEnabled = selected > 0 && _scan is null && !_saving;
        _import.Content = selected == 0 ? "导入选中封面" : $"导入 {selected} 张封面";
    }

    private async Task ImportAsync()
    {
        var selected = _covers.Where(item => item.Checkbox.IsChecked == true).Select(item => item.Cover).ToArray();
        if (_scan is not null || _saving || selected.Length == 0)
            return;

        _saving = true;
        SetBusy(true, "取消");
        _cancel.IsEnabled = false;
        _progress.IsIndeterminate = true;
        _status.Text = "正在保存到本地…";
        CoverImportResult? result = null;
        try
        {
            result = await Task.Run(() => PlaylistCoverStore.Import(selected));
            _status.Text = $"已导入 {result.Added} 张，已有 {result.Existing} 张，失败 {result.Failed} 张。";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _status.Text = "无法保存封面。请检查磁盘空间和文件夹权限。";
        }
        finally
        {
            _saving = false;
            SetBusy(false, "取消");
            UpdateChecks();
        }

        if (result is not null && result.Added + result.Existing > 0)
        {
            MessageBox.Show(this, _status.Text, Title, MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
        }
    }

    private void SetBusy(bool busy, string cancelLabel)
    {
        _read.IsEnabled = _link.IsEnabled = _items.IsEnabled = !busy;
        _selectAll.IsEnabled = _import.IsEnabled = false;
        _progress.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        _cancel.IsEnabled = true;
        _cancel.Content = cancelLabel;
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_scan is null && !_saving)
            return;
        if (_scan is not null)
        {
            _closeAfterScan = true;
            _scan.Cancel();
        }
        e.Cancel = true;
    }

    private void ClearTemporaryFolder()
    {
        if (_temporaryFolder is null)
            return;
        try { Directory.Delete(_temporaryFolder, true); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        _temporaryFolder = null;
    }
}
