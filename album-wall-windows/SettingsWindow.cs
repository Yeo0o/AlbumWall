using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace AlbumWall;

internal sealed class SettingsWindow : Window
{
    private readonly ComboBox _rows = new();
    private readonly TextBox _seconds = new();
    private readonly TextBox _folder = new() { IsReadOnly = true };
    private readonly CheckBox _netease = new() { Content = "显示已导入的网易云封面" };

    public SettingsWindow(Settings settings)
    {
        Title = "唱片墙 · 屏幕保护程序设置";
        Width = 500;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        FontSize = 14;

        var panel = new StackPanel { Margin = new Thickness(24) };
        Content = panel;

        for (int rows = 3; rows <= 8; rows++)
            _rows.Items.Add(rows);
        _rows.SelectedItem = settings.Rows;
        _rows.MinWidth = 92;
        _rows.HorizontalAlignment = HorizontalAlignment.Left;
        AddField(panel, "封面行数", _rows);

        _seconds.Text = (settings.IntervalMs / 1000d).ToString("0.###", CultureInfo.CurrentCulture);
        _seconds.MinWidth = 92;
        _seconds.HorizontalAlignment = HorizontalAlignment.Left;
        AddField(panel, "切换间隔（秒）", _seconds);

        var folderRow = new DockPanel { LastChildFill = true };
        var browse = new Button { Content = "选择文件夹", Padding = new Thickness(8, 2, 8, 2) };
        DockPanel.SetDock(browse, Dock.Right);
        browse.Margin = new Thickness(8, 0, 0, 0);
        browse.Click += (_, _) => Browse();
        folderRow.Children.Add(browse);
        _folder.Text = settings.CoverFolder;
        folderRow.Children.Add(_folder);
        AddField(panel, "封面图片文件夹", folderRow, _folder);

        var neteaseRow = new DockPanel { Margin = new Thickness(0, 15, 0, 0) };
        var import = new Button { Content = "导入网易云歌单…", Padding = new Thickness(8, 2, 8, 2) };
        DockPanel.SetDock(import, Dock.Right);
        neteaseRow.Children.Add(import);
        _netease.IsChecked = settings.UseNeteaseCovers;
        _netease.VerticalAlignment = VerticalAlignment.Center;
        neteaseRow.Children.Add(_netease);
        panel.Children.Add(neteaseRow);
        import.Click += (_, _) =>
        {
            var window = new NeteaseImportWindow { Owner = this };
            if (window.ShowDialog() == true)
                _netease.IsChecked = true;
        };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 20, 0, 0)
        };
        panel.Children.Add(buttons);
        var useDemo = AddButton(buttons, "使用示例封面");
        useDemo.Click += (_, _) =>
        {
            _folder.Clear();
            _netease.IsChecked = false;
        };
        var cancel = AddButton(buttons, "取消");
        cancel.IsCancel = true;
        cancel.Click += (_, _) => Close();
        var save = AddButton(buttons, "保存");
        save.IsDefault = true;
        save.Click += (_, _) => Save();
    }

    private static void AddField(Panel panel, string title, FrameworkElement element, UIElement? target = null)
    {
        var label = new Label { Content = title, Target = target ?? element, Padding = new Thickness(0) };
        label.Margin = new Thickness(0, panel.Children.Count == 0 ? 0 : 14, 0, 5);
        panel.Children.Add(label);
        panel.Children.Add(element);
    }

    private static Button AddButton(Panel panel, string title)
    {
        var button = new Button
        {
            Content = title,
            Padding = new Thickness(12, 4, 12, 4),
            Margin = new Thickness(8, 0, 0, 0)
        };
        panel.Children.Add(button);
        return button;
    }

    private void Browse()
    {
        var picker = new OpenFolderDialog { Title = "选择存放封面图片的文件夹" };
        if (Directory.Exists(_folder.Text))
            picker.InitialDirectory = _folder.Text;
        if (picker.ShowDialog(this) == true)
            _folder.Text = picker.FolderName;
    }

    private void Save()
    {
        if (!double.TryParse(_seconds.Text, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingWhite |
            NumberStyles.AllowTrailingWhite, CultureInfo.CurrentCulture, out double seconds) ||
            !double.IsFinite(seconds) || seconds < 1 || seconds > 30)
        {
            MessageBox.Show(this, "请输入 1 到 30 秒之间的间隔。", Title, MessageBoxButton.OK, MessageBoxImage.Information);
            _seconds.Focus();
            return;
        }

        try
        {
            new Settings
            {
                Rows = (int)_rows.SelectedItem,
                IntervalMs = (int)Math.Round(seconds * 1000),
                CoverFolder = _folder.Text,
                UseNeteaseCovers = _netease.IsChecked == true
            }.Save();
            Close();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, "无法保存设置。\n" + ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
