using Avalonia;
using System.Collections.ObjectModel;

namespace forAxxon.Models;

public static class ShapeHitTester
{
    public static ShapeBase? FindTopmostShapeAt(Point point, ObservableCollection<ShapeBase> shapes)
    {
        for (int i = shapes.Count - 1; i >= 0; i--)
        {
            if (IsPointInShape(point, shapes[i]))
                return shapes[i];
        }
        return null;
    }

    public static bool IsPointInShape(Point point, ShapeBase shape) => shape switch
    {
        Circle c when c.Points.Count == 2 => IsPointInCircle(point, c),
        Triangle t when t.Points.Count == 3 => GeometryHelper.IsPointInPolygon(point, t.Points),
        Square s when s.Points.Count == 2 => IsPointInSquare(point, s),
        _ => false
    };

    private static bool IsPointInCircle(Point p, Circle c)
    {
        var center = c.Points[0];
        var outer = c.Points[1];
        var radius = GeometryHelper.Distance(center, outer);
        return GeometryHelper.Distance(p, center) <= radius;
    }

    private static bool IsPointInSquare(Point p, Square s)
    {
        var pts = GeometryHelper.GetSquareFromDiagonal(s.Points[0], s.Points[1]);
        return GeometryHelper.IsPointInPolygon(p, pts);
    }
}