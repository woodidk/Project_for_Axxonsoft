using Avalonia;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using forAxxon.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace forAxxon.ViewModels;

public enum InteractionMode
{
    Idle,
    DrawingCircle,
    DrawingTriangle,
    DrawingSquare,
    EditingPoints
}

public partial class MainWindowViewModel : ObservableObject
{
    private ShapeBase? _currentShape;
    private ShapeBase? _selectedShape;
    private bool _isEditingPoints;
    private bool _isDragging;
    private ShapeBase? _draggedShape;
    private Point _dragStartOffset;
    private bool _isEditingPoint;
    private ShapeBase? _editingShape;
    private int _editingPointIndex;
    private Point? _lastClickPoint;
    private List<ShapeBase>? _shapesAtLastClick;
    private int _currentIndexInStack;
    private DateTime _lastClickTime = DateTime.MinValue;
    private InteractionMode _currentMode = InteractionMode.Idle;

    public IStorageProvider? StorageProvider { get; set; }
    public ObservableCollection<ShapeBase> DrawnShapes { get; } = new();

    public ShapeBase? SelectedShape
    {
        get => _selectedShape;
        set
        {
            if (_selectedShape != value)
            {
                if (_selectedShape != null)
                    _selectedShape.IsSelected = false;
                _selectedShape = value;
                if (_selectedShape != null)
                    _selectedShape.IsSelected = true;
                OnPropertyChanged();
            }
        }
    }

    public ShapeBase? CurrentShape
    {
        get => _currentShape;
        private set => SetProperty(ref _currentShape, value);
    }

    public InteractionMode CurrentMode
    {
        get => _currentMode;
        set
        {
            if (SetProperty(ref _currentMode, value))
            {
                OnPropertyChanged(nameof(CanStartCircle));
                OnPropertyChanged(nameof(CanStartTriangle));
                OnPropertyChanged(nameof(CanStartSquare));
                OnPropertyChanged(nameof(CanTogglePointEditing));
                OnPropertyChanged(nameof(CanCancelDrawing));
                OnPropertyChanged(nameof(CurrentModeText));
            }
        }
    }

    public string CurrentModeText => CurrentMode switch
    {
        InteractionMode.Idle => "Готово",
        InteractionMode.DrawingCircle => "Режим: Рисование круга",
        InteractionMode.DrawingTriangle => "Режим: Рисование треугольника",
        InteractionMode.DrawingSquare => "Режим: Рисование квадрата",
        InteractionMode.EditingPoints => "Режим: Редактирование точек",
        _ => "Неизвестно"
    };

    public bool IsInDrawingMode => CurrentMode is InteractionMode.DrawingCircle or InteractionMode.DrawingTriangle or InteractionMode.DrawingSquare;
    public bool IsInEditingMode => CurrentMode == InteractionMode.EditingPoints;
    public bool CanStartCircle => !IsInEditingMode;
    public bool CanStartTriangle => !IsInEditingMode;
    public bool CanStartSquare => !IsInEditingMode;
    public bool CanTogglePointEditing => !IsInDrawingMode;
    public bool CanCancelDrawing => IsInDrawingMode;

    private void InvalidateClickCache()
    {
        _lastClickPoint = null;
        _shapesAtLastClick = null;
        _currentIndexInStack = 0;
    }

    public bool IsEditingPoints
    {
        get => _isEditingPoints;
        set
        {
            if (SetProperty(ref _isEditingPoints, value))
            {
                if (value)
                    CurrentMode = InteractionMode.EditingPoints;
                else if (CurrentMode == InteractionMode.EditingPoints)
                    CurrentMode = InteractionMode.Idle;
            }
        }
    }

    public Func<Task<bool>>? RequestConfirmation { get; set; }

    [RelayCommand]
    private void TogglePointEditing() => IsEditingPoints = !IsEditingPoints;

    [RelayCommand]
    private void DeleteSelectedShape()
    {
        if (SelectedShape != null)
        {
            DrawnShapes.Remove(SelectedShape);
            SelectedShape = null;
            UpdateShapeIndices();
            InvalidateClickCache();
        }
    }

    [RelayCommand]
    private async Task LoadFromFile()
    {
        await LoadFromFileInternal(null);
    }

