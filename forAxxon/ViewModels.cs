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
using System.Threading;
using System.Threading.Tasks;

namespace forAxxon.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private ShapeBase? _currentShape;
    private readonly List<ShapeBase> _drawnShapes = new();
    private ShapeBase? _selectedShape;

    public IStorageProvider? StorageProvider { get; set; }

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

    public ObservableCollection<ShapeBase> DrawnShapes { get; } = new();

    public ShapeBase? CurrentShape
    {
        get => _currentShape;
        private set => SetProperty(ref _currentShape, value);
    }

    [RelayCommand]
    private void DeleteSelectedShape()
    {
        if (SelectedShape != null)
        {
            _drawnShapes.Remove(SelectedShape);
            DrawnShapes.Remove(SelectedShape);
            SelectedShape = null;
        }
    }

    private bool _isLoading;
    private double _loadProgress;

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public double LoadProgress
    {
        get => _loadProgress;
        private set => SetProperty(ref _loadProgress, value);
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

    private async Task LoadFromFileInternal(string? filePath)
    {
        if (StorageProvider == null || IsLoading)
            return;

        IStorageFile? file = null;

        if (string.IsNullOrEmpty(filePath))
        {
            var options = new FilePickerOpenOptions
            {
                Title = "Загрузить фигуры",
                FileTypeFilter = new[]
                {
                new FilePickerFileType("JSON файлы") { Patterns = ["*.json"] }
            }
            };

            var files = await StorageProvider.OpenFilePickerAsync(options);
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

        IsLoading = true;
        LoadProgress = 0;

        try
        {
            string json;
            using (var stream = await file.OpenReadAsync())
            using (var reader = new StreamReader(stream))
            {
                json = await reader.ReadToEndAsync();
            }

            var dtoList = JsonSerializer.Deserialize<List<SerializableShape>>(json, JsonSettings.Options);
            if (dtoList == null || dtoList.Count == 0)
                return;

            DrawnShapes.Clear();
            _drawnShapes.Clear();

            int total = dtoList.Count;
            for (int i = 0; i < dtoList.Count; i++)
            {
                var dto = dtoList[i];
                try
                {
                    var shape = ShapeConverter.ToRuntimeModel(dto);
                    _drawnShapes.Add(shape);
                    DrawnShapes.Add(shape);
                    LoadProgress = (double)(i + 1) / total * 100;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка при загрузке фигуры: {ex.Message}");
                }
            }

            SelectedShape = null;
            SessionManager.SetLastFilePath(file.Path.LocalPath);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка загрузки: {ex}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task SaveToFile()
    {
        if (StorageProvider == null) return;

        var options = new FilePickerSaveOptions
        {
            Title = "Сохранить фигуры",
            FileTypeChoices = new[] { new FilePickerFileType("JSON файлы") { Patterns = ["*.json"] } },
            DefaultExtension = "json",
            ShowOverwritePrompt = true
        };

        var file = await StorageProvider.SaveFilePickerAsync(options);
        if (file == null) return;

        try
        {
            var dtoList = DrawnShapes.Select(ShapeConverter.ToDto).ToList();
            var json = JsonSerializer.Serialize(dtoList, JsonSettings.Options);

            using var stream = await file.OpenWriteAsync();
            using var writer = new StreamWriter(stream);
            await writer.WriteAsync(json);

            SessionManager.SetLastFilePath(file.Path.LocalPath);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Ошибка сохранения: {ex}");
        }
    }

    [RelayCommand]
    private void CancelDrawing() => CurrentShape = null;

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
            CurrentShape = CurrentShape switch
            {
                Circle => new Circle(),
                Triangle => new Triangle(),
                Square => new Square(),
                _ => null
            };
        }
    }
}