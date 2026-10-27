using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using BrimDeck.Core;
using BrimDeck.Mac.Drawing;
using BrimDeck.Mac.Hosting;
using BrimDeck.Mac.Platform;
using BrimDeck.Mac.Theme;

namespace BrimDeck.Mac.Views;

public sealed class IslandWindow : Window
{
    private readonly Session _session;
    private readonly IslandChrome _chrome = new();
    private readonly Grid _compact = new();
    private readonly Grid _expanded = new();
    private readonly UsageBoard _usage = new();
    private readonly MusicBoard _music = new();
    private readonly MonitorBoard _monitor = new();
    private readonly TextBlock _clock;
    private readonly DispatcherTimer _timer;
    private bool _open, _pointerInside, _capturing;
    private double _blend, _blendFrom;
    private long _blendStart;
    private DateTimeOffset? _openAt, _closeAt, _nextPoll;
    private SettingsWindow? _settings;
    private Screen? _screen;

    public IslandWindow(Session session)
    {
        _session = session;
        Title = "BrimDeck";
        WindowDecorations = WindowDecorations.None;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.Manual;
        FontFamily = Face.Family;
        _clock = Face.Text("", 12, "#9EEBEBF5");
        _clock.HorizontalAlignment = HorizontalAlignment.Center;
        _clock.VerticalAlignment = VerticalAlignment.Center;
        _clock.IsHitTestVisible = false;
        var root = new Grid();
        var layers = new Grid();
        layers.Children.Add(_compact);
        layers.Children.Add(_expanded);
        _chrome.Host.Children.Add(layers);
        root.Children.Add(_chrome);
        Content = root;
        _usage.ShowAgain += () => { if (_session.Page == DeckPage.Usage) _usage.Show(_session, OpenDetails); };
        _chrome.PointerEntered += (_, _) => { _pointerInside = true; _closeAt = null; _openAt = DateTimeOffset.Now.AddMilliseconds(_session.Settings.OpenDelay); };
        _chrome.PointerExited += (_, _) => { _pointerInside = false; _openAt = null; _closeAt = DateTimeOffset.Now.AddMilliseconds(Math.Max(80, _session.Settings.CloseDelay)); };
        KeyDown += (_, e) => { if (e.Key == Key.Escape) SetOpen(false); };
        _session.Changed += () => Dispatcher.UIThread.Post(Refresh);
        _session.RevealRequested += () => Dispatcher.UIThread.Post(() => { Show(); SetOpen(true); });
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += (_, _) => OnTick();
        Opened += (_, _) => Begin();
    }

    public void Reveal()
    {
        _session.Hide(false);
        Show();
        Activate();
        SetOpen(true);
    }

    public void ToggleHidden() => _session.Hide(!_session.Hidden);

    public void OpenSettings()
    {
        if (_settings is { } existing) { existing.Activate(); return; }
        _settings = new SettingsWindow(_session);
        _settings.Closed += (_, _) => _settings = null;
        _settings.Show();
    }

    private void Begin()
    {
        _screen = Screens.Primary ?? Screens.All.FirstOrDefault();
        PositionTo(0);
        Refresh();
        _timer.Start();
        if (!_session.Preview) _ = _session.RefreshUsageAsync();
        if (_session.Options.ScreenshotDirectory is { } directory) _ = CaptureAsync(directory);
    }

