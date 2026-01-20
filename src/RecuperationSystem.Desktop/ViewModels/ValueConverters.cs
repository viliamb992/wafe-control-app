using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace RecuperationSystem.Desktop.ViewModels;

/// <summary>
/// Converts a boolean to a Brush resource by name.
/// Usage: ConverterParameter='TrueResourceKey|FalseResourceKey' (e.g., 'SystemRunningGradient|SystemStoppedGradient')
/// Falls back to direct color parsing if resources not found.
/// </summary>
public class BoolToBrushConverter : IValueConverter
{
    public static readonly BoolToBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isTrue = value is bool b && b;
        
        if (parameter is string resourcePair && resourcePair.Contains('|'))
        {
            var resources = resourcePair.Split('|');
            if (resources.Length == 2)
            {
                var resourceKey = isTrue ? resources[0].Trim() : resources[1].Trim();
                
                // Try to find the resource in the application resources
                if (Application.Current?.Resources.TryGetResource(resourceKey, null, out var resource) == true)
                {
                    return resource as IBrush;
                }
                
                // Fallback: try to parse as color if resource not found
                if (resourceKey.StartsWith('#'))
                {
                    return new SolidColorBrush(Color.Parse(resourceKey));
                }
            }
        }
        
        // Default fallback
        return isTrue ? Brushes.Red : Brushes.Green;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException("BoolToBrushConverter does not support two-way binding.");
    }
}

/// <summary>
/// Legacy converter - kept for backward compatibility.
/// Consider using BoolToBrushConverter with resource references instead.
/// </summary>
[Obsolete("Use BoolToBrushConverter with resource references instead")]
public class BoolToColorConverter : IValueConverter
{
    public static readonly BoolToColorConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isTrue = value is bool b && b;
        
        if (parameter is string colorPair && colorPair.Contains('|'))
        {
            var colors = colorPair.Split('|');
            if (colors.Length == 2)
            {
                var colorString = isTrue ? colors[0].Trim() : colors[1].Trim();
                return Color.Parse(colorString);
            }
        }
        
        return isTrue ? Colors.Red : Colors.Green;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public class BoolToMarginConverter : IValueConverter
{
    public static readonly BoolToMarginConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isTrue = value is bool b && b;
        return isTrue ? new Thickness(0) : new Thickness(2, 0, 0, 0);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException("BoolToMarginConverter does not support two-way binding.");
    }
}

/// <summary>
/// Converts a boolean to opacity. True = 0.5 (dimmed), False = 1.0 (normal)
/// Useful for showing loading/disabled states
/// </summary>
public class BoolToOpacityConverter : IValueConverter
{
    public static readonly BoolToOpacityConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isTrue = value is bool b && b;
        // When true (loading/disabled), dim to 0.5, otherwise normal (1.0)
        return isTrue ? 0.5 : 1.0;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException("BoolToOpacityConverter does not support two-way binding.");
    }
}
