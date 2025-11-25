using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using forAxxon.Models;
using forAxxon.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace forAxxon.Views;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _vm = new();
    private Point? _currentMousePosition;
    private Size _canvasSize = new(0, 0);
    private bool _isEditingPoint = false;
    private int _editingPointIndex = -1;
    private ShapeBase? _editingShape;
    private bool _isDragging = false;
    private Point _dragStartOffset;
    private ShapeBase? _draggedShape;

    private Point? _circleRadiusOffset;
    private Point? _squareDiagonalOffsetA;
    private Point? _squareDiagonalOffsetC;

    private bool _isClosingGracefully = false;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;
        DrawingCanvas.SizeChanged += DrawingCanvas_SizeChanged;
        _vm.DrawnShapes.CollectionChanged += (s, e) => RedrawCanvas();
        _vm.StorageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
        _vm.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainWindowViewModel.SelectedShape))
                RedrawCanvas();
        };
        _vm.RequestConfirmation = async () =>
        {
            var result = await ShowDialogAsync(
                "Подтверждение",
                "Вы уверены, что хотите удалить все фигуры?",
                DialogButtons.YesNo
            );
            return result == DialogResult.Yes;
        };
        this.Opened += async (s, e) =>
        {
            var lastFile = SessionManager.GetLastFilePath();
            if (!string.IsNullOrEmpty(lastFile))
            {
                await _vm.LoadFromFile(lastFile);
                RedrawCanvas();
            }
        };

        this.Closing += async (s, e) =>
        {
            if (_isClosingGracefully) return;
            if (_vm.DrawnShapes.Count == 0) return;

            e.Cancel = true;
            var result = await ShowDialogAsync(
                "Подтверждение",
                "Сохранить изменения перед выходом?",
                DialogButtons.YesNoCancel
            );
            if (result == DialogResult.Cancel) return;
            if (result == DialogResult.Yes)
            {
                await _vm.SaveToFileCommand.ExecuteAsync(null);
            }

            _isClosingGracefully = true;
            Close();
        };
    }

    private void DrawingCanvas_SizeChanged(object? sender, SizeChangedEventArgs e)
    {
        _canvasSize = e.NewSize;
        _vm.CanvasSize = _canvasSize; 
    }

    private void DrawingCanvas_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetPosition(DrawingCanvas);
        if (_vm.CurrentShape != null)
        {
            _vm.HandleCanvasPointerPressed(point);
            RedrawCanvas();
            return;
        }

        ShapeBase? clickedShape = null;
        foreach (var shape in _vm.DrawnShapes.Reverse())
        {
            if (IsPointInShape(point, shape))
            {
                clickedShape = shape;
                break;
            }
        }

        if (clickedShape != null)
        {
            _vm.SelectedShape = clickedShape;
            _isDragging = true;
            _draggedShape = clickedShape;

            var centroid = ComputeCentroid(clickedShape.Points);
            _dragStartOffset = new Point(point.X - centroid.X, point.Y - centroid.Y);
            if (_draggedShape is Circle c && c.Points.Count == 2)
            {
                _circleRadiusOffset = new Point(c.Points[1].X - c.Points[0].X, c.Points[1].Y - c.Points[0].Y);
            }
            else if (_draggedShape is Square s && s.Points.Count == 2)
            {
                _squareDiagonalOffsetA = new Point(s.Points[0].X - centroid.X, s.Points[0].Y - centroid.Y);
                _squareDiagonalOffsetC = new Point(s.Points[1].X - centroid.X, s.Points[1].Y - centroid.Y);
            }

            e.Pointer.Capture(DrawingCanvas);
        }
        else
        {
            _vm.SelectedShape = null;
        }

        RedrawCanvas();
    }

    private void DrawingCanvas_PointerMoved(object? sender, PointerEventArgs e)
    {
        var point = e.GetPosition(DrawingCanvas);

        if (_isEditingPoint && _editingShape != null && _editingPointIndex >= 0)
        {
            Point newPoint;

            if (_editingShape is Circle circle && circle.Points.Count >= 2)
            {
                if (_editingPointIndex == 1) 
                {
                    var center = circle.Points[0];
                    var maxRadiusX = Math.Min(center.X, _canvasSize.Width - center.X);
                    var maxRadiusY = Math.Min(center.Y, _canvasSize.Height - center.Y);
                    var maxRadius = Math.Min(maxRadiusX, maxRadiusY);

                    var dx = point.X - center.X;
                    var dy = point.Y - center.Y;
                    var actualRadius = Math.Sqrt(dx * dx + dy * dy);

                    if (actualRadius <= 0)
                    {
                        newPoint = point;
                    }
                    else if (actualRadius <= maxRadius)
                    {
                        newPoint = point;
                    }
                    else
                    {
                        var scale = maxRadius / actualRadius;
                        newPoint = new Point(
                            center.X + dx * scale,
                            center.Y + dy * scale
                        );
                    }
                }
                else 
                {
                    newPoint = new Point(
                        Math.Max(0, Math.Min(point.X, _canvasSize.Width)),
                        Math.Max(0, Math.Min(point.Y, _canvasSize.Height))
                    );
                }
            }
            else
            {
                newPoint = new Point(
                    Math.Max(0, Math.Min(point.X, _canvasSize.Width)),
                    Math.Max(0, Math.Min(point.Y, _canvasSize.Height))
                );
            }

            var newPoints = new List<Point>(_editingShape.Points);
            newPoints[_editingPointIndex] = newPoint;
            _editingShape.Points = newPoints;
            RedrawCanvas();
            return;
        }

        if (_isDragging && _draggedShape != null)
        {
            var newCentroid = new Point(point.X - _dragStartOffset.X, point.Y - _dragStartOffset.Y);

            if (_draggedShape is Circle circle && circle.Points.Count == 2)
            {
                var radiusOffset = _circleRadiusOffset ?? new Point(circle.Points[1].X - circle.Points[0].X, circle.Points[1].Y - circle.Points[0].Y);
                var radius = Math.Sqrt(radiusOffset.X * radiusOffset.X + radiusOffset.Y * radiusOffset.Y);

                var safeCenter = new Point(
                    Math.Max(radius, Math.Min(newCentroid.X, _canvasSize.Width - radius)),
                    Math.Max(radius, Math.Min(newCentroid.Y, _canvasSize.Height - radius))
                );

                var newOuter = new Point(safeCenter.X + radiusOffset.X, safeCenter.Y + radiusOffset.Y);
                circle.Points = new List<Point> { safeCenter, newOuter };
            }
            else if (_draggedShape is Square square && square.Points.Count == 2)
            {
                var offsetA = _squareDiagonalOffsetA ?? new Point(
                    square.Points[0].X - ComputeCentroid(square.Points).X,
                    square.Points[0].Y - ComputeCentroid(square.Points).Y
                );
                var offsetC = _squareDiagonalOffsetC ?? new Point(
                    square.Points[1].X - ComputeCentroid(square.Points).X,
                    square.Points[1].Y - ComputeCentroid(square.Points).Y
                );

                var newA = new Point(newCentroid.X + offsetA.X, newCentroid.Y + offsetA.Y);
                var newC = new Point(newCentroid.X + offsetC.X, newCentroid.Y + offsetC.Y);

                var corners = GetSquareFromDiagonal(newA, newC);
                var minX = corners.Min(p => p.X);
                var maxX = corners.Max(p => p.X);
                var minY = corners.Min(p => p.Y);
                var maxY = corners.Max(p => p.Y);

                double shiftX = 0, shiftY = 0;
                if (minX < 0) shiftX = -minX;
                else if (maxX > _canvasSize.Width) shiftX = _canvasSize.Width - maxX;

                if (minY < 0) shiftY = -minY;
                else if (maxY > _canvasSize.Height) shiftY = _canvasSize.Height - maxY;

                if (shiftX != 0 || shiftY != 0)
                {
                    newA = new Point(newA.X + shiftX, newA.Y + shiftY);
                    newC = new Point(newC.X + shiftX, newC.Y + shiftY);
                }

                square.Points = new List<Point> { newA, newC };
            }
            else
            {
                var oldCentroid = ComputeCentroid(_draggedShape.Points);
                var dx = newCentroid.X - oldCentroid.X;
                var dy = newCentroid.Y - oldCentroid.Y;
                var newPoints = _draggedShape.Points.Select(p => new Point(p.X + dx, p.Y + dy)).ToList();

                bool fits = newPoints.All(p =>
                    p.X >= 0 && p.X <= _canvasSize.Width &&
                    p.Y >= 0 && p.Y <= _canvasSize.Height
                );

                if (fits)
                {
                    _draggedShape.Points = newPoints;
                }
            }

            RedrawCanvas();
            return;
        }

        _currentMousePosition = point;
        RedrawCanvas();
    }

    private void DrawingCanvas_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            _draggedShape = null;
            _circleRadiusOffset = null;
            _squareDiagonalOffsetA = null;
            _squareDiagonalOffsetC = null;
            e.Pointer.Capture(null);
        }

        if (_isEditingPoint)
        {
            _isEditingPoint = false;
            _editingPointIndex = -1;
            _editingShape = null;
            e.Pointer.Capture(null);
        }
    }

    private void RedrawCanvas()
    {
        DrawingCanvas.Children.Clear();
        for (int i = 0; i < _vm.DrawnShapes.Count; i++)
        {
            var shape = _vm.DrawnShapes[i];
            string label = (i + 1).ToString();
            switch (shape)
            {
                case Circle ci when ci.Points.Count == 2:
                    var circleStroke = ci.IsSelected ? Brushes.DarkGreen : Brushes.Green;
                    var circleThickness = ci.IsSelected ? 3.0 : 2.0;
                    DrawCircle(ci, circleStroke, circleThickness);
                    var circleCenter = ClampPoint(ci.Points[0]);
                    DrawLabel(circleCenter, label);
                    break;

                case Triangle t when t.Points.Count == 3:
                    var triBrush = t.IsSelected ? Brushes.DarkRed : Brushes.Red;
                    var triThickness = t.IsSelected ? 3.0 : 2.0;
                    DrawPolygon(t.Points, triBrush, triThickness);
                    var triCenter = ComputeCentroid(t.Points);
                    DrawLabel(triCenter, label);
                    break;

                case Square s when s.Points.Count == 2:
                    var a = s.Points[0];
                    var c = s.Points[1];
                    var clampedC = GetClampedDiagonalEnd(a, c);
                    var sqBrush = s.IsSelected ? Brushes.DarkBlue : Brushes.Blue;
                    var sqThickness = s.IsSelected ? 3.0 : 2.0;
                    var squarePoints = GetSquareFromDiagonal(a, clampedC);
                    DrawPolygon(squarePoints, sqBrush, sqThickness);
                    var squareCenter = ComputeCentroid(squarePoints);
                    DrawLabel(squareCenter, label);
                    break;
            }
        }

        if (_vm.SelectedShape != null && _vm.IsEditingPoints)
        {
            var shape = _vm.SelectedShape;
            for (int i = 0; i < shape.Points.Count; i++)
            {
 
                if (shape is Circle && i == 0)
                    continue;

                var p = shape.Points[i];
                var handle = new Ellipse
                {
                    Width = 10,
                    Height = 10,
                    Fill = Brushes.White,
                    Stroke = Brushes.Black,
                    StrokeThickness = 1,
                    Tag = i
                };
                Canvas.SetLeft(handle, p.X - 5);
                Canvas.SetTop(handle, p.Y - 5);

                var idx = i;
                var editingShape = shape;
                handle.PointerPressed += (s, e) =>
                {
                    _isEditingPoint = true;
                    _editingPointIndex = idx;
                    _editingShape = editingShape;
                    e.Pointer.Capture(handle);
                    e.Handled = true;
                };

                DrawingCanvas.Children.Add(handle);
            }
        }

        if (_vm.CurrentShape != null && _currentMousePosition.HasValue)
        {
            var mouse = _currentMousePosition.Value;
            var cur = _vm.CurrentShape;

            foreach (var p in cur.Points)
                DrawPoint(p);

            switch (cur)
            {
                case Circle c when c.Points.Count == 1:
                    var r = Distance(c.Points[0], mouse);
                    DrawCirclePreview(c.Points[0], r);
                    break;

                case Triangle t when t.Points.Count == 1:
                    DrawLine(t.Points[0], mouse, Brushes.Gray, true);
                    break;

                case Triangle t when t.Points.Count == 2:
                    var triPreview = new List<Point> { t.Points[0], t.Points[1], mouse };
                    DrawPolygonPreview(triPreview, Brushes.Red);
                    break;

                case Square s when s.Points.Count == 1:
                    var clampedMouse = GetClampedDiagonalEnd(s.Points[0], mouse);
                    var sqPreview = GetSquareFromDiagonal(s.Points[0], clampedMouse);
                    DrawPolygonPreview(sqPreview, Brushes.Blue);
                    break;
            }
        }
    }

    #region Вспомогательные методы рисования

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
        var maxRadius = Math.Min(
            Math.Min(clampedCenter.X, _canvasSize.Width - clampedCenter.X),
            Math.Min(clampedCenter.Y, _canvasSize.Height - clampedCenter.Y)
        );
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

    private void DrawCircle(Circle circle, IBrush stroke, double thickness = 2)
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

    private void DrawLabel(Point center, string text)
    {
        var label = new TextBlock
        {
            Text = text,
            Foreground = Brushes.Black,
            FontSize = 12,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        Canvas.SetLeft(label, center.X - 6);
        Canvas.SetTop(label, center.Y - 6);
        DrawingCanvas.Children.Add(label);
    }

    #endregion

    #region Геометрия и перетаскивание

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
        if (dx == 0 && dy == 0) return mouse;

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
        return t <= 0 ? start : candidate;
    }

    private Point ComputeCentroid(List<Point> points)
    {
        if (points == null || points.Count == 0)
            return new Point(0, 0);
        double x = points.Average(p => p.X);
        double y = points.Average(p => p.Y);
        return new Point(x, y);
    }

    private bool IsPointInShape(Point point, ShapeBase shape)
    {
        return shape switch
        {
            Circle c when c.Points.Count == 2 => IsPointInCircle(point, c),
            Triangle t when t.Points.Count == 3 => IsPointInPolygon(point, t.Points),
            Square s when s.Points.Count == 2 => IsPointInSquare(point, s),
            _ => false
        };
    }

    private bool IsPointInCircle(Point p, Circle circle)
    {
        var center = ClampPoint(circle.Points[0]);
        var outer = ClampPoint(circle.Points[1]);
        var radius = Distance(center, outer);
        return Distance(p, center) <= radius;
    }

    private bool IsPointInPolygon(Point p, List<Point> vertices)
    {
        int count = 0;
        for (int i = 0, j = vertices.Count - 1; i < vertices.Count; j = i++)
        {
            if (((vertices[i].Y > p.Y) != (vertices[j].Y > p.Y)) &&
                (p.X < (vertices[j].X - vertices[i].X) * (p.Y - vertices[i].Y) / (vertices[j].Y - vertices[i].Y) + vertices[i].X))
            {
                count++;
            }
        }
        return count % 2 == 1;
    }

    private bool IsPointInSquare(Point p, Square square)
    {
        var pts = GetSquareFromDiagonal(ClampPoint(square.Points[0]), ClampPoint(square.Points[1]));
        return IsPointInPolygon(p, pts);
    }

    #endregion

    #region Диалог закрытия

    private async Task<DialogResult> ShowDialogAsync(string title, string message, DialogButtons buttons)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 320,
            Height = 160,
            CanResize = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Topmost = true
        };

        var result = DialogResult.None;
        var textBlock = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(15, 15, 15, 0)
        };

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 10, 15, 15),
            Spacing = 10
        };

        void AddButton(string content, DialogResult res, bool isDefault = false)
        {
            var btn = new Button
            {
                Content = content,
                Width = 80,
                IsDefault = isDefault
            };
            btn.Click += (_, _) =>
            {
                result = res;
                dialog.Close();
            };
            buttonPanel.Children.Add(btn);
        }

        switch (buttons)
        {
            case DialogButtons.OK:
                AddButton("OK", DialogResult.OK, true);
                break;
            case DialogButtons.YesNo:
                AddButton("Да", DialogResult.Yes, true);
                AddButton("Нет", DialogResult.No);
                break;
            case DialogButtons.YesNoCancel:
                AddButton("Да", DialogResult.Yes);
                AddButton("Нет", DialogResult.No);
                AddButton("Отмена", DialogResult.Cancel, true);
                break;
        }

        var layout = new DockPanel();
        DockPanel.SetDock(buttonPanel, Dock.Bottom);
        layout.Children.Add(buttonPanel);
        layout.Children.Add(textBlock);
        dialog.Content = layout;

        await dialog.ShowDialog(this);
        return result;
    }

    public enum DialogButtons
    {
        OK,
        YesNo,
        YesNoCancel
    }

    public enum DialogResult
    {
        None,
        OK,
        Yes,
        No,
        Cancel
    }

    #endregion
}