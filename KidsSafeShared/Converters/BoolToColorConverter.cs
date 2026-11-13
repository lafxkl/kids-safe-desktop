using System.Globalization;
using Microsoft.Maui.Graphics;

namespace KidsSafeDesktop.Converters;

/// <summary>
/// 把 bool 映射成两种颜色中的一种。
/// ConverterParameter 格式为 "TrueColor,FalseColor"（十六进制，如 "#2E7D32,#C62828"）。
/// </summary>
public class BoolToColorConverter : Microsoft.Maui.Controls.IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isTrue = value is bool b && b;
        var parts = (parameter as string)?.Split(',') ?? Array.Empty<string>();
        var color = parts.Length == 2
            ? (isTrue ? parts[0] : parts[1])
            : (isTrue ? "#000000" : "#FFFFFF");
        try { return Color.FromArgb(color); }
        catch { return Colors.Transparent; }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
