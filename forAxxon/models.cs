using Avalonia;
using Avalonia.Media;
using Avalonia.Controls.Shapes;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace forAxxon.Models;

public interface IShape
{
    string Type { get; }
    string Name { get; }
    string DisplayName { get; }
    int Index { get; set; }
    List<Point> Points { get; set; }
    bool IsSelected { get; set; }
    Color? FillColor { get; set; }

    Point ComputeCentroid();
    void NotifyPointsChanged();
}

public class PointJsonConverter : JsonConverter<Point>
{
    public override Point Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Ожидался JSON-объект для Point.");

        double x = 0, y = 0;
        bool hasX = false, hasY = false;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                break;
            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException("Некорректный формат объекта Point.");

            string propertyName = reader.GetString();
            reader.Read();

            switch (propertyName)
            {
                case "X":
                    x = reader.GetDouble();
                    hasX = true;
                    break;
                case "Y":
                    y = reader.GetDouble();
                    hasY = true;
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        if (!hasX || !hasY)
            throw new JsonException("Point должен содержать оба поля: X и Y.");

        return new Point(x, y);
    }

    public override void Write(Utf8JsonWriter writer, Point value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("X", value.X);
        writer.WriteNumber("Y", value.Y);
        writer.WriteEndObject();
    }
}

public static class JsonSettings
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new PointJsonConverter() }
    };
}

public record SerializableShape(string Type, List<Point> Points, string? FillColor = null);

public abstract class ShapeBase : ObservableObject, IShape
{
    private bool _isSelected;
    private List<Point> _points = new();
    private int _index;
    private Color? _fillColor; 

    public virtual string Type => GetType().Name;
    public string DisplayName => $"{Index}. {Name}";

    public List<Point> Points
    {
        get => _points;
        set => SetProperty(ref _points, value);
    }

    public int Index
    {
        get => _index;
        set
        {
            if (SetProperty(ref _index, value))
                OnPropertyChanged(nameof(DisplayName));
        }
    }

    public abstract string Name { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
    public Color? FillColor
    {
        get => _fillColor;
        set => SetProperty(ref _fillColor, value);
    }

    public bool HasFill => FillColor.HasValue;

    public Point ComputeCentroid()
    {
        return this switch
        {
            Circle c => c.Points[0],
            Triangle t => GeometryHelper.ComputeCentroid(t.Points),
            Square s => GeometryHelper.ComputeCentroid(GeometryHelper.GetSquareFromDiagonal(s.Points[0], s.Points[1])),
            _ => new Point(0, 0)
        };
    }

    public void NotifyPointsChanged() => OnPropertyChanged(nameof(Points));
}

public class Triangle : ShapeBase
{
    public override string Name => "Треугольник";
}

public class Circle : ShapeBase
{
    public override string Name => "Круг";

    public double Radius => Points.Count >= 2 ? GeometryHelper.Distance(Points[0], Points[1]) : 0;
}

public class Square : ShapeBase
{
    public override string Name => "Квадрат";
}

public static class ShapeConverter
{
    public static ShapeBase ToRuntimeModel(SerializableShape dto)
    {
        if (dto == null)
            throw new ArgumentNullException(nameof(dto));

        if (string.IsNullOrWhiteSpace(dto.Type))
            throw new ArgumentException("Тип фигуры не может быть пустым", nameof(dto.Type));

        if (dto.Points == null)
            throw new ArgumentException("Точки фигуры не могут быть null", nameof(dto.Points));

        if (dto.Points.Count == 0)
            throw new ArgumentException("Фигура должна содержать хотя бы одну точку", nameof(dto.Points));

        ShapeBase shape = dto.Type switch
        {
            nameof(Circle) => new Circle(),
            nameof(Triangle) => new Triangle(),
            nameof(Square) => new Square(),
            _ => throw new NotSupportedException($"Неизвестный тип фигуры: {dto.Type}")
        };

        int expectedCount = shape switch
        {
            Circle => 2,
            Triangle => 3,
            Square => 2,
            _ => throw new InvalidOperationException("Неподдерживаемый тип фигуры")
        };

        if (dto.Points.Count != expectedCount)
        {
            throw new InvalidOperationException(
                $"{dto.Type} требует ровно {expectedCount} точек, получено: {dto.Points.Count}");
        }

        foreach (var p in dto.Points)
        {
            if (double.IsNaN(p.X) || double.IsNaN(p.Y) ||
                double.IsInfinity(p.X) || double.IsInfinity(p.Y))
            {
                throw new ArgumentException("Координаты точек не могут быть NaN или бесконечностью");
            }
        }

        shape.Points = dto.Points;

        if (!string.IsNullOrEmpty(dto.FillColor) && Color.TryParse(dto.FillColor, out var color))
        {
            shape.FillColor = color;
        }

        return shape;
    }

    public static SerializableShape ToDto(ShapeBase shape)
    {
        if (shape == null)
            throw new ArgumentNullException(nameof(shape));

        return new SerializableShape(
            Type: shape.Type,
            Points: shape.Points?.ToList() ?? new List<Point>(),
            FillColor: shape.FillColor?.ToString() 
        );
    }
}