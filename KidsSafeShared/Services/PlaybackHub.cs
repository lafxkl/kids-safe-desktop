using System.ComponentModel;

namespace KidsSafeDesktop.Services;

/// <summary>
/// 轻量全局播放状态中心：记录当前正在播放的内容，供「媒体中心」右下角的
/// 迷你悬浮播放器展示与快速回到播放页。不持有播放器实例，只做状态广播。
/// </summary>
public static class PlaybackHub
{
    public static event EventHandler? StateChanged;

    public static bool HasItem => !string.IsNullOrEmpty(CurrentTitle);

    public static string? CurrentEmoji { get; private set; }
    public static string? CurrentTitle { get; private set; }
    public static bool IsPlaying { get; private set; }

    private static Action? _reopen;

    /// <summary>设置当前播放项（进入播放页时调用）。</summary>
    public static void Set(string emoji, string title, bool playing, Action reopen)
    {
        CurrentEmoji = emoji;
        CurrentTitle = title;
        IsPlaying = playing;
        _reopen = reopen;
        StateChanged?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>仅更新播放/暂停状态。</summary>
    public static void UpdatePlaying(bool playing)
    {
        if (IsPlaying == playing) return;
        IsPlaying = playing;
        StateChanged?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>清空（离开播放页且停止时调用）。</summary>
    public static void Clear()
    {
        CurrentEmoji = null;
        CurrentTitle = null;
        IsPlaying = false;
        _reopen = null;
        StateChanged?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>回到当前播放页。</summary>
    public static void Reopen() => _reopen?.Invoke();
}

/// <summary>给迷你播放器用的极简可绑定包装（避免在 XAML 里直接绑静态属性）。</summary>
public class PlaybackState : INotifyPropertyChanged
{
    private bool _visible;
    private string _emoji = "🎵";
    private string _title = string.Empty;
    private bool _playing;

    public bool Visible { get => _visible; set { _visible = value; OnChanged(); } }
    public string Emoji { get => _emoji; set { _emoji = value; OnChanged(); } }
    public string Title { get => _title; set { _title = value; OnChanged(); } }
    public bool Playing { get => _playing; set { _playing = value; OnChanged(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
}
