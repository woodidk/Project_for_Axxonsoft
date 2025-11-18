using Avalonia;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace forAxxon.Models;

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


public record SerializableShape
{
    public string Type { get; init; } = string.Empty;
    public List<Point> Points { get; init; } = new();
}


public static class ShapeConverter
{
    public static ShapeBase ToRuntimeModel(SerializableShape dto)
    {
        return dto.Type switch
        {
            "Circle" => new Circle { Points = dto.Points },
            "Triangle" => new Triangle { Points = dto.Points },
            "Square" => new Square { Points = dto.Points },
            _ => throw new NotSupportedException($"Неизвестный тип фигуры: {dto.Type}")
        };
    }

    public static SerializableShape ToDto(ShapeBase shape)
    {
        return new SerializableShape
        {
            Type = shape switch
            {
                Circle => "Circle",
                Triangle => "Triangle",
                Square => "Square",
                _ => throw new NotSupportedException($"Неизвестная фигура: {shape.GetType()}")
            },
            Points = shape.Points.ToList()
        };
    }
}


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