    private void OnTick()
    {
        if (!_capturing && _openAt is { } open && DateTimeOffset.Now >= open && _pointerInside) { _openAt = null; SetOpen(true); }
        if (!_capturing && _closeAt is { } close && DateTimeOffset.Now >= close && !_pointerInside) { _closeAt = null; SetOpen(false); }
        double duration = _session.Settings.Animations ? Math.Max(0.12, _session.Settings.AnimationDuration / 1000d) : 0.01;
        double elapsed = (Stopwatch.GetTimestamp() - _blendStart) / (double)Stopwatch.Frequency;
        double t = Math.Clamp(elapsed / duration, 0, 1);
        double target = _open ? 1 : 0;
        _blend = _blendFrom + (target - _blendFrom) * Curves.Bounce(t);
        if (t >= 1) _blend = target;
        PositionTo(_blend);
        _clock.Text = Format.Clock(DateTimeOffset.Now, _session.Settings.Clock24Hour);
        if (_session.Page == DeckPage.Music) _music.Show(_session);
        bool moving = t < 1;
        bool live = _session.Track?.State == MediaState.Playing;
        _timer.Interval = moving || live ? TimeSpan.FromMilliseconds(16) : TimeSpan.FromMilliseconds(200);
        if (_nextPoll is null || DateTimeOffset.Now >= _nextPoll)
        {
            _nextPoll = DateTimeOffset.Now.AddSeconds(1);
            _ = _session.TickAsync();
        }
    }

    private void SetOpen(bool open)
    {
        if (_open == open && Math.Abs(_blend - (open ? 1 : 0)) < 0.001) return;
        _open = open;
        _blendFrom = _blend;
        _blendStart = Stopwatch.GetTimestamp();
        _timer.Interval = TimeSpan.FromMilliseconds(16);
    }

    private void PositionTo(double blend)
    {
        var screen = _screen ?? Screens.Primary ?? Screens.All.FirstOrDefault();
        if (screen is null) return;
        _screen = screen;
        double scale = screen.Scaling <= 0 ? 1 : screen.Scaling;
        var bounds = screen.Bounds;
        var work = screen.WorkingArea;
        var monitor = new DipRect(0, 0, bounds.Width / scale, bounds.Height / scale);
        var area = new DipRect((work.X - bounds.X) / scale, (work.Y - bounds.Y) / scale, work.Width / scale, work.Height / scale);
        var behavior = _session.Behavior;
        var style = behavior == WindowBehavior.Line ? CompactStyle.Line : _session.Settings.Style;
        if (_session.Hidden || behavior == WindowBehavior.Hide) { IsVisible = false; return; }
        IsVisible = true;
        var shape = CollapsedGeometry.Resolve(_session.Settings, style, monitor, area, scale);
        var page = DeckPages.Resolve(_session.Settings, _session.Page);
        var (pageWidth, pageHeight) = DeckPages.Size(_session.Settings, page);
        double shown = Math.Clamp(blend, 0, 1.04);
        double width = Lerp(shape.Surface.Width, pageWidth, shown);
        double height = Lerp(shape.Surface.Height, pageHeight, shown);
        var placement = CollapsedGeometry.Panel(shape, area, pageHeight);
        double top = Lerp(shape.Surface.Y, placement.Top, shown);
        double collapsedCenter = shape.Surface.X + shape.Surface.Width / 2;
        double x = collapsedCenter - width / 2;
        Width = Math.Max(1, width);
        Height = Math.Max(1, height);
        Position = new PixelPoint(bounds.X + (int)Math.Round(x * scale), bounds.Y + (int)Math.Round(top * scale));
        double shoulder = style == CompactStyle.Notch ? Lerp(shape.Shoulder, 0, shown) : 0;
        double bottom = Lerp(shape.BottomRadius, PanelHeader.PanelRadius, shown);
        bool floating = placement.Top > 0.5 && shown > 0.5;
        _chrome.Shoulder = shoulder;
        _chrome.TopLeft = floating ? bottom : Lerp(shape.TopRadius, 0, shown);
        _chrome.TopRight = floating ? bottom : Lerp(shape.TopRadius, 0, shown);
        _chrome.BottomLeft = bottom;
        _chrome.BottomRight = bottom;
        _chrome.Fill = Ink.Brush(_session.Palette.Surface.ToString());
        _chrome.Rim = Ink.Brush(Ink.Hex(_session.Palette, "rim"));
        _chrome.Width = Width;
        _chrome.Height = Height;
        _chrome.Redraw();
        double compact = shown < 0.2 ? 1 : shown > 0.55 ? 0 : 1 - (shown - 0.2) / 0.35;
        double expanded = shown < 0.45 ? 0 : shown > 0.9 ? 1 : (shown - 0.45) / 0.45;
        _compact.Opacity = compact;
        _compact.IsVisible = compact > 0.02 && style != CompactStyle.Line;
        _expanded.Opacity = expanded;
        _expanded.IsVisible = expanded > 0.02;
        _expanded.Width = pageWidth;
        _expanded.Height = pageHeight;
        _expanded.HorizontalAlignment = HorizontalAlignment.Center;
        _expanded.VerticalAlignment = VerticalAlignment.Top;
    }

