using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using BrimDeck.Mac.Drawing;

namespace BrimDeck.Mac.Views;

public sealed class IslandChrome : Grid
{
    private readonly Avalonia.Controls.Shapes.Path _fill = new() { Stretch = Stretch.None, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
    private readonly Grid _host = new();

    public IslandChrome()
    {
        Children.Add(_fill);
        Children.Add(_host);
    }

    public Grid Host => _host;
    public double Shoulder { get; set; }
    public double TopLeft { get; set; }
    public double TopRight { get; set; }
    public double BottomRight { get; set; } = 12;
    public double BottomLeft { get; set; } = 12;
    public IBrush? Fill { get; set; }
    public IBrush? Rim { get; set; }

    public void Redraw()
    {
        var shape = IslandOutline.Geometry(Math.Max(0, Bounds.Width), Math.Max(0, Bounds.Height), Shoulder, TopLeft, TopRight, BottomRight, BottomLeft);
        _fill.Data = shape;
        _fill.Fill = Fill;
        _fill.Stroke = Rim;
        _fill.StrokeThickness = Rim is null ? 0 : 1;
        _fill.Width = Bounds.Width;
        _fill.Height = Bounds.Height;
        _host.Clip = shape;
        foreach (var child in _host.Children) child.Arrange(new Rect(Bounds.Size));
    }
}
