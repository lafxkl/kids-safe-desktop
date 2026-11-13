using System.Globalization;
using Microsoft.Maui.Controls;

namespace KidsSafeDesktop.Converters;

/// <summary>把预警级别（"预警"/"注意"）映射为浅色背景。</summary>
public class AlertBgConverter : IValueConverter
{
    public object? Convert(object? value, Type? targetType, object? parameter, CultureInfo? culture)
    {
        var level = value as string;
        return level == "预警"
            ? new Microsoft.Maui.Graphics.Color(255, 205, 210)   // 浅红
            : new Microsoft.Maui.Graphics.Color(255, 248, 225);  // 浅黄
    }

    public object? ConvertBack(object? value, Type? targetType, object? parameter, CultureInfo? culture)
        => throw new NotSupportedException();
}

/// <summary>整数大于 0 时返回 true（用于是否显示降水概率）。</summary>
public class GreaterThanZeroConverter : IValueConverter
{
    public object? Convert(object? value, Type? targetType, object? parameter, CultureInfo? culture)
        => value is int i && i > 0;

    public object? ConvertBack(object? value, Type? targetType, object? parameter, CultureInfo? culture)
        => throw new NotSupportedException();
}
