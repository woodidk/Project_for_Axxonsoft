using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using forAxxon.Models;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace forAxxon.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private ShapeBase? _currentShape;
    private readonly List<ShapeBase> _drawnShapes = new();

    public ObservableCollection<ShapeBase> DrawnShapes { get; } = new();

    public ShapeBase? CurrentShape
    {
        get => _currentShape;
        private set => SetProperty(ref _currentShape, value);
    }
    [RelayCommand]
    private void StartCircle() => CurrentShape = new Circle();

    [RelayCommand]
    private void StartTriangle() => CurrentShape = new Triangle();

    [RelayCommand]
    private void StartSquare() => CurrentShape = new Square();

    public void HandleCanvasPointerPressed(Point point)
    {
        foreach (var shape in DrawnShapes)
            shape.IsSelected = false;
        if (CurrentShape == null) return;

        CurrentShape.Points.Add(point);

        int required = CurrentShape switch
        {
            Circle => 2,
            Triangle => 3,
            Square => 2,
            _ => 0
        };

        if (CurrentShape.Points.Count == required)
        {
            _drawnShapes.Add(CurrentShape);
            DrawnShapes.Add(CurrentShape);
            CurrentShape = null;
        }
    }
}