    public async Task LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath)) return;
        await LoadFromFileInternal(filePath);
    }

    [RelayCommand]
    private async Task SaveToFile()
    {
        await SaveToFileInternal();
    }

    [RelayCommand]
    private async Task ClearAll()
    {
        if (DrawnShapes.Count == 0) return;
        var confirmed = RequestConfirmation == null || await RequestConfirmation();
        if (confirmed)
        {
            DrawnShapes.Clear();
            SelectedShape = null;
            CurrentMode = InteractionMode.Idle;
            InvalidateClickCache();
        }
    }

    [RelayCommand]
    private void CancelDrawing()
    {
        CurrentShape = null;
        CurrentMode = InteractionMode.Idle;
    }

    [RelayCommand(CanExecute = nameof(CanStartCircle))]
    private void StartCircle()
    {
        CurrentMode = InteractionMode.DrawingCircle;
        CurrentShape = new Circle();
    }

    [RelayCommand(CanExecute = nameof(CanStartTriangle))]
    private void StartTriangle()
    {
        CurrentMode = InteractionMode.DrawingTriangle;
        CurrentShape = new Triangle();
    }

    [RelayCommand(CanExecute = nameof(CanStartSquare))]
    private void StartSquare()
    {
        CurrentMode = InteractionMode.DrawingSquare;
        CurrentShape = new Square();
    }

    public void OnCanvasPointerPressed(Point point)
    {
        if (CurrentShape != null)
        {
            AddPointToCurrentShape(point);
            TryCompleteCurrentShape();
            InvalidateClickCache();
            return;
        }

        if (_isEditingPoint)
        {
            InvalidateClickCache();
            return;
        }

        var shapesUnderCursor = DrawnShapes
            .Where(s => ShapeHitTester.IsPointInShape(point, s))
            .Reverse()
            .ToList();

        if (IsEditingPoints)
        {
            if (shapesUnderCursor.Count == 0)
            {
                SelectedShape = null;
            }
            else
            {
                bool isSamePoint = _lastClickPoint.HasValue &&
                    Math.Abs(_lastClickPoint.Value.X - point.X) < 1e-3 &&
                    Math.Abs(_lastClickPoint.Value.Y - point.Y) < 1e-3;
                if (isSamePoint && (DateTime.Now - _lastClickTime) < TimeSpan.FromMilliseconds(600))
                {
                    _currentIndexInStack = (_currentIndexInStack + 1) % shapesUnderCursor.Count;
                }
                else
                {
                    _currentIndexInStack = 0;
                }
                _lastClickPoint = point;
                _lastClickTime = DateTime.Now;
                SelectedShape = shapesUnderCursor[_currentIndexInStack];
            }
            InvalidateClickCache();
            return;
        }

        if (shapesUnderCursor.Count == 0)
        {
            SelectedShape = null;
            _isDragging = false;
            _draggedShape = null;
        }
        else
        {
            bool isSamePoint = _lastClickPoint.HasValue &&
                Math.Abs(_lastClickPoint.Value.X - point.X) < 1e-3 &&
                Math.Abs(_lastClickPoint.Value.Y - point.Y) < 1e-3;
            if (isSamePoint && (DateTime.Now - _lastClickTime) < TimeSpan.FromMilliseconds(600))
            {
                _currentIndexInStack = (_currentIndexInStack + 1) % shapesUnderCursor.Count;
            }
            else
            {
                _currentIndexInStack = 0;
            }
            _lastClickPoint = point;
            _lastClickTime = DateTime.Now;
            SelectedShape = shapesUnderCursor[_currentIndexInStack];
            StartDragging(SelectedShape, point);
        }
        InvalidateClickCache();
    }

    public void OnCanvasPointerMoved(Point point)
    {
        if (_isEditingPoint && _editingShape != null)
        {
            MoveEditingPoint(point);
            return;
        }
        if (_isDragging && _draggedShape != null)
        {
            DragShapeToPoint(point);
            return;
        }
    }

    public void OnCanvasPointerReleased()
    {
        _isDragging = false;
        _draggedShape = null;
        _isEditingPoint = false;
        _editingShape = null;
    }

    public void StartEditingPoint(ShapeBase shape, int pointIndex)
    {
        if (IsEditingPoints)
        {
            _isEditingPoint = true;
            _editingShape = shape;
            _editingPointIndex = pointIndex;
        }
    }

    private void AddPointToCurrentShape(Point point)
    {
        if (CurrentShape is Circle circle && circle.Points.Count == 1)
        {
            var clamped = GeometryHelper.ClampCircleOuterToNonNegativeArea(circle.Points[0], point);
            CurrentShape.Points.Add(clamped);
        }
        else if (CurrentShape is Square square && square.Points.Count == 1)
        {
            var clamped = GeometryHelper.ClampSquareOuterToNonNegativeArea(square.Points[0], point);
            CurrentShape.Points.Add(clamped);
        }
        else
        {
            CurrentShape.Points.Add(point);
        }
        InvalidateClickCache();
    }

    private void TryCompleteCurrentShape()
    {
        if (CurrentShape == null) return;
        int required = CurrentShape switch
        {
            Circle => 2,
            Triangle => 3,
            Square => 2,
            _ => 0
        };
        if (CurrentShape.Points.Count != required) return;
        if (CurrentShape is Circle circle && circle.Points.Count == 2)
        {
            circle.Points[1] = GeometryHelper.ClampCircleOuterToNonNegativeArea(circle.Points[0], circle.Points[1]);
        }
        else if (CurrentShape is Square square && square.Points.Count == 2)
        {
            square.Points[1] = GeometryHelper.ClampSquareOuterToNonNegativeArea(square.Points[0], square.Points[1]);
        }
        DrawnShapes.Add(CurrentShape);
        CurrentShape.Index = DrawnShapes.Count;
        CurrentShape = CurrentShape switch
        {
            Circle => new Circle(),
            Triangle => new Triangle(),
            Square => new Square(),
            _ => null
        };
        CurrentMode = InteractionMode.Idle;
        InvalidateClickCache();
    }

    private void StartDragging(ShapeBase shape, Point pointerPosition)
    {
        _isDragging = true;
        _draggedShape = shape;
        Point centroid;
        if (shape is Square square && square.Points.Count == 2)
        {
            centroid = new Point(
                (square.Points[0].X + square.Points[1].X) / 2,
                (square.Points[0].Y + square.Points[1].Y) / 2
            );
        }
        else if (shape is Circle circle && circle.Points.Count == 2)
        {
            centroid = circle.Points[0];
        }
        else
        {
            centroid = GeometryHelper.ComputeCentroid(shape.Points);
        }
        _dragStartOffset = new Point(pointerPosition.X - centroid.X, pointerPosition.Y - centroid.Y);
    }

    private void DragShapeToPoint(Point pointerPosition)
    {
        if (_draggedShape == null) return;
        var newCentroid = new Point(
            pointerPosition.X - _dragStartOffset.X,
            pointerPosition.Y - _dragStartOffset.Y
        );
        if (_draggedShape is Circle circle && circle.Points.Count == 2)
        {
            var offset = new Point(circle.Points[1].X - circle.Points[0].X, circle.Points[1].Y - circle.Points[0].Y);
            circle.Points = new List<Point>
            {
                newCentroid,
                new Point(newCentroid.X + offset.X, newCentroid.Y + offset.Y)
            };
            circle.Points[1] = GeometryHelper.ClampCircleOuterToNonNegativeArea(circle.Points[0], circle.Points[1]);
        }
        else if (_draggedShape is Square square && square.Points.Count == 2)
        {
            var oldCenter = new Point(
                (square.Points[0].X + square.Points[1].X) / 2,
                (square.Points[0].Y + square.Points[1].Y) / 2
            );
            var dx = newCentroid.X - oldCenter.X;
            var dy = newCentroid.Y - oldCenter.Y;
            var newA = new Point(square.Points[0].X + dx, square.Points[0].Y + dy);
            var newC = new Point(square.Points[1].X + dx, square.Points[1].Y + dy);
            square.Points = new List<Point> { newA, newC };
            var fullSquare = GeometryHelper.GetSquareFromDiagonal(newA, newC);
            double minX = fullSquare.Min(p => p.X);
            double minY = fullSquare.Min(p => p.Y);
            double shiftX = Math.Max(0, -minX);
            double shiftY = Math.Max(0, -minY);
            if (shiftX > 0 || shiftY > 0)
            {
                square.Points[0] = new Point(square.Points[0].X + shiftX, square.Points[0].Y + shiftY);
                square.Points[1] = new Point(square.Points[1].X + shiftX, square.Points[1].Y + shiftY);
            }
        }
        else
        {
            var oldCentroid = GeometryHelper.ComputeCentroid(_draggedShape.Points);
            var dx = newCentroid.X - oldCentroid.X;
            var dy = newCentroid.Y - oldCentroid.Y;
            _draggedShape.Points = _draggedShape.Points.Select(p => new Point(p.X + dx, p.Y + dy)).ToList();
            double minX = _draggedShape.Points.Min(p => p.X);
            double minY = _draggedShape.Points.Min(p => p.Y);
            double shiftX = Math.Max(0, -minX);
            double shiftY = Math.Max(0, -minY);
            if (shiftX > 0 || shiftY > 0)
            {
                _draggedShape.Points = _draggedShape.Points.Select(p => new Point(p.X + shiftX, p.Y + shiftY)).ToList();
            }
        }
    }

    private void MoveEditingPoint(Point point)
    {
        if (_editingShape == null || _editingPointIndex < 0) return;
        if (_editingShape is Circle circle && _editingPointIndex == 1)
        {
            point = GeometryHelper.ClampCircleOuterToNonNegativeArea(circle.Points[0], point);
            var newPoints = new List<Point>(_editingShape.Points) { [1] = point };
            _editingShape.Points = newPoints;
        }
        else if (_editingShape is Square square && _editingPointIndex == 1 && square.Points.Count >= 1)
        {
            point = GeometryHelper.ClampSquareOuterToNonNegativeArea(square.Points[0], point);
            var newPoints = new List<Point>(square.Points) { [1] = point };
            _editingShape.Points = newPoints;
        }
        else
        {
            var newPoints = new List<Point>(_editingShape.Points);
            newPoints[_editingPointIndex] = point;
            _editingShape.Points = newPoints;
        }
    }

    private void UpdateShapeIndices()
    {
        for (int i = 0; i < DrawnShapes.Count; i++)
            DrawnShapes[i].Index = i + 1;
    }

    private async Task LoadFromFileInternal(string? filePath)
    {
        if (StorageProvider == null) return;
        IStorageFile? file = null;
        if (string.IsNullOrEmpty(filePath))
        {
            var files = await StorageProvider.OpenFilePickerAsync(new()
            {
                FileTypeFilter = [new FilePickerFileType("JSON файлы") { Patterns = ["*.json"] }]
            });
            if (files.Count == 0) return;
            file = files[0];
        }
        else
        {
            try
            {
                file = await StorageProvider.TryGetFileFromPathAsync(new Uri(filePath));
            }
            catch
            {
                return;
            }
        }
        if (file == null) return;
        try
        {
            var json = await File.ReadAllTextAsync(file.Path.LocalPath);
            var dtoList = JsonSerializer.Deserialize<List<SerializableShape>>(json, JsonSettings.Options) ?? new List<SerializableShape>();
            DrawnShapes.Clear();
            foreach (var dto in dtoList)
            {
                DrawnShapes.Add(ShapeConverter.ToRuntimeModel(dto));
            }
            SelectedShape = null;
            UpdateShapeIndices();
            SessionManager.SetLastFilePath(file.Path.LocalPath);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка загрузки: {ex}");
        }
    }

    public async Task<bool> SaveToFileInternal()
    {
        if (StorageProvider == null) return false;
        var file = await StorageProvider.SaveFilePickerAsync(new()
        {
            FileTypeChoices = [new FilePickerFileType("JSON файлы") { Patterns = ["*.json"] }],
            DefaultExtension = "json",
            ShowOverwritePrompt = true
        });
        if (file == null) return false;
        try
        {
            var dtoList = DrawnShapes.Select(ShapeConverter.ToDto).ToList();
            var json = JsonSerializer.Serialize(dtoList, JsonSettings.Options);
            await File.WriteAllTextAsync(file.Path.LocalPath, json);
            SessionManager.SetLastFilePath(file.Path.LocalPath);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка сохранения: {ex}");
            return false;
        }
    }
}