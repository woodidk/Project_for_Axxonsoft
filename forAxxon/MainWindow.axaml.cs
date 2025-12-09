// forAxxon/Views/MainWindow.cs
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using forAxxon.Models;
using forAxxon.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace forAxxon.Views;

public class PointHandleTag
{
    public ShapeBase Shape { get; }
    public int PointIndex { get; }
    public PointHandleTag(ShapeBase shape, int index) => (Shape, PointIndex) = (shape, index);
}

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _vm = new();
    private Point? _currentMousePosition;
    private bool _isClosingGracefully;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;
        _vm.StorageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;

        _vm.DrawnShapes.CollectionChanged += (_, _) => RedrawCanvas();
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainWindowViewModel.SelectedShape))
                RedrawCanvas();
        };

        _vm.RequestConfirmation = async () =>
        {
            var r = await ShowDialog("Подтверждение", "Удалить все фигуры?", DialogButtons.YesNo);
            return r == DialogResult.Yes;
        };

        this.Opened += async (_, _) =>
        {
            var path = SessionManager.GetLastFilePath();
            if (!string.IsNullOrEmpty(path)) await _vm.LoadFromFile(path);
            RedrawCanvas();
        };

        this.Closing += async (_, e) =>
        {
            if (_isClosingGracefully || _vm.DrawnShapes.Count == 0) return;
            e.Cancel = true;
            var result = await ShowDialog("Подтверждение", "Сохранить перед выходом?", DialogButtons.YesNoCancel);
            if (result == DialogResult.Cancel) return;
            if (result == DialogResult.Yes && !await _vm.SaveToFileInternal()) return;
            _isClosingGracefully = true;
            e.Cancel = false;
            Dispatcher.UIThread.Post(Close);
        };
    }

    private void DrawingCanvas_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetPosition(DrawingCanvas);
        if (e.Source is Control { Tag: PointHandleTag tag })
        {
            _vm.StartEditingPoint(tag.Shape, tag.PointIndex);
        }
        else
        {
            _vm.OnCanvasPointerPressed(point);
        }
        RedrawCanvas();
    }

    private void DrawingCanvas_PointerMoved(object? sender, PointerEventArgs e)
    {
        _vm.OnCanvasPointerMoved(e.GetPosition(DrawingCanvas));
        _currentMousePosition = e.GetPosition(DrawingCanvas);
        RedrawCanvas();
    }

    private void DrawingCanvas_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _vm.OnCanvasPointerReleased();
        RedrawCanvas();
    }

    private void RedrawCanvas()
    {
        DrawingCanvas.Children.Clear();

        foreach (var shape in _vm.DrawnShapes)
        {
            switch (shape)
            {
                case Circle c when c.Points.Count == 2:
                    DrawCircle(c, c.IsSelected ? Brushes.DarkGreen : Brushes.Green, c.IsSelected ? 3 : 2);
                    DrawLabel(GeometryHelper.ComputeCentroid(c.Points), shape.Index.ToString());
                    break;
                case Triangle t when t.Points.Count == 3:
                    DrawPolygon(t.Points, t.IsSelected ? Brushes.DarkRed : Brushes.Red, t.IsSelected ? 3 : 2);
                    DrawLabel(GeometryHelper.ComputeCentroid(t.Points), shape.Index.ToString());
                    break;
                case Square s when s.Points.Count == 2:
                    var pts = GeometryHelper.GetSquareFromDiagonal(s.Points[0], s.Points[1]);
                    DrawPolygon(pts, s.IsSelected ? Brushes.DarkBlue : Brushes.Blue, s.IsSelected ? 3 : 2);
                    DrawLabel(GeometryHelper.ComputeCentroid(pts), shape.Index.ToString());
                    break;
            }
        }

        if (_vm.SelectedShape != null && _vm.IsEditingPoints)
        {
            for (int i = 0; i < _vm.SelectedShape.Points.Count; i++)
            {
                if (_vm.SelectedShape is Circle && i == 0) continue;
                var p = _vm.SelectedShape.Points[i];
                var handle = new Ellipse
                {
                    Width = 10,
                    Height = 10,
                    Fill = Brushes.White,
                    Stroke = Brushes.Black,
                    StrokeThickness = 1,
                    Tag = new PointHandleTag(_vm.SelectedShape, i)
                };
                Canvas.SetLeft(handle, p.X - 5);
                Canvas.SetTop(handle, p.Y - 5);
                DrawingCanvas.Children.Add(handle);
            }
        }

        if (_vm.CurrentShape != null && _currentMousePosition.HasValue)
        {
            var mouse = _currentMousePosition.Value;
            foreach (var p in _vm.CurrentShape.Points) DrawPoint(p);
            switch (_vm.CurrentShape)
            {
                case Triangle t when t.Points.Count == 1:
                    DrawLine(t.Points[0], mouse, Brushes.Gray, dashed: true);
                    break;
                case Triangle t when t.Points.Count == 2:
                    DrawPolygonPreview([.. t.Points, mouse], Brushes.Red);
                    break;
                case Circle c when c.Points.Count == 1:
                    var clampedOuter = GeometryHelper.ClampCircleOuterToNonNegativeArea(c.Points[0], mouse);
                    var radius = GeometryHelper.Distance(c.Points[0], clampedOuter);
                    DrawCirclePreview(c.Points[0], radius);
                    break;
                case Square s when s.Points.Count == 1:
                    var clampedC = GeometryHelper.ClampSquareOuterToNonNegativeArea(s.Points[0], mouse);
                    var sq = GeometryHelper.GetSquareFromDiagonal(s.Points[0], clampedC);
                    DrawPolygonPreview(sq, Brushes.Blue);
                    break;
            }
        }
    }

    // === Визуальные примитивы ===

    private void DrawPoint(Point p) => DrawingCanvas.Children.Add(new Ellipse
    {
        Width = 6,
        Height = 6,
        Fill = Brushes.Black,
        Stroke = Brushes.White,
        StrokeThickness = 1,
        [Canvas.LeftProperty] = p.X - 3,
        [Canvas.TopProperty] = p.Y - 3
    });

    private void DrawLine(Point p1, Point p2, IBrush brush, bool dashed) => DrawingCanvas.Children.Add(new Polyline
    {
        Points = [p1, p2],
        Stroke = brush,
        StrokeThickness = 1,
        StrokeDashArray = dashed ? [3, 3] : null
    });

    private void DrawCirclePreview(Point center, double radius) => DrawingCanvas.Children.Add(new Ellipse
    {
        Width = radius * 2,
        Height = radius * 2,
        Stroke = Brushes.Green,
        StrokeThickness = 1,
        StrokeDashArray = [3, 3],
        [Canvas.LeftProperty] = center.X - radius,
        [Canvas.TopProperty] = center.Y - radius
    });

    private void DrawCircle(Circle c, IBrush stroke, double thickness) => DrawingCanvas.Children.Add(new Ellipse
    {
        Width = c.Radius * 2,
        Height = c.Radius * 2,
        Stroke = stroke,
        StrokeThickness = thickness,
        [Canvas.LeftProperty] = c.Points[0].X - c.Radius,
        [Canvas.TopProperty] = c.Points[0].Y - c.Radius
    });

    private void DrawPolygon(IList<Point> points, IBrush brush, double thickness) => DrawingCanvas.Children.Add(new Polygon
    {
        Points = points,
        Stroke = brush,
        StrokeThickness = thickness,
        Fill = null
    });

    private void DrawPolygonPreview(IList<Point> points, IBrush brush) => DrawingCanvas.Children.Add(new Polygon
    {
        Points = points,
        Stroke = brush,
        StrokeThickness = 1,
        Fill = null,
        StrokeDashArray = [3, 3]
    });

    private void DrawLabel(Point center, string text) => DrawingCanvas.Children.Add(new TextBlock
    {
        Text = text,
        FontSize = 12,
        Foreground = Brushes.Black,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        [Canvas.LeftProperty] = center.X - 6,
        [Canvas.TopProperty] = center.Y - 6
    });



    private async Task<DialogResult> ShowDialog(string title, string message, DialogButtons buttons)
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

        var result = DialogResult.Cancel; 
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
            var btn = new Button { Content = content, Width = 80, IsDefault = isDefault };
            btn.Click += (_, _) => { result = res; dialog.Close(); };
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

    public enum DialogButtons { OK, YesNo, YesNoCancel }
    public enum DialogResult { None, OK, Yes, No, Cancel }
}