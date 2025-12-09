using Avalonia.Data.Converters;
using forAxxon.ViewModels;
using System;
using System.Globalization;

namespace forAxxon.Converters;

public class NotConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b ? !b : false;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b ? !b : false;
}

public class ModeToClassConverter : IValueConverter
{
    public static readonly ModeToClassConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is InteractionMode mode && parameter is string target)
        {
            return mode.ToString() == target ? "active" : "";
        }
        return "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}