#if IOS
using AVFoundation;
using CoreMedia;
using Foundation;
using KidsSafeDesktop.Services;

namespace KidsSafeDesktop.Platforms.iOS;

/// <summary>
/// iOS 端音频播放：统一用 <see cref="AVPlayer"/>。
///
/// 为什么不用 AVAudioPlayer：它只吃本地文件，而「听一听」里的音频既可能来自
/// 本地缓存，也可能是家长服务器上的 http 地址，两套播放器会让进度条逻辑分叉。
/// AVPlayer 两者都能播，接口也和 Android 的 MediaPlayer 对得上
/// （Play/Pause/Seek/时长/播完通知）。
///
/// 两个已知差异在这里抹平：
///   1) AVPlayer 的时长在 item 就绪前是 NaN —— 对外一律返回 0，避免界面显示 NaN；
///   2) 循环播放没有内建开关 —— 用「播完通知里回 0 再播」实现，和 Android 的 Looping 等价。
/// </summary>
public class IosAudioPlayerService : IAudioPlayerService
{
    private AVPlayer? _player;
    private NSObject? _endObserver;
    private bool _loop;

    public bool IsPlaying
    {
        get
        {
            try { return _player?.Rate > 0; }
            catch { return false; }
        }
    }

    public double DurationSeconds
    {
        get
        {
            try
            {
                var seconds = _player?.CurrentItem?.Duration.Seconds ?? 0;
                // 未就绪时 CMTime 是无效值（NaN / Infinity），当作 0
                return double.IsFinite(seconds) && seconds > 0 ? seconds : 0;
            }
            catch { return 0; }
        }
    }

    public double PositionSeconds
    {
        get
        {
            try
            {
                var seconds = _player?.CurrentTime.Seconds ?? 0;
                return double.IsFinite(seconds) && seconds > 0 ? seconds : 0;
            }
            catch { return 0; }
        }
    }

    public Task SetSourceAsync(string filePath, bool loop)
    {
        Stop();
        Release();
        _loop = loop;

        // 播放类别设成 Playback：静音开关拨到静音时仍然出声（儿歌/报题是核心功能，
        // 不是可有可无的提示音）。加 try：模拟器上音频会话偶尔不可用，不该让播放失败。
        try
        {
            var session = AVAudioSession.SharedInstance();
            session.SetCategory(AVAudioSessionCategory.Playback);
            session.SetActive(true);
        }
        catch { /* 音频会话不可用时仍然尝试播放 */ }

        NSUrl? url = IsHttp(filePath)
            ? NSUrl.FromString(filePath)
            : (File.Exists(filePath) ? NSUrl.FromFilename(filePath) : null);

        if (url is null)
        {
            throw new FileNotFoundException("音频文件不存在", filePath);
        }

        var item = AVPlayerItem.FromUrl(url);
        _player = new AVPlayer(item);

        _endObserver = NSNotificationCenter.DefaultCenter.AddObserver(
            AVPlayerItem.DidPlayToEndTimeNotification,
            OnPlaybackEnded,
            item);

        return Task.CompletedTask;
    }

    private void OnPlaybackEnded(NSNotification notification)
    {
        if (_loop)
        {
            // 循环：回到开头继续，不通知上层（上层只在「真的播完」时切下一首）
            try
            {
                _player?.Seek(CMTime.FromSeconds(0, 600));
                _player?.Play();
            }
            catch { }
            return;
        }

        Ended?.Invoke(this, EventArgs.Empty);
    }

    public void Play()
    {
        try { _player?.Play(); } catch { }
    }

    public void Pause()
    {
        try { _player?.Pause(); } catch { }
    }

    public void Stop()
    {
        try
        {
            _player?.Pause();
            _player?.Seek(CMTime.FromSeconds(0, 600));
        }
        catch { }
    }

    public void SeekTo(TimeSpan position)
    {
        try
        {
            var seconds = Math.Max(0, position.TotalSeconds);
            _player?.Seek(CMTime.FromSeconds(seconds, 600));
        }
        catch { }
    }

    private void Release()
    {
        try
        {
            if (_endObserver is not null)
            {
                NSNotificationCenter.DefaultCenter.RemoveObserver(_endObserver);
                _endObserver.Dispose();
            }
        }
        catch { }
        finally
        {
            _endObserver = null;
        }

        try
        {
            _player?.Pause();
            _player?.Dispose();
        }
        catch { }
        finally
        {
            _player = null;
        }
    }

    private static bool IsHttp(string s)
        => !string.IsNullOrEmpty(s) &&
           (s.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            s.StartsWith("https://", StringComparison.OrdinalIgnoreCase));

    public event EventHandler? Ended;
}
#endif
