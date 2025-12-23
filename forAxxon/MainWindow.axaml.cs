using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using forAxxon.Models;
using forAxxon.services;
using forAxxon.Services;
using forAxxon.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace forAxxon.Views;

public class PointHandleTag
{
    public ShapeBase Shape { get; }
    public int PointIndex { get; }
    public PointHandleTag(ShapeBase shape, int index) => (Shape, PointIndex) = (shape, index);
}

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _vm;
    private Point? _currentMousePosition;
    private bool _isClosingGracefully;
    private bool _isRedrawScheduled;
    private bool _isAltPressed = false;
    private const double POINT_SIZE = 6;
    private const double HANDLE_SIZE = 10;
    private const double HALF_POINT_SIZE = POINT_SIZE / 2;
    private const double HALF_HANDLE_SIZE = HANDLE_SIZE / 2;
    private const double LABEL_OFFSET = 6;

    public MainWindow(IServiceProvider services)
    {
        InitializeComponent();


        var dialogService = services.GetRequiredService<IDialogService>();
        _vm = services.GetRequiredService<MainWindowViewModel>();

        if (dialogService is AvaloniaDialogService ads)
            ads.Owner = this;

        DataContext = _vm;
        _vm.StorageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;


        _vm.DrawnShapes.CollectionChanged += OnDrawnShapesCollectionChanged;
        _vm.PropertyChanged += OnViewModelPropertyChanged;

        this.Opened += async (_, _) =>
        {
            var path = SessionManager.GetLastFilePath();
            if (!string.IsNullOrEmpty(path)) await _vm.LoadFromFile(path);
            ScheduleRedraw();
        };
        this.AttachedToVisualTree += OnAttachedToVisualTree;
        this.DetachedFromVisualTree += OnDetachedFromVisualTree;
        this.Closing += ClosingHandler;
        this.Closed += OnWindowClosed;
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        this.KeyDown += OnKeyDown;
        this.KeyUp += OnKeyUp;
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        this.KeyDown -= OnKeyDown;
        this.KeyUp -= OnKeyUp;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.LeftAlt || e.Key == Key.RightAlt)
            _isAltPressed = true;
    }

    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.LeftAlt || e.Key == Key.RightAlt)
            _isAltPressed = false;
    }
    private void OnDrawnShapesCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        ScheduleRedraw();
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.SelectedShape))
            ScheduleRedraw();
    }

    private void DrawingCanvas_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetPosition(DrawingCanvas);

        bool isRightClick = e.GetCurrentPoint(this).Properties.IsRightButtonPressed;

        if (e.Source is Control { Tag: PointHandleTag tag })
        {
            _vm.StartEditingPoint(tag.Shape, tag.PointIndex);
        }
        else
        {
            _vm.OnCanvasPointerPressed(point, isRightClick);
        }
        ScheduleRedraw();
    }

    private void DrawingCanvas_PointerMoved(object? sender, PointerEventArgs e)
    {
        _vm.OnCanvasPointerMoved(e.GetPosition(DrawingCanvas));
        _currentMousePosition = e.GetPosition(DrawingCanvas);
        ScheduleRedraw();
    }

    private void DrawingCanvas_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _vm.OnCanvasPointerReleased();
        ScheduleRedraw();
    }

    private void ScheduleRedraw()
    {
        if (_isRedrawScheduled) return;
        _isRedrawScheduled = true;
        Dispatcher.UIThread.Post(() =>
        {
            RedrawCanvas();
            _isRedrawScheduled = false;
        }, DispatcherPriority.Render);
    }

    private void RedrawCanvas()
    {
        DrawingCanvas.Children.Clear();
        DrawAllShapes();
        DrawEditHandlesIfNeeded();
        DrawCurrentShapePreview();
    }

    private void DrawAllShapes()
    {
        foreach (var shape in _vm.DrawnShapes)
        {
            DrawShape(shape);
            DrawShapeLabel(shape);
        }
    }

    private void DrawShape(ShapeBase shape)
    {
        var (brush, thickness) = GetShapeAppearance(shape);

        switch (shape)
        {
            case Circle c when c.Points.Count == 2:
                DrawCircle(c, brush, thickness);
                break;
            case Triangle t when t.Points.Count == 3:
                DrawPolygon(t.Points, brush, thickness);
                break;
            case Square s when s.Points.Count == 2:
                var pts = GeometryHelper.GetSquareFromDiagonal(s.Points[0], s.Points[1]);
                DrawPolygon(pts, brush, thickness);
                break;
        }
    }

    private (IBrush Brush, double Thickness) GetShapeAppearance(ShapeBase shape)
    {
        IBrush baseBrush = shape switch
        {
            Circle => Brushes.Green,
            Triangle => Brushes.Red,
            Square => Brushes.Blue,
            _ => Brushes.Gray
        };

        IBrush darkBrush = shape switch
        {
            Circle => Brushes.DarkGreen,
            Triangle => Brushes.DarkRed,
            Square => Brushes.DarkBlue,
            _ => Brushes.DarkGray
        };

        return shape.IsSelected
            ? (darkBrush, 3.0)
            : (baseBrush, 2.0);
    }

    private void DrawShapeLabel(ShapeBase shape)
    {
        Point centroid = shape switch
        {
            Circle c => GeometryHelper.ComputeCentroid(c.Points),
            Triangle t => GeometryHelper.ComputeCentroid(t.Points),
            Square s => GeometryHelper.ComputeCentroid(GeometryHelper.GetSquareFromDiagonal(s.Points[0], s.Points[1])),
            _ => new Point(0, 0)
        };
        DrawLabel(centroid, shape.Index.ToString());
    }

    private void DrawEditHandlesIfNeeded()
    {
        if (_vm.SelectedShape == null || !_vm.IsEditingPoints) return;

        for (int i = 0; i < _vm.SelectedShape.Points.Count; i++)
        {
            if (_vm.SelectedShape is Circle && i == 0) continue;

            var p = _vm.SelectedShape.Points[i];
            var handle = new Ellipse
            {
                Width = HANDLE_SIZE,
                Height = HANDLE_SIZE,
                Fill = Brushes.White,
                Stroke = Brushes.Black,
                StrokeThickness = 1,
                Tag = new PointHandleTag(_vm.SelectedShape, i)
            };
            Canvas.SetLeft(handle, p.X - HALF_HANDLE_SIZE);
            Canvas.SetTop(handle, p.Y - HALF_HANDLE_SIZE);
            DrawingCanvas.Children.Add(handle);
        }
    }

    private void DrawCurrentShapePreview()
    {
        if (_vm.CurrentShape == null || !_currentMousePosition.HasValue) return;

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

    private void DrawPoint(Point p) => DrawingCanvas.Children.Add(new Ellipse
    {
        Width = POINT_SIZE,
        Height = POINT_SIZE,
        Fill = Brushes.Black,
        Stroke = Brushes.White,
        StrokeThickness = 1,
        [Canvas.LeftProperty] = p.X - HALF_POINT_SIZE,
        [Canvas.TopProperty] = p.Y - HALF_POINT_SIZE
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
        [Canvas.LeftProperty] = center.X - LABEL_OFFSET,
        [Canvas.TopProperty] = center.Y - LABEL_OFFSET
    });


    private async void ClosingHandler(object? sender, WindowClosingEventArgs e)
    {
        if (_isClosingGracefully || _vm.DrawnShapes.Count == 0) return;
        e.Cancel = true;

        var dialogService = new AvaloniaDialogService(this);
        var result = await dialogService.ShowConfirmationAsync(
            "Подтверждение",
            "Сохранить перед выходом?",
            DialogButtons.YesNoCancel
        );

        if (result == DialogResult.Cancel) return;
        if (result == DialogResult.Yes && !await _vm.SaveToFileInternal()) return;

        _isClosingGracefully = true;
        e.Cancel = false;
        Dispatcher.UIThread.Post(Close);
    }


    private void OnWindowClosed(object? sender, EventArgs e)
    {
        _vm.DrawnShapes.CollectionChanged -= OnDrawnShapesCollectionChanged;
        _vm.PropertyChanged -= OnViewModelPropertyChanged;
    }
}