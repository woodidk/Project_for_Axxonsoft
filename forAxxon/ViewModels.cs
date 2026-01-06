using Avalonia;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using forAxxon.Models;
using forAxxon.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

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
    private const int DOUBLE_CLICK_THRESHOLD_MS = 600;
    private const double POINT_COMPARISON_TOLERANCE = 1e-3;
    private const long MAX_FILE_SIZE = 10 * 1024 * 1024; // 10 МБ

    private readonly IDialogService _dialogService;
    private ShapeBase? _lastSelectedShapeForDrag;
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
    private DateTime _lastClickTime = DateTime.MinValue;
    private InteractionMode _currentMode = InteractionMode.Idle;
    private bool _skipAllErrors = false;
    private Point? _lastClickPositionForStack;
    private List<ShapeBase>? _shapesAtLastClick;
    private int _currentIndexInStack;

    // === ЗАЛИВКА: ручное объявление свойства ===
    private Color? _selectedFillColor = Colors.Transparent;
    public Color? SelectedFillColor
    {
        get => _selectedFillColor;
        set
        {
            if (SetProperty(ref _selectedFillColor, value))
            {
                if (SelectedShape != null)
                {
                    SelectedShape.FillColor = value;
                    OnPropertyChanged(nameof(DrawnShapes));
                }
            }
        }
    }

    public List<Color?> FillColorOptions { get; } = new()
    {
        null, // Без заливки
        Colors.Transparent,
        Colors.White,
        Colors.Black,
        Colors.Red,
        Colors.Green,
        Colors.Blue,
        Colors.Yellow,
        Colors.Cyan,
        Colors.Magenta,
        Colors.Gray
    };

    [ObservableProperty]
    private double _loadProgress;

    [ObservableProperty]
    private bool _isLoading;

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

    public MainWindowViewModel(IDialogService dialogService)
    {
        _dialogService = dialogService;
    }

    private void InvalidateClickCache()
    {
        _lastClickPoint = null;
        _shapesAtLastClick = null;
        _lastClickPositionForStack = null;
        _currentIndexInStack = 0;
        _lastSelectedShapeForDrag = null;
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

        var result = await _dialogService.ShowConfirmationAsync(
            "Подтверждение",
            "Удалить все фигуры?",
            DialogButtons.YesNo
        );

        if (result == DialogResult.Yes)
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
        CurrentShape = new Circle { FillColor = SelectedFillColor };
    }

    [RelayCommand(CanExecute = nameof(CanStartTriangle))]
    private void StartTriangle()
    {
        CurrentMode = InteractionMode.DrawingTriangle;
        CurrentShape = new Triangle { FillColor = SelectedFillColor };
    }

    [RelayCommand(CanExecute = nameof(CanStartSquare))]
    private void StartSquare()
    {
        CurrentMode = InteractionMode.DrawingSquare;
        CurrentShape = new Square { FillColor = SelectedFillColor };
    }

    public void OnCanvasPointerPressed(Point point, bool isRightClick = false)
    {
        if (CurrentShape != null)
        {
            HandleDrawingModeClick(point);
            InvalidateClickCache();
            return;
        }

        if (_isEditingPoint)
        {
            InvalidateClickCache();
            return;
        }

        var shapesUnderCursor = GetShapesUnderPoint(point);

        if (shapesUnderCursor.Count == 0)
        {
            SelectedShape = null;
            _lastSelectedShapeForDrag = null;
            _isDragging = false;
            _draggedShape = null;
            return;
        }

        if (isRightClick)
        {
            _currentIndexInStack = (_currentIndexInStack + 1) % shapesUnderCursor.Count;
            SelectedShape = shapesUnderCursor[_currentIndexInStack];
            _lastSelectedShapeForDrag = SelectedShape;
            _isDragging = false;
            _draggedShape = null;
        }
        else
        {
            bool clickedOnSelected = SelectedShape != null &&
                                    shapesUnderCursor.Contains(SelectedShape);

            if (clickedOnSelected)
            {
                if (!IsEditingPoints)
                {
                    StartDragging(SelectedShape, point);
                }
            }
            else
            {
                SelectedShape = shapesUnderCursor[0];
                _lastSelectedShapeForDrag = SelectedShape;
                if (!IsEditingPoints)
                {
                    StartDragging(SelectedShape, point);
                }
            }
        }

        _lastClickPoint = point;
        _lastClickTime = DateTime.Now;
    }

    private List<ShapeBase> GetShapesUnderPoint(Point point)
    {
        return DrawnShapes
            .Where(s => ShapeHitTester.IsPointInShape(point, s))
            .Reverse()
            .ToList();
    }

    private void HandleDrawingModeClick(Point point)
    {
        AddPointToCurrentShape(point);
        TryCompleteCurrentShape();
    }

    private void HandleEditingModeClick(Point point, List<ShapeBase> shapesUnderCursor)
    {
        if (shapesUnderCursor.Count == 0)
        {
            SelectedShape = null;
        }
        else
        {
            HandleShapeSelection(point, shapesUnderCursor);
            SelectedShape = shapesUnderCursor[_currentIndexInStack];
        }
    }

    private void HandleSelectionModeClick(Point point, List<ShapeBase> shapesUnderCursor)
    {
        if (shapesUnderCursor.Count == 0)
        {
            SelectedShape = null;
            _isDragging = false;
            _draggedShape = null;
        }
        else
        {
            HandleShapeSelection(point, shapesUnderCursor);
            SelectedShape = shapesUnderCursor[_currentIndexInStack];
            StartDragging(SelectedShape, point);
        }
    }

    private void HandleShapeSelection(Point point, List<ShapeBase> shapesUnderCursor)
    {
        if (shapesUnderCursor.Count == 0)
        {
            _currentIndexInStack = 0;
            return;
        }

        bool isSamePoint = _lastClickPoint.HasValue &&
            Math.Abs(_lastClickPoint.Value.X - point.X) < POINT_COMPARISON_TOLERANCE &&
            Math.Abs(_lastClickPoint.Value.Y - point.Y) < POINT_COMPARISON_TOLERANCE;

        if (isSamePoint && (DateTime.Now - _lastClickTime) < TimeSpan.FromMilliseconds(DOUBLE_CLICK_THRESHOLD_MS))
        {
            _currentIndexInStack = (_currentIndexInStack + 1) % shapesUnderCursor.Count;
        }
        else
        {
            _currentIndexInStack = 0;
        }

        _lastClickPoint = point;
        _lastClickTime = DateTime.Now;
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

        // Применяем заливку при финализации
        CurrentShape.FillColor = SelectedFillColor;

        DrawnShapes.Add(CurrentShape);
        CurrentShape.Index = DrawnShapes.Count;
        CurrentShape = CurrentShape switch
        {
            Circle => new Circle { FillColor = SelectedFillColor },
            Triangle => new Triangle { FillColor = SelectedFillColor },
            Square => new Square { FillColor = SelectedFillColor },
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
            DragCircle(circle, newCentroid);
        }
        else if (_draggedShape is Square square && square.Points.Count == 2)
        {
            DragSquare(square, newCentroid);
        }
        else
        {
            DragGenericShape(_draggedShape, newCentroid);
        }
    }

    private void DragCircle(Circle circle, Point newCentroid)
    {
        var originalRadius = GeometryHelper.Distance(circle.Points[0], circle.Points[1]);
        var clampedCentroid = new Point(
            Math.Max(originalRadius, newCentroid.X),
            Math.Max(originalRadius, newCentroid.Y)
        );
        var direction = new Point(
            (circle.Points[1].X - circle.Points[0].X) / originalRadius,
            (circle.Points[1].Y - circle.Points[0].Y) / originalRadius
        );
        var outerPoint = new Point(
            clampedCentroid.X + direction.X * originalRadius,
            clampedCentroid.Y + direction.Y * originalRadius
        );
        circle.Points = new List<Point> { clampedCentroid, outerPoint };
    }

    private void DragSquare(Square square, Point newCentroid)
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

    private void DragGenericShape(ShapeBase shape, Point newCentroid)
    {
        var oldCentroid = GeometryHelper.ComputeCentroid(shape.Points);
        var dx = newCentroid.X - oldCentroid.X;
        var dy = newCentroid.Y - oldCentroid.Y;
        shape.Points = shape.Points.Select(p => new Point(p.X + dx, p.Y + dy)).ToList();
        double minX = shape.Points.Min(p => p.X);
        double minY = shape.Points.Min(p => p.Y);
        double shiftX = Math.Max(0, -minX);
        double shiftY = Math.Max(0, -minY);
        if (shiftX > 0 || shiftY > 0)
        {
            shape.Points = shape.Points.Select(p => new Point(p.X + shiftX, p.Y + shiftY)).ToList();
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
        else if (_editingShape is Square square && _editingPointIndex == 1 && square.Points.Count >= 2)
        {
            var testSquare = GeometryHelper.GetSquareFromDiagonal(square.Points[0], point);
            bool isValid = testSquare.All(p => p.X >= 0 && p.Y >= 0);
            if (isValid)
            {
                var newPoints = new List<Point>(square.Points) { [1] = point };
                _editingShape.Points = newPoints;
            }
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
            var fileInfo = new FileInfo(file.Path.LocalPath);
            if (fileInfo.Length > MAX_FILE_SIZE)
            {
                await _dialogService.ShowConfirmationAsync(
                    "Ошибка",
                    $"Файл превышает допустимый размер ({MAX_FILE_SIZE / 1024 / 1024} МБ).",
                    DialogButtons.OK);
                return;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка проверки размера файла: {ex}");
            return;
        }

        IsLoading = true;
        LoadProgress = 0;

        var validShapes = new List<ShapeBase>();
        _skipAllErrors = false;

        try
        {
            string json = await File.ReadAllTextAsync(file.Path.LocalPath);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Array)
            {
                await _dialogService.ShowConfirmationAsync(
                    "Ошибка формата",
                    "Файл должен содержать JSON-массив фигур.",
                    DialogButtons.OK);
                return;
            }

            var array = root.EnumerateArray();
            int index = 0;
            int total = root.GetArrayLength();

            foreach (var element in array)
            {
                index++;

                try
                {
                    var dto = element.Deserialize<SerializableShape>(JsonSettings.Options);
                    if (dto == null)
                        throw new InvalidOperationException("Фигура не может быть null.");

                    var shape = ShapeConverter.ToRuntimeModel(dto);
                    validShapes.Add(shape);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка при загрузке фигуры #{index}: {ex.Message}");

                    if (!_skipAllErrors)
                    {
                        var dialogResult = await _dialogService.ShowSkipOptionsDialogAsync(
                            "Ошибка загрузки фигуры",
                            $"Фигура #{index} повреждена:\n{ex.Message}"
                        );

                        if (dialogResult == DialogResult.Cancel)
                        {
                            DrawnShapes.Clear();
                            SelectedShape = null;
                            return;
                        }
                        else if (dialogResult == DialogResult.No)
                        {
                            _skipAllErrors = true;
                        }
                    }
                }
                if (total > 0)
                {
                    LoadProgress = (double)index / total * 100;
                }
            }
            DrawnShapes.Clear();
            foreach (var shape in validShapes)
            {
                DrawnShapes.Add(shape);
            }
            SelectedShape = null;
            UpdateShapeIndices();
            SessionManager.SetLastFilePath(file.Path.LocalPath);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Критическая ошибка при загрузке: {ex}");
            await _dialogService.ShowConfirmationAsync(
                "Ошибка",
                $"Не удалось загрузить файл:\n{ex.Message}",
                DialogButtons.OK);
        }
        finally
        {
            IsLoading = false;
            LoadProgress = 0;
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