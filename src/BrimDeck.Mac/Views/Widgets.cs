using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using BrimDeck.Mac.Theme;

namespace BrimDeck.Mac.Views;

public static class Face
{
    public static readonly FontFamily Family = new("Inter, WenQuanYi Micro Hei, PingFang SC, Noto Sans CJK SC, sans-serif");

    public static TextBlock Text(string text, double size, string color, FontWeight weight = FontWeight.Normal, TextAlignment align = TextAlignment.Left)
        => new()
        {
            Text = text,
            FontFamily = Family,
            FontSize = size,
            Foreground = Ink.Brush(color),
            FontWeight = weight,
            TextAlignment = align,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap
        };
}

public sealed class Clickable : Border
{
    private IBrush _idle = Brushes.Transparent;
    private IBrush? _hover;

    public event EventHandler? Pressed;

    public Clickable()
    {
        Background = Brushes.Transparent;
        Cursor = new Cursor(StandardCursorType.Hand);
        PointerEntered += (_, _) => { if (_hover is not null) Background = _hover; };
        PointerExited += (_, _) => Background = _idle;
        PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                Pressed?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
            }
        };
    }

    public void Rest(IBrush idle, IBrush? hover = null)
    {
        _idle = idle;
        _hover = hover;
        Background = idle;
    }
}

public sealed class QuotaRing : Control
{
    public static readonly StyledProperty<double> PercentProperty = AvaloniaProperty.Register<QuotaRing, double>(nameof(Percent));
    public static readonly StyledProperty<IBrush?> ColorProperty = AvaloniaProperty.Register<QuotaRing, IBrush?>(nameof(Color));
    public static readonly StyledProperty<IBrush?> TrackProperty = AvaloniaProperty.Register<QuotaRing, IBrush?>(nameof(Track));

    public double Percent { get => GetValue(PercentProperty); set => SetValue(PercentProperty, value); }
    public IBrush? Color { get => GetValue(ColorProperty); set => SetValue(ColorProperty, value); }
    public IBrush? Track { get => GetValue(TrackProperty); set => SetValue(TrackProperty, value); }

    static QuotaRing()
    {
        AffectsRender<QuotaRing>(PercentProperty, ColorProperty, TrackProperty);
    }

    public override void Render(DrawingContext context)
    {
        double size = Math.Min(Bounds.Width, Bounds.Height);
        if (size <= 0) return;
        double thickness = Math.Max(1.6, size * 0.14);
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        double radius = size / 2 - thickness / 2;
        if (Track is { } track) context.DrawEllipse(null, new Pen(track, thickness), center, radius, radius);
        double fraction = Math.Clamp(Percent / 100, 0, 1);
        if (Color is not { } color || fraction <= 0) return;
        if (fraction >= 0.999) { context.DrawEllipse(null, new Pen(color, thickness), center, radius, radius); return; }
        double angle = fraction * Math.PI * 2;
        var start = new Point(center.X, center.Y - radius);
        var end = new Point(center.X - radius * Math.Sin(angle), center.Y - radius * Math.Cos(angle));
        var figure = new PathFigure { StartPoint = start, IsClosed = false };
        figure.Segments!.Add(new ArcSegment
        {
            Point = end,
            Size = new Size(radius, radius),
            IsLargeArc = fraction > 0.5,
            SweepDirection = SweepDirection.CounterClockwise
        });
        var geometry = new PathGeometry();
        geometry.Figures!.Add(figure);
        context.DrawGeometry(null, new Pen(color, thickness) { LineCap = PenLineCap.Round }, geometry);
    }
}

public sealed class Meter : Control
{
    public static readonly StyledProperty<double> FractionProperty = AvaloniaProperty.Register<Meter, double>(nameof(Fraction));
    public static readonly StyledProperty<IBrush?> ColorProperty = AvaloniaProperty.Register<Meter, IBrush?>(nameof(Color));
    public static readonly StyledProperty<IBrush?> TrackProperty = AvaloniaProperty.Register<Meter, IBrush?>(nameof(Track));
    public double Fraction { get => GetValue(FractionProperty); set => SetValue(FractionProperty, value); }
    public IBrush? Color { get => GetValue(ColorProperty); set => SetValue(ColorProperty, value); }
    public IBrush? Track { get => GetValue(TrackProperty); set => SetValue(TrackProperty, value); }

    static Meter() => AffectsRender<Meter>(FractionProperty, ColorProperty, TrackProperty);

    public override void Render(DrawingContext context)
    {
        double h = Math.Max(2, Bounds.Height), w = Bounds.Width, r = h / 2;
        if (w <= 0) return;
        if (Track is { } track) context.DrawRectangle(track, null, new Rect(0, 0, w, h), r, r);
        double fill = Math.Clamp(Fraction, 0, 1) * w;
        if (Color is { } color && fill > 0) context.DrawRectangle(color, null, new Rect(0, 0, Math.Max(fill, h), h), r, r);
    }
}

public sealed class Equalizer : Control
{
    public static readonly StyledProperty<bool> PlayingProperty = AvaloniaProperty.Register<Equalizer, bool>(nameof(Playing));
    public static readonly StyledProperty<IBrush?> ColorProperty = AvaloniaProperty.Register<Equalizer, IBrush?>(nameof(Color));
    public bool Playing { get => GetValue(PlayingProperty); set => SetValue(PlayingProperty, value); }
    public IBrush? Color { get => GetValue(ColorProperty); set => SetValue(ColorProperty, value); }

    static Equalizer() => AffectsRender<Equalizer>(PlayingProperty, ColorProperty);

    public override void Render(DrawingContext context)
    {
        if (Color is not { } color || Bounds.Width <= 0) return;
        double t = Playing ? DateTime.UtcNow.TimeOfDay.TotalSeconds : 0;
        double gap = 2, bars = 4, bar = Math.Max(1.5, (Bounds.Width - gap * (bars - 1)) / bars);
        for (int i = 0; i < bars; i++)
        {
            double level = Playing ? 0.28 + 0.72 * (0.5 + 0.5 * Math.Sin(t * 7 + i * 1.3)) : 0.22;
            double h = Bounds.Height * level;
            context.DrawRectangle(color, null, new Rect(i * (bar + gap), Bounds.Height - h, bar, h), 1, 1);
        }
    }
}
