using Avalonia;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace forAxxon.Models;

public abstract class ShapeBase : INotifyPropertyChanged
{
    private bool _isSelected;
    private List<Point> _points = new();

    public List<Point> Points
    {
        get => _points;
        set
        {
            _points = value ?? new List<Point>();
            OnPropertyChanged();
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }
    }

    public abstract string Name { get; }


    public string TypeName => Name;


    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class Triangle : ShapeBase
{
    public override string Name => "Треугольник";
}

public class Circle : ShapeBase
{
    public override string Name => "Круг";
}

public class Square : ShapeBase
{
    public override string Name => "Квадрат";
}