using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using forAxxon.Models;
using forAxxon.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace forAxxon.Views;

public partial class MainWindow : Window
{
    private int circle_total = 0;
    private int square_total = 0;
    private int triangle_total = 0;
    private readonly MainWindowViewModel _vm = new();
    private Point? _currentMousePosition;
    private Size _canvasSize = new(0, 0);

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;

        DrawingCanvas.SizeChanged += DrawingCanvas_SizeChanged;
    }

    private void DrawingCanvas_SizeChanged(object? sender, SizeChangedEventArgs e)
    {
        _canvasSize = e.NewSize;
    }

    private void DrawingCanvas_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetPosition(DrawingCanvas);
        _vm.HandleCanvasPointerPressed(point);
        RedrawCanvas();
    }

    private void DrawingCanvas_PointerMoved(object? sender, PointerEventArgs e)
    {
        _currentMousePosition = e.GetPosition(DrawingCanvas);
        RedrawCanvas();
    }

    private void RedrawCanvas()
    {
        DrawingCanvas.Children.Clear();

        // --- Готовые фигуры ---
        for (int i = 0; i < _vm.DrawnShapes.Count; i++)
        {
            var shape = _vm.DrawnShapes[i];
            string label = (i + 1).ToString(); 

            switch (shape)
            {
                case Models.Circle ci when ci.Points.Count == 2:
                    var circleStroke = ci.IsSelected ? Brushes.DarkGreen : Brushes.Green;
                    var circleThickness = ci.IsSelected ? 3.0 : 2.0;
                    DrawCircle(ci, circleStroke, circleThickness);
                    var circleCenter = ClampPoint(ci.Points[0]);
                    DrawLabel(circleCenter, (i + 1).ToString());
                    break;
                case Models.Triangle t when t.Points.Count == 3:
                    var triBrush = t.IsSelected ? Brushes.DarkRed : Brushes.Red;
                    var triThickness = t.IsSelected ? 3.0 : 2.0;
                    DrawPolygon(t.Points, triBrush, triThickness);
                    var triCenter = ComputeCentroid(t.Points);
                    DrawLabel(triCenter, label);
                    break;

                case Models.Square s when s.Points.Count == 2:
                    var a = s.Points[0];
                    var c = s.Points[1];
                    var clampedC = GetClampedDiagonalEnd(a, c);
                    var sqBrush = s.IsSelected ? Brushes.DarkBlue : Brushes.Blue;
                    var sqThickness = s.IsSelected ? 3.0 : 2.0;
                    var squarePoints = GetSquareFromDiagonal(a, clampedC);
                    DrawPolygon(squarePoints, sqBrush, sqThickness);
                    DrawPolygon(squarePoints, Brushes.Blue);
                    var squareCenter = ComputeCentroid(squarePoints);
                    DrawLabel(squareCenter, label);
                    break;
            }
        }

        // --- Фигуры пунктиром ---
        if (_vm.CurrentShape != null && _currentMousePosition.HasValue)
        {
            var mouse = _currentMousePosition.Value;
            var cur = _vm.CurrentShape;

            foreach (var p in cur.Points)
                DrawPoint(p);

            switch (cur)
            {
                case Models.Circle c when c.Points.Count == 1:
                    var r = Distance(c.Points[0], mouse);
                    DrawCirclePreview(c.Points[0], r);
                    
                    break;

                case Models.Triangle t when t.Points.Count == 1:
                    DrawLine(t.Points[0], mouse, Brushes.Gray, true);
                    break;

                case Models.Triangle t when t.Points.Count == 2:
                    var triPreview = new List<Point> { t.Points[0], t.Points[1], mouse };
                    DrawPolygonPreview(triPreview, Brushes.Red);
                   
                    break;

                case Models.Square s when s.Points.Count == 1:
                    var clampedMouse = GetClampedDiagonalEnd(s.Points[0], mouse);
                    var sqPreview = GetSquareFromDiagonal(s.Points[0], clampedMouse);
                    DrawPolygonPreview(sqPreview, Brushes.Blue);
                    break;
            }
        }
    }

    private double Distance(Point a, Point b) =>
        Math.Sqrt(Math.Pow(b.X - a.X, 2) + Math.Pow(b.Y - a.Y, 2));

    private Point ClampPoint(Point p)
    {
        return new Point(
            Math.Max(0, Math.Min(p.X, _canvasSize.Width)),
            Math.Max(0, Math.Min(p.Y, _canvasSize.Height))
        );
    }



    private void DrawPoint(Point p)
    {
        var dot = new Ellipse
        {
            Width = 6,
            Height = 6,
            Fill = Brushes.Black,
            Stroke = Brushes.White,
            StrokeThickness = 1
        };
        Canvas.SetLeft(dot, p.X - 3);
        Canvas.SetTop(dot, p.Y - 3);
        DrawingCanvas.Children.Add(dot);
    }

    private void DrawLine(Point p1, Point p2, IBrush brush, bool dashed)
    {
        var line = new Polyline
        {
            Points = new List<Point> { p1, p2 },
            Stroke = brush,
            StrokeThickness = 1
        };
        if (dashed)
            line.StrokeDashArray = new AvaloniaList<double> { 3, 3 };
        DrawingCanvas.Children.Add(line);
    }

    private void DrawCirclePreview(Point center, double radius)
    {
        var clampedCenter = ClampPoint(center);

        // Ограничиваем радиус
        var maxRadiusX = Math.Min(clampedCenter.X, _canvasSize.Width - clampedCenter.X);
        var maxRadiusY = Math.Min(clampedCenter.Y, _canvasSize.Height - clampedCenter.Y);
        var maxRadius = Math.Min(maxRadiusX, maxRadiusY);
        radius = Math.Min(radius, maxRadius);

        var ellipse = new Ellipse
        {
            Width = radius * 2,
            Height = radius * 2,
            Stroke = Brushes.Green,
            StrokeThickness = 1,
            StrokeDashArray = new AvaloniaList<double> { 3, 3 }
        };
        Canvas.SetLeft(ellipse, clampedCenter.X - radius);
        Canvas.SetTop(ellipse, clampedCenter.Y - radius);
        DrawingCanvas.Children.Add(ellipse);
    }

    private void DrawCircle(Models.Circle circle, IBrush stroke, double thickness = 2)
    {
        if (circle.Points.Count < 2) return;
        var center = ClampPoint(circle.Points[0]);
        var outer = ClampPoint(circle.Points[1]);
        var radius = Distance(center, outer);
        var maxRadius = Math.Min(
            Math.Min(center.X, _canvasSize.Width - center.X),
            Math.Min(center.Y, _canvasSize.Height - center.Y)
        );
        radius = Math.Min(radius, maxRadius);

        var ellipse = new Ellipse
        {
            Width = radius * 2,
            Height = radius * 2,
            Stroke = stroke,
            StrokeThickness = thickness
        };
        Canvas.SetLeft(ellipse, center.X - radius);
        Canvas.SetTop(ellipse, center.Y - radius);
        DrawingCanvas.Children.Add(ellipse);
    }

    private void DrawPolygon(List<Point> points, IBrush brush, double thickness = 2)
    {
        var polygon = new Polygon
        {
            Points = points,
            Stroke = brush,
            StrokeThickness = thickness,
            Fill = null
        };
        DrawingCanvas.Children.Add(polygon);
    }

    private void DrawPolygonPreview(List<Point> points, IBrush brush)
    {
        if (points.Count < 2) return;
        var polygon = new Polygon
        {
            Points = points,
            Stroke = brush,
            StrokeThickness = 1,
            Fill = null,
            StrokeDashArray = new AvaloniaList<double> { 3, 3 }
        };
        DrawingCanvas.Children.Add(polygon);
    }
    private void ShapeItem_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border border && border.Tag is ShapeBase shape)
        {
            foreach (var s in _vm.DrawnShapes)
                s.IsSelected = false;

            shape.IsSelected = true;

            RedrawCanvas();
        }
    }
    private List<Point> GetSquareFromDiagonal(Point a, Point c)
    {
        double centerX = (a.X + c.X) / 2;
        double centerY = (a.Y + c.Y) / 2;
        double dx = a.X - centerX;
        double dy = a.Y - centerY;
        var b = new Point(centerX - dy, centerY + dx);
        var d = new Point(centerX + dy, centerY - dx);
        return new List<Point> { a, b, c, d };
    }

    private Point GetClampedDiagonalEnd(Point start, Point mouse)
    {

        var dx = mouse.X - start.X;
        var dy = mouse.Y - start.Y;


        if (dx == 0 && dy == 0)
            return mouse;

        var candidate = mouse;


        var points = GetSquareFromDiagonal(start, candidate);
        var minX = points.Min(p => p.X);
        var maxX = points.Max(p => p.X);
        var minY = points.Min(p => p.Y);
        var maxY = points.Max(p => p.Y);

        if (minX >= 0 && maxX <= _canvasSize.Width && minY >= 0 && maxY <= _canvasSize.Height)
            return candidate;


        var t = 1.0; 
        var step = 0.01;

        while (t > 0)
        {
            candidate = new Point(start.X + dx * t, start.Y + dy * t);
            points = GetSquareFromDiagonal(start, candidate);
            minX = points.Min(p => p.X);
            maxX = points.Max(p => p.X);
            minY = points.Min(p => p.Y);
            maxY = points.Max(p => p.Y);

            if (minX >= 0 && maxX <= _canvasSize.Width && minY >= 0 && maxY <= _canvasSize.Height)
                break;

            t -= step;
        }

        // Если не нашли — возвращаем start (квадрат вырожден)
        if (t <= 0)
            return start;

        return candidate;
    }
    private void DrawLabel(Point center, string text)
    {
        var label = new TextBlock
        {
            Text = text,
            Foreground = Brushes.Black,
            FontSize = 12,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };
        Canvas.SetLeft(label, center.X - 6);
        Canvas.SetTop(label, center.Y - 6);
        DrawingCanvas.Children.Add(label);
    }
    private Point ComputeCentroid(List<Point> points)
    {
        if (points == null || points.Count == 0)
            return new Point(0, 0);

        double x = points.Average(p => p.X);
        double y = points.Average(p => p.Y);
        return new Point(x, y);
    }
}