using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using BrimDeck.Core;
using BrimDeck.Mac.Hosting;
using BrimDeck.Mac.Theme;

namespace BrimDeck.Mac.Views;

public sealed class SettingsWindow : Window
{
    private readonly Session _session;
    private readonly StackPanel _body = new() { Spacing = 14 };
    private readonly StackPanel _nav = new() { Spacing = 4, Margin = new Thickness(12) };
    private int _section;

    public SettingsWindow(Session session)
    {
        _session = session;
        Title = Loc.T("BrimDeck 设置", "BrimDeck Settings");
        Width = 880;
        Height = 620;
        MinWidth = 720;
        MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        FontFamily = Face.Family;
        var root = new Grid();
        root.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(200)));
        root.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        root.Children.Add(_nav);
        var scroll = new ScrollViewer { Content = _body, Margin = new Thickness(8, 16, 16, 16) };
        Grid.SetColumn(scroll, 1);
        root.Children.Add(scroll);
        Content = root;
        ShowSection();
    }

    private void BuildNav(PanelPalette palette)
    {
        _nav.Children.Clear();
        _nav.Children.Add(Face.Text("BrimDeck", 18, Ink.Hex(palette, "textPrimary"), FontWeight.SemiBold));
        _nav.Children.Add(Face.Text(Loc.T("macOS 版", "for macOS"), 12, Ink.Hex(palette, "textTertiary")));
        string[] names = [Loc.T("通用", "General"), Loc.T("收起形态", "Collapsed"), Loc.T("主题", "Theme"), Loc.T("工具", "Tools"), Loc.T("关于", "About")];
        for (int i = 0; i < names.Length; i++)
        {
            int index = i;
            bool on = index == _section;
            var button = new Clickable { CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 8), Child = Face.Text(names[i], 13, Ink.Hex(palette, on ? "textPrimary" : "textSecondary"), on ? FontWeight.SemiBold : FontWeight.Normal) };
            button.Rest(on ? Ink.Brush(Ink.Hex(palette, "fillSelected")) : Brushes.Transparent, Ink.Brush(Ink.Hex(palette, "hover")));
            button.Pressed += (_, _) => { _section = index; ShowSection(); };
            _nav.Children.Add(button);
        }
    }

    private void ShowSection()
    {
        var palette = _session.Palette;
        Background = Ink.Brush(palette.Surface.ToString());
        BuildNav(palette);
        _body.Children.Clear();
        switch (_section)
        {
            case 0: General(palette); break;
            case 1: Collapsed(palette); break;
            case 2: Themes(palette); break;
            case 3: Tools(palette); break;
            default: About(palette); break;
        }
    }

    private void General(PanelPalette palette)
    {
        _body.Children.Add(Heading(Loc.T("通用", "General"), palette));
        _body.Children.Add(Choice(Loc.T("界面语言", "Language"), palette, [
            (Loc.Chinese, Loc.T("简体中文", "Simplified Chinese")),
            (Loc.English, "English")
        ], _session.Settings.Language, value => { _session.Settings.Language = value; _session.Save(); Title = Loc.T("BrimDeck 设置", "BrimDeck Settings"); ShowSection(); }));
        _body.Children.Add(Toggle(Loc.T("登录时启动", "Launch at login"), palette, _session.Settings.LaunchAtStartup, value => { _session.Settings.LaunchAtStartup = value; _session.Save(); }));
        _body.Children.Add(Toggle(Loc.T("用量页", "Usage page"), palette, _session.Settings.UsagePage, value => { _session.Settings.UsagePage = value; _session.Save(); }));
        _body.Children.Add(Toggle(Loc.T("音乐页", "Music page"), palette, _session.Settings.MusicPage, value => { _session.Settings.MusicPage = value; _session.Save(); }));
        _body.Children.Add(Toggle(Loc.T("系统监控页", "System monitor"), palette, _session.Settings.MonitorPage, value => { _session.Settings.MonitorPage = value; _session.Save(); }));
        _body.Children.Add(Toggle(Loc.T("自动同步用量", "Sync usage automatically"), palette, _session.Settings.UsageAutoSync, value => { _session.Settings.UsageAutoSync = value; _session.Save(); }));
        _body.Children.Add(Choice(Loc.T("面板大小", "Panel size"), palette, [
            ("compact", Loc.T("紧凑", "Compact")),
            ("standard", Loc.T("标准", "Standard")),
            ("spacious", Loc.T("宽敞", "Spacious"))
        ], "standard", value =>
        {
            var size = value switch { "compact" => PanelSize.Compact, "spacious" => PanelSize.Spacious, _ => PanelSize.Standard };
            var (width, height) = _session.Settings.PresetSize(size);
            _session.Settings.Width = width;
            _session.Settings.Height = height;
            var monitor = MonitorSizes.For(size);
            _session.Settings.MonitorWidth = monitor.Width;
            _session.Settings.MonitorHeight = monitor.Height;
            _session.Save();
        }));
    }

    private void Collapsed(PanelPalette palette)
    {
        _body.Children.Add(Heading(Loc.T("收起形态", "Collapsed style"), palette));
        _body.Children.Add(Choice(Loc.T("样式", "Style"), palette, [
            (nameof(CompactStyle.Notch), Loc.T("刘海", "Notch")),
            (nameof(CompactStyle.Capsule), Loc.T("胶囊", "Capsule")),
            (nameof(CompactStyle.Line), Loc.T("指示条", "Indicator"))
        ], _session.Settings.Style.ToString(), value =>
        {
            if (Enum.TryParse<CompactStyle>(value, out var style)) { _session.Settings.Style = style; _session.Save(); }
        }));
        var styleNow = _session.Settings.Style;
        _body.Children.Add(Toggle(Loc.T("配额环", "Quota rings"), palette, _session.Settings.ShowsSummary(styleNow), value =>
        {
            if (styleNow == CompactStyle.Capsule) _session.Settings.CapsuleSummary = value; else _session.Settings.NotchSummary = value;
            _session.Save();
        }));
        _body.Children.Add(Toggle(Loc.T("音乐", "Music"), palette, _session.Settings.ShowsMusic(styleNow), value =>
        {
            if (styleNow == CompactStyle.Capsule) _session.Settings.CapsuleMusic = value; else _session.Settings.NotchMusic = value;
            _session.Save();
        }));
        _body.Children.Add(Toggle(Loc.T("时间", "Clock"), palette, _session.Settings.ShowsClock(styleNow), value =>
        {
            if (styleNow == CompactStyle.Capsule) _session.Settings.CapsuleClock = value; else _session.Settings.NotchClock = value;
            _session.Save();
        }));
        _body.Children.Add(Toggle(Loc.T("24 小时制", "24-hour clock"), palette, _session.Settings.Clock24Hour, value => { _session.Settings.Clock24Hour = value; _session.Save(); }));
    }

    private void Themes(PanelPalette palette)
    {
        _body.Children.Add(Heading(Loc.T("面板主题", "Panel theme"), palette));
        foreach (var theme in PanelThemes.BuiltIns)
        {
            var row = new Clickable { CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 8) };
            bool on = string.Equals(_session.Settings.PanelTheme, theme.Id, StringComparison.OrdinalIgnoreCase);
            row.Rest(on ? Ink.Brush(Ink.Hex(palette, "fillSelected")) : Brushes.Transparent, Ink.Brush(Ink.Hex(palette, "hover")));
            var line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
            var resolved = PanelThemes.Resolve(theme);
            line.Children.Add(new Border { Width = 18, Height = 18, CornerRadius = new CornerRadius(4), Background = Ink.Brush(resolved.Surface.ToString()) });
            line.Children.Add(Face.Text(theme.DisplayName, 13, Ink.Hex(palette, "textPrimary"), on ? FontWeight.SemiBold : FontWeight.Normal));
            row.Child = line;
            var id = theme.Id;
            row.Pressed += (_, _) => { _session.Settings.PanelTheme = id; _session.Save(); ShowSection(); };
            _body.Children.Add(row);
        }
    }

    private void Tools(PanelPalette palette)
    {
        _body.Children.Add(Heading(Loc.T("工具", "Tools"), palette));
        _body.Children.Add(Face.Text(Loc.T("已登录的工具会自动读取，最多同时显示 7 个。", "Signed-in tools are read automatically, up to 7 at once."), 12, Ink.Hex(palette, "textTertiary")));
        foreach (var id in ProviderCatalog.AutomaticSources)
        {
            var source = id;
            bool enabled = _session.Settings.Apps.Any(app => app.QuotaSource == source && app.Enabled);
            _body.Children.Add(Toggle(ProviderCatalog.Name(source), palette, enabled, value =>
            {
                _session.Settings.SetEnabled(source, value);
                _session.Save();
                _ = _session.RefreshUsageAsync();
            }));
        }
    }

    private void About(PanelPalette palette)
    {
        _body.Children.Add(Heading("BrimDeck", palette));
        _body.Children.Add(Face.Text(Loc.T($"版本 {ApplicationVersion.Current}", $"Version {ApplicationVersion.Current}"), 13, Ink.Hex(palette, "textSecondary")));
        _body.Children.Add(Face.Text(Loc.T("屏幕顶部的灵动岛，集中显示 AI 工具额度、正在播放的音乐和系统占用。", "A Dynamic Island at the top of the screen for AI quotas, the music playing and system usage."), 13, Ink.Hex(palette, "textSecondary")));
        _body.Children.Add(Face.Text(Loc.T("登录状态只在内存中使用。手动填写的密钥保存在登录钥匙串里。", "Sign-in tokens stay in memory. Keys you enter are kept in the login keychain."), 13, Ink.Hex(palette, "textTertiary")));
        _body.Children.Add(Face.Text(Loc.T("音乐目前接入“音乐”和 Spotify。系统监控读取本机 CPU、内存、网速和占用最高的应用。", "Music currently follows the Music app and Spotify. The monitor reads CPU, memory, network and the busiest apps."), 13, Ink.Hex(palette, "textTertiary")));
    }

    private static Control Heading(string text, PanelPalette palette) => Face.Text(text, 20, Ink.Hex(palette, "textPrimary"), FontWeight.SemiBold);

    private static Control Toggle(string label, PanelPalette palette, bool value, Action<bool> changed)
    {
        var box = new CheckBox { Content = label, IsChecked = value, Foreground = Ink.Brush(Ink.Hex(palette, "textPrimary")), FontFamily = Face.Family, FontSize = 13 };
        box.IsCheckedChanged += (_, _) => { if (box.IsChecked == value) return; value = box.IsChecked == true; changed(value); };
        return box;
    }

    private static Control Choice(string label, PanelPalette palette, (string Value, string Name)[] options, string selected, Action<string> changed)
    {
        var row = new StackPanel { Spacing = 4 };
        row.Children.Add(Face.Text(label, 12, Ink.Hex(palette, "textTertiary")));
        var combo = new ComboBox { HorizontalAlignment = HorizontalAlignment.Left, MinWidth = 220, FontFamily = Face.Family };
        foreach (var option in options) combo.Items.Add(new ComboBoxItem { Content = option.Name, Tag = option.Value });
        combo.SelectedIndex = Math.Max(0, Array.FindIndex(options, option => option.Value == selected));
        combo.SelectionChanged += (_, _) =>
        {
            if (combo.SelectedItem is ComboBoxItem { Tag: string value } && value != selected) { selected = value; changed(value); }
        };
        row.Children.Add(combo);
        return row;
    }
}
