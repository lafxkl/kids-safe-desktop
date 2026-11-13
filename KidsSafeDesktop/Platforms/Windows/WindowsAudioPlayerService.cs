using KidsSafeDesktop.Services;

namespace KidsSafeDesktop.Platforms.Windows;

/// <summary>
/// Windows 端音频播放：基于 WinRT <c>Windows.Media.Playback.MediaPlayer</c>。
/// 直接用 MediaPlayer 而不是 XAML 宿主控件，避免宿主控件带来的渲染/生命周期问题。
/// </summary>
public class WindowsAudioPlayerService : IAudioPlayerService
{
    private readonly global::Windows.Media.Playback.MediaPlayer _player = new() { AutoPlay = false };

    private double _duration;

    public WindowsAudioPlayerService()
    {
        _player.MediaEnded += (_, _) => Ended?.Invoke(this, EventArgs.Empty);
    }

    public bool IsPlaying =>
        _player.PlaybackSession?.PlaybackState == global::Windows.Media.Playback.MediaPlaybackState.Playing;

    public double DurationSeconds => _duration;

    public double PositionSeconds =>
        _player.PlaybackSession?.Position.TotalSeconds ?? 0;

    public async Task SetSourceAsync(string filePath, bool loop)
    {
        Stop();

        global::Windows.Media.Core.MediaSource source;
        if (IsHttp(filePath))
        {
            // 在线音频：直接用 URI 流式播放（家长服务器，受控）
            source = global::Windows.Media.Core.MediaSource.CreateFromUri(new Uri(filePath));
        }
        else
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("音频文件不存在", filePath);

            var file = await global::Windows.Storage.StorageFile.GetFileFromPathAsync(filePath)
                           .AsTask().ConfigureAwait(false);
            if (file is null)
                throw new InvalidOperationException("无法打开音频文件：" + filePath);

            source = global::Windows.Media.Core.MediaSource.CreateFromStorageFile(file);
        }

        _duration = 0;
        _player.IsLoopingEnabled = loop;
        _player.Source = source;

        // 等待元数据就绪以便取得总时长（通常几十毫秒内即有值）
        for (var i = 0; i < 20; i++)
        {
            _duration = _player.PlaybackSession?.NaturalDuration.TotalSeconds ?? 0;
            if (_duration > 0) break;
            await Task.Delay(50).ConfigureAwait(false);
        }
    }

    private static bool IsHttp(string s)
        => !string.IsNullOrEmpty(s) &&
           (s.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            s.StartsWith("https://", StringComparison.OrdinalIgnoreCase));

    public void Play() => _player.Play();

    public void Pause() => _player.Pause();

    public void Stop()
    {
        try
        {
            if (_player.PlaybackSession?.PlaybackState != global::Windows.Media.Playback.MediaPlaybackState.None)
                _player.Pause();
        }
        catch { /* 忽略：未装载时的空调用 */ }
    }

    public void SeekTo(TimeSpan position)
    {
        try { _player.PlaybackSession?.Position = position; }
        catch { /* 忽略：未装载时的空调用 */ }
    }

    public event EventHandler? Ended;
}
