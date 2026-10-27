using Avalonia;
using Avalonia.Media;

namespace BrimDeck.Mac.Drawing;

// The notch outline from the Windows island: shoulders that leave the screen edge with zero curvature,
// then a straight waist and rounded bottom corners. A capsule or an open floating panel has no shoulders.
public static class IslandOutline
{
    public const double Circle = 0.5522847498307936;

    public readonly record struct Vertex(double X, double Y);

    public static IReadOnlyList<Vertex> Build(double width, double height, double shoulderRadius, double topLeft, double topRight, double bottomRight, double bottomLeft)
    {
        double w = width, h = height;
        if (w <= 0 || h <= 0) return [];
        double shoulder = Math.Clamp(shoulderRadius, 0, Math.Min(w / 4, h / 2));
        double depth = shoulder * (7d / 16);
        double left = shoulder, right = w - shoulder;
        double limit = Math.Max(0, Math.Min(shoulder > 0 ? h - depth : h / 2, (right - left) / 2));
        double bl = Math.Clamp(bottomLeft, 0, limit);
        double br = Math.Clamp(bottomRight, 0, limit);
        double tl = Math.Clamp(topLeft, 0, Math.Min(w, h) / 2);
        double tr = Math.Clamp(topRight, 0, Math.Min(w, h) / 2);
        var points = new List<Vertex> { new(shoulder > 0 ? 0 : tl, 0), new(shoulder > 0 ? w : w - tr, 0) };
        if (shoulder > 0) points.Add(new(right, depth));
        else points.Add(new(w, tr));
        points.Add(new(right, h - br));
        points.Add(new(right - br, h));
        points.Add(new(left + bl, h));
        points.Add(new(left, h - bl));
        points.Add(new(left, shoulder > 0 ? depth : tl));
        points.Add(new(0, 0));
        return points;
    }

    public static StreamGeometry Geometry(double width, double height, double shoulderRadius, double topLeft, double topRight, double bottomRight, double bottomLeft)
    {
        var geometry = new StreamGeometry();
        double w = width, h = height;
        if (w <= 0 || h <= 0) return geometry;
        double shoulder = Math.Clamp(shoulderRadius, 0, Math.Min(w / 4, h / 2));
        double depth = shoulder * (7d / 16);
        double left = shoulder, right = w - shoulder;
        double limit = Math.Max(0, Math.Min(shoulder > 0 ? h - depth : h / 2, (right - left) / 2));
        double bl = Math.Clamp(bottomLeft, 0, limit);
        double br = Math.Clamp(bottomRight, 0, limit);
        double tl = Math.Clamp(topLeft, 0, Math.Min(w, h) / 2);
        double tr = Math.Clamp(topRight, 0, Math.Min(w, h) / 2);
        using (var path = geometry.Open())
        {
            path.BeginFigure(new Point(shoulder > 0 ? 0 : tl, 0), true);
            path.LineTo(new Point(shoulder > 0 ? w : w - tr, 0));
            if (shoulder > 0)
            {
                path.CubicBezierTo(new Point(w - shoulder * .28, 0), new Point(w - shoulder * .64, 0), new Point(w - shoulder * .82, depth * .18));
                path.CubicBezierTo(new Point(right, depth * .36), new Point(right, depth * .72), new Point(right, depth));
            }
            else path.CubicBezierTo(new Point(w - tr * (1 - Circle), 0), new Point(w, tr * (1 - Circle)), new Point(w, tr));
            path.LineTo(new Point(right, h - br));
            path.CubicBezierTo(new Point(right, h - br * (1 - Circle)), new Point(right - br * (1 - Circle), h), new Point(right - br, h));
            path.LineTo(new Point(left + bl, h));
            path.CubicBezierTo(new Point(left + bl * (1 - Circle), h), new Point(left, h - bl * (1 - Circle)), new Point(left, h - bl));
            path.LineTo(new Point(left, shoulder > 0 ? depth : tl));
            if (shoulder > 0)
            {
                path.CubicBezierTo(new Point(left, depth * .72), new Point(left, depth * .36), new Point(shoulder * .82, depth * .18));
                path.CubicBezierTo(new Point(shoulder * .64, 0), new Point(shoulder * .28, 0), new Point(0, 0));
            }
            else path.CubicBezierTo(new Point(0, tl * (1 - Circle)), new Point(tl * (1 - Circle), 0), new Point(tl, 0));
            path.EndFigure(true);
        }
        return geometry;
    }
}
