using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using BrimDeck.Core;
using BrimDeck.Mac.Hosting;
using BrimDeck.Mac.Theme;

namespace BrimDeck.Mac.Views;

public sealed class MusicBoard : Grid
{
    private TextBlock? _title, _artist, _lyric, _time, _length;
    private Meter? _seek;
    private Border? _cover;
    private TextBlock? _coverLetter;
    private Session? _session;

    public MusicBoard()
    {
        ColumnDefinitions.Add(new ColumnDefinition(new GridLength(92)));
        ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        RowDefinitions.Add(new RowDefinition(GridLength.Star));
        Margin = new Thickness(16, 4, 18, 12);
    }

    public void Show(Session session)
    {
        _session = session;
        if (Children.Count == 0) Build(session.Palette);
        Apply(session);
    }

    private void Build(PanelPalette palette)
    {
        Children.Clear();
        _cover = new Border
        {
            Width = 76, Height = 76, CornerRadius = new CornerRadius(10),
            Background = Ink.Brush(Ink.Hex(palette, "coverPlaceholder")),
            BorderBrush = Ink.Brush(Ink.Hex(palette, "coverOutline")),
            BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = _coverLetter = Face.Text("", 28, Ink.Hex(palette, "textSecondary"), FontWeight.SemiBold, TextAlignment.Center)
        };
        _coverLetter.HorizontalAlignment = HorizontalAlignment.Center;
        _coverLetter.VerticalAlignment = VerticalAlignment.Center;
        Children.Add(_cover);

        var stack = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
        stack.Children.Add(_title = Face.Text("", 18, Ink.Hex(palette, "textPrimary"), FontWeight.SemiBold));
        stack.Children.Add(_artist = Face.Text("", 13, Ink.Hex(palette, "textSecondary")));
        stack.Children.Add(_lyric = Face.Text("", 12, Ink.Hex(palette, "textTertiary")));
        var seekHost = new Grid { Height = 18, Margin = new Thickness(0, 6, 0, 0) };
        seekHost.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(46)));
        seekHost.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        seekHost.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(46)));
        seekHost.Children.Add(_time = Face.Text("0:00", 11, Ink.Hex(palette, "textTertiary")));
        _seek = new Meter { Height = 4, VerticalAlignment = VerticalAlignment.Center, Color = Ink.Brush(Ink.Hex(palette, "textPrimary")), Track = Ink.Brush(Ink.Hex(palette, "seekRail")) };
        var seekHit = new Border { Child = _seek, Background = Brushes.Transparent, Cursor = new Cursor(StandardCursorType.Hand), Padding = new Thickness(0, 6) };
        seekHit.PointerPressed += Seek;
        Grid.SetColumn(seekHit, 1);
        seekHost.Children.Add(seekHit);
        var length = _length = Face.Text("0:00", 11, Ink.Hex(palette, "textTertiary"));
        length.HorizontalAlignment = HorizontalAlignment.Right;
        Grid.SetColumn(length, 2);
        seekHost.Children.Add(length);
        stack.Children.Add(seekHost);

        var controls = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 0) };
        controls.Children.Add(Button("⏮", MediaCommand.Previous, palette));
        controls.Children.Add(Button("⏯", MediaCommand.PlayPause, palette));
        controls.Children.Add(Button("⏭", MediaCommand.Next, palette));
        stack.Children.Add(controls);
        Grid.SetColumn(stack, 1);
        Children.Add(stack);
    }

    private Clickable Button(string glyph, MediaCommand command, PanelPalette palette)
    {
        var button = new Clickable { CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 4), Child = Face.Text(glyph, 16, Ink.Hex(palette, "textPrimary")) };
        button.Rest(Ink.Brush(Ink.Hex(palette, "fill")), Ink.Brush(Ink.Hex(palette, "hover")));
        button.Pressed += async (_, _) => { if (_session is not null) await _session.CommandAsync(command); };
        return button;
    }

    private void Apply(Session session)
    {
        var track = session.Track;
        var palette = session.Palette;
        if (_title is null || _artist is null || _lyric is null || _time is null || _length is null || _seek is null || _coverLetter is null || _cover is null) return;
        if (track is null)
        {
            _title.Text = Loc.T("没有正在播放的音乐", "Nothing is playing");
            _artist.Text = Loc.T("音乐和 Spotify 会显示在这里", "Music and Spotify show up here");
            _lyric.Text = "";
            _time.Text = "0:00";
            _length.Text = "0:00";
            _seek.Fraction = 0;
            _coverLetter.Text = "♪";
            return;
        }
        var now = DateTimeOffset.Now;
        var position = track.Position(now);
        _title.Text = track.Title.Length > 0 ? track.Title : Loc.T("未知曲目", "Unknown title");
        _artist.Text = track.Artist.Length > 0 ? track.Artist : track.Source;
        _lyric.Text = track.CurrentLyric ?? "";
        _time.Text = Clock(position);
        _length.Text = Clock(track.Duration);
        _seek.Fraction = track.Duration > TimeSpan.Zero ? position.TotalSeconds / track.Duration.TotalSeconds : 0;
        _seek.Color = Ink.Brush(track.State == MediaState.Playing ? Ink.Hex(palette, "textPrimary") : Ink.Hex(palette, "textTertiary"));
        _coverLetter.Text = (_title.Text.Length > 0 ? _title.Text[..1] : "♪").ToUpperInvariant();
        _cover.Background = Ink.Brush(Ink.Hex(palette, "fillSelected"));
    }

    private void Seek(object? sender, PointerPressedEventArgs e)
    {
        if (_session?.Track is not { CanSeek: true, Duration.TotalSeconds: > 0 } track || sender is not Control host) return;
        var point = e.GetPosition(host);
        double fraction = Math.Clamp(point.X / Math.Max(1, host.Bounds.Width), 0, 1);
        _ = _session.SeekAsync(track.Duration.TotalSeconds * fraction);
    }

    private static string Clock(TimeSpan time) => $"{(int)time.TotalMinutes}:{time.Seconds:00}";
}