    private void Refresh()
    {
        var palette = _session.Palette;
        _clock.Foreground = Ink.Brush(Ink.Hex(palette, "textTertiary"));
        BuildCompact(palette);
        BuildExpanded(palette);
        PositionTo(_blend);
    }

    private void BuildCompact(PanelPalette palette)
    {
        _compact.Children.Clear();
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(18, 0)
        };
        if (_session.Settings.ShowsSummary(_session.Settings.Style))
        {
            foreach (var column in DashboardUsage.Columns(_session.Settings, _session.Snapshots))
            {
                double used = column.Quota.Metrics.Select(metric => metric.UsedPercent ?? 0).DefaultIfEmpty(0).Max();
                row.Children.Add(new QuotaRing
                {
                    Width = 16, Height = 16,
                    Percent = used,
                    Color = Ink.Brush(Ink.Quota(palette, column.Entry, used)),
                    Track = Ink.Brush(Ink.Hex(palette, "ringTrack"))
                });
            }
        }
        if (_session.Settings.ShowsMusic(_session.Settings.Style) && _session.Track is { } track)
        {
            string text = _session.Settings.MusicText == CompactMusicText.Lyrics && track.CurrentLyric is { Length: > 0 } lyric ? lyric : track.Title;
            if (text.Length > 0)
            {
                var title = Face.Text(text, 12, Ink.Hex(palette, "textPrimary"));
                title.MaxWidth = 160;
                row.Children.Add(title);
            }
            row.Children.Add(new Equalizer { Width = 16, Height = 12, Playing = track.State == MediaState.Playing, Color = Ink.Brush(Ink.Hex(palette, "equalizer")), VerticalAlignment = VerticalAlignment.Center });
        }
        if (_session.Settings.ShowsClock(_session.Settings.Style)) row.Children.Add(_clock);
        _compact.Children.Add(row);
    }

    private void BuildExpanded(PanelPalette palette)
    {
        _expanded.Children.Clear();
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition(new GridLength(36)));
        root.RowDefinitions.Add(new RowDefinition(GridLength.Star));
        root.Children.Add(Toolbar(palette));
        var page = DeckPages.Resolve(_session.Settings, _session.Page);
        Control body = page switch
        {
            DeckPage.Music => _music,
            DeckPage.Monitor => _monitor,
            _ => _usage
        };
        if (page == DeckPage.Usage) _usage.Show(_session, OpenDetails);
        else if (page == DeckPage.Music) _music.Show(_session);
        else _monitor.Show(_session.Monitor, palette);
        if (body.Parent is Panel previous) previous.Children.Remove(body);
        Grid.SetRow(body, 1);
        root.Children.Add(body);
        _expanded.Children.Add(root);
    }

    private Control Toolbar(PanelPalette palette)
    {
        var bar = new Grid { Margin = new Thickness(12, 0, 12, 0) };
        bar.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        bar.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        bar.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        var pages = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        var host = new Border
        {
            Background = Ink.Brush(Ink.Hex(palette, "switchFill")),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(2),
            Child = pages
        };
        void Add(DeckPage page, string glyph)
        {
            if (!DeckPages.Shown(_session.Settings, page)) return;
            bool on = DeckPages.Resolve(_session.Settings, _session.Page) == page;
            var button = new Clickable
            {
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 3),
                Child = Face.Text(glyph, 13, on ? Ink.Hex(palette, "textPrimary") : Ink.Hex(palette, "textTertiary"))
            };
            button.Rest(on ? Ink.Brush(Ink.Hex(palette, "fillSelected")) : Brushes.Transparent, Ink.Brush(Ink.Hex(palette, "hover")));
            button.Pressed += (_, _) => _session.Select(page);
            pages.Children.Add(button);
        }
        Add(DeckPage.Usage, Loc.T("用量", "Usage"));
        Add(DeckPage.Music, Loc.T("音乐", "Music"));
        Add(DeckPage.Monitor, Loc.T("监控", "Monitor"));
        bar.Children.Add(host);
        var clock = Face.Text(Format.Clock(DateTimeOffset.Now, _session.Settings.Clock24Hour), 12, Ink.Hex(palette, "textTertiary"));
        clock.HorizontalAlignment = HorizontalAlignment.Center;
        clock.Name = "ToolbarClock";
        Grid.SetColumn(clock, 1);
        bar.Children.Add(clock);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        var sync = new Clickable { CornerRadius = new CornerRadius(8), Padding = new Thickness(8, 3), Child = Face.Text("↻", 14, Ink.Hex(palette, "textSecondary")) };
        sync.Rest(Brushes.Transparent, Ink.Brush(Ink.Hex(palette, "hover")));
        sync.Pressed += async (_, _) => await _session.RefreshUsageAsync();
        var settings = new Clickable { CornerRadius = new CornerRadius(8), Padding = new Thickness(8, 3), Child = Face.Text("⚙", 14, Ink.Hex(palette, "textSecondary")) };
        settings.Rest(Brushes.Transparent, Ink.Brush(Ink.Hex(palette, "hover")));
        settings.Pressed += (_, _) => OpenSettings();
        actions.Children.Add(sync);
        actions.Children.Add(settings);
        Grid.SetColumn(actions, 2);
        bar.Children.Add(actions);
        return bar;
    }

    private void OpenDetails(ProviderSnapshot snapshot, int days)
    {
        var window = new DetailsWindow(_session, snapshot, days);
        window.Show();
    }

    private async Task CaptureAsync(string directory)
    {
        if (_capturing) return;
        _capturing = true;
        try
        {
            Directory.CreateDirectory(directory);
            await Task.Delay(400);
            _open = true;
            _blend = _blendFrom = 1;
            _blendStart = Stopwatch.GetTimestamp() - Stopwatch.Frequency;
            PositionTo(1);
            Refresh();
            await Task.Delay(500);
            await LayoutFrame();
            SaveShot(Path.Combine(directory, "usage.png"));
            _session.Select(DeckPage.Music);
            Refresh();
            await LayoutFrame();
            SaveShot(Path.Combine(directory, "music.png"));
            _session.Select(DeckPage.Monitor);
            Refresh();
            await LayoutFrame();
            SaveShot(Path.Combine(directory, "monitor.png"));
            _open = false;
            _blend = _blendFrom = 0;
            _blendStart = Stopwatch.GetTimestamp() - Stopwatch.Frequency;
            PositionTo(0);
            Refresh();
            await LayoutFrame();
            SaveShot(Path.Combine(directory, "notch.png"));
            OpenSettings();
            await LayoutFrame();
            if (_settings is not null) SaveControl(_settings, Path.Combine(directory, "settings.png"));
        }
        catch (Exception ex) { AppPaths.LogCrash(ex); }
        finally
        {
            if (Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime life) life.Shutdown();
        }
    }

    private async Task LayoutFrame()
    {
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);
        UpdateLayout();
        await Task.Delay(50);
    }

    private void SaveShot(string path) => SaveControl(this, path);

    private static void SaveControl(Control control, string path)
    {
        int width = Math.Max(1, (int)control.Bounds.Width);
        int height = Math.Max(1, (int)control.Bounds.Height);
        var bitmap = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
        bitmap.Render(control);
        bitmap.Save(path, PngBitmapEncoderOptions.Default);
    }

    private static double Lerp(double from, double to, double t) => from + (to - from) * t;
}
