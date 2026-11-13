namespace KidsSafeDesktop.Services;

/// <summary>
/// 跨平台音频播放服务（「听一听」使用）。
///
/// 早先使用 CommunityToolkit.Maui.MediaElement 作为播放控件，但该包仅面向 net8.0，
/// 在本项目 net10.0 上运行 Windows 端会在原生播放管线阶段直接终止进程（0xC000027B），
/// 且即使不调用 Play()、只设置 Source 也会崩溃。改为直接调用各平台原生播放器：
/// Windows 用 WinRT `Windows.Media.Playback.MediaPlayer`；Android 用 `Android.Media.MediaPlayer`。
/// 两者都不涉及原生宿主。
/// </summary>
public interface IAudioPlayerService
{
    /// <summary>是否正在播放。</summary>
    bool IsPlaying { get; }

    /// <summary>总时长（秒），未就绪时为 0。</summary>
    double DurationSeconds { get; }

    /// <summary>当前播放位置（秒）。</summary>
    double PositionSeconds { get; }

    /// <summary>装载本地文件。<paramref name="loop"/> 为 true 时循环播放。</summary>
    Task SetSourceAsync(string filePath, bool loop);

    void Play();

    void Pause();

    void Stop();

    /// <summary>跳转到指定位置。</summary>
    void SeekTo(TimeSpan position);

    /// <summary>非循环模式下播放到结尾时触发。</summary>
    event EventHandler? Ended;
}
