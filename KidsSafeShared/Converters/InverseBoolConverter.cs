using System.Globalization;
using Microsoft.Maui.Controls;

namespace KidsSafeDesktop.Converters;

/// <summary>
/// 将 bool 取反，用于绑定 IsVisible 等场景（例如“有图标时隐藏 emoji”）。
/// </summary>
public class InverseBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? !b : true;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b ? !b : false;
}
