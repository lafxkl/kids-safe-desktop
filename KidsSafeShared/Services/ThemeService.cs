using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using AppTheme = KidsSafeDesktop.Models.AppTheme;
using KidsSafeDesktop.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace KidsSafeDesktop.Services;

/// <summary>
/// 主题服务（单例）。提供 3 套童趣主题，并在切换时持久化到设置。
/// 页面通过绑定 <see cref="Instance"/>.CurrentTheme 来应用背景与配色。
/// </summary>
public class ThemeService : INotifyPropertyChanged
{
    public static readonly ThemeService Instance = new();

    public IReadOnlyList<AppTheme> Themes { get; }

    private AppTheme _current;
    public AppTheme CurrentTheme
    {
        get => _current;
        private set
        {
            if (_current == value) return;
            _current = value;
            OnPropertyChanged();
        }
    }

    private ThemeService()
    {
        Themes = new List<AppTheme>
        {
            new()
            {
                Id = "sunshine", Name = "暖阳", Emoji = "☀️", BackgroundFile = "theme_sunshine.png",
                HeaderGradient = MakeGradient("#FFB74D", "#FF8A65"),
                TileGradient = MakeGradient("#FFD54F", "#FFB300"),
                Accent = Color.FromArgb("#FB8C00"), Ink = Color.FromArgb("#5D4037")
            },
            new()
            {
                Id = "sky", Name = "蓝天", Emoji = "🌤️", BackgroundFile = "theme_sky.png",
                HeaderGradient = MakeGradient("#4FC3F7", "#29B6F6"),
                TileGradient = MakeGradient("#4DD0E1", "#26C6DA"),
                Accent = Color.FromArgb("#0288D1"), Ink = Color.FromArgb("#01579B")
            },
            new()
            {
                Id = "rainbow", Name = "彩虹", Emoji = "🌈", BackgroundFile = "theme_rainbow.png",
                HeaderGradient = MakeGradient("#FF8AB0", "#9575CD"),
                TileGradient = MakeGradient("#FFAB91", "#F06292"),
                Accent = Color.FromArgb("#8E24AA"), Ink = Color.FromArgb("#4A148C")
            },
        };
        _current = Themes[0];
    }

    private static LinearGradientBrush MakeGradient(string a, string b)
    {
        return new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1),
            GradientStops = new GradientStopCollection
            {
                new GradientStop(Color.FromArgb(a), 0.0f),
                new GradientStop(Color.FromArgb(b), 1.0f),
            }
        };
    }

    /// <summary>从设置读取已保存的主题（需在设置加载完成后调用）。</summary>
    public void Init()
    {
        var settings = ServiceProvider.Get<ISettingsService>();
        var t = Themes.FirstOrDefault(x => x.Id == settings.Current.ThemeId) ?? Themes[0];
        CurrentTheme = t;
    }

    /// <summary>切换主题并保存。</summary>
    public void SetTheme(string id)
    {
        var t = Themes.FirstOrDefault(x => x.Id == id);
        if (t is null) return;
        CurrentTheme = t;
        var settings = ServiceProvider.Get<ISettingsService>();
        settings.Current.ThemeId = id;
        _ = settings.SaveAsync();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
