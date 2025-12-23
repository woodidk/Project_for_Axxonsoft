using Avalonia;
using System;
using System.Collections.Generic;
using System.Linq;

namespace forAxxon.Models;

public static class GeometryHelper
{
    public static double Distance(Point a, Point b) =>
        Math.Sqrt(Math.Pow(b.X - a.X, 2) + Math.Pow(b.Y - a.Y, 2));

    public static Point ComputeCentroid(IList<Point> points) =>
        points.Count == 0 ? new(0, 0) : new(points.Average(p => p.X), points.Average(p => p.Y));

    public static List<Point> GetSquareFromDiagonal(Point a, Point c)
    {
        var cx = (a.X + c.X) / 2;
        var cy = (a.Y + c.Y) / 2;
        var dx = a.X - cx;
        var dy = a.Y - cy;
        return new List<Point>
        {
            a,
            new Point(cx - dy, cy + dx),
            c,
            new Point(cx + dy, cy - dx)
        };
    }
    public static Point ClampCircleOuterToNonNegativeArea(Point center, Point outer)
    {
        var dx = outer.X - center.X;
        var dy = outer.Y - center.Y;
        var radius = Math.Sqrt(dx * dx + dy * dy);
        if (radius < 1e-6)
            return new Point(center.X + 1, center.Y);
        double maxRadius = Math.Min(center.X, center.Y);
        if (maxRadius < 1e-6)
            maxRadius = 1e-6;
        if (radius <= maxRadius)
            return outer;
        double scale = maxRadius / radius;
        return new Point(center.X + dx * scale, center.Y + dy * scale);
    }


    public static Point ClampSquareOuterToNonNegativeArea(Point a, Point candidateC)
    {
        var square = GetSquareFromDiagonal(a, candidateC);
        double minX = square.Min(p => p.X);
        double minY = square.Min(p => p.Y);
        if (minX >= 0 && minY >= 0)
            return candidateC;
        double dx = candidateC.X - a.X;
        double dy = candidateC.Y - a.Y;
        if (Math.Abs(dx) < 1e-6 && Math.Abs(dy) < 1e-6)
            return new Point(a.X + 1, a.Y + 1);
        double low = 0.0, high = 1.0;
        Point best = a;
        for (int i = 0; i < 30; i++)
        {
            double mid = (low + high) / 2;
            Point testC = new Point(a.X + dx * mid, a.Y + dy * mid);
            var testSquare = GetSquareFromDiagonal(a, testC);
            double testMinX = testSquare.Min(p => p.X);
            double testMinY = testSquare.Min(p => p.Y);
            if (testMinX >= 0 && testMinY >= 0)
            {
                best = testC;
                low = mid;
            }
            else
            {
                high = mid;
            }
        }
        return best;
    }

    public static bool IsPointInPolygon(Point p, IList<Point> vertices)
    {
        int count = 0;
        for (int i = 0, j = vertices.Count - 1; i < vertices.Count; j = i++)
        {
            if (((vertices[i].Y > p.Y) != (vertices[j].Y > p.Y)) &&
                (p.X < (vertices[j].X - vertices[i].X) * (p.Y - vertices[i].Y) / (vertices[j].Y - vertices[i].Y) + vertices[i].X))
                count++;
        }
        return count % 2 == 1;
    }
}