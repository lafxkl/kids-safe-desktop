using System.Globalization;

namespace KidsSafeDesktop.Converters;

/// <summary>
/// 判断绑定值是否非空（用于 IsVisible 控制）。
/// 空引用 / 空字符串返回 false，否则返回 true。
/// </summary>
public class HasItemConverter : Microsoft.Maui.Controls.IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string s) return !string.IsNullOrEmpty(s);
        return value is not null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
