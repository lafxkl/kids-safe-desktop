#if ANDROID
using KidsSafeDesktop.Services;

namespace KidsSafeDesktop.Platforms.Android;

/// <summary>
/// Android 端音频播放：基于 <c>Android.Media.MediaPlayer</c>。
/// 注意：MediaPlayer 在 Looping=true 时不会回调 Completion，因此 PlaybackEnded 由 hints 控制。
/// </summary>
public class AndroidAudioPlayerService : IAudioPlayerService
{
    private global::Android.Media.MediaPlayer? _player;
    private bool _loop;

    public bool IsPlaying
    {
        get
        {
            try { return _player?.IsPlaying ?? false; }
            catch { return false; }
        }
    }

    public double DurationSeconds
    {
        get
        {
            try { return _player is null ? 0 : _player.Duration / 1000.0; }
            catch { return 0; }
        }
    }

    public double PositionSeconds
    {
        get
        {
            try { return _player is null ? 0 : _player.CurrentPosition / 1000.0; }
            catch { return 0; }
        }
    }

    public async Task SetSourceAsync(string filePath, bool loop)
    {
        Stop();
        Release();

        var mp = new global::Android.Media.MediaPlayer
        {
            Looping = loop
        };
        _loop = loop;

        mp.Completion += (_, _) =>
        {
            // 循环模式下 Android 不回调 Completion；收到即视为真的播完
            if (!_loop)
                Ended?.Invoke(this, EventArgs.Empty);
        };
        mp.Error += (_, _) =>
        {
            // 出错时也通知上层，避免播放条卡在播放态
            if (!_loop)
                Ended?.Invoke(this, EventArgs.Empty);
        };

        if (IsHttp(filePath))
        {
            // 在线音频：直接用 URI 流式播放（家长服务器，受控）
            try
            {
                mp.SetDataSource(filePath);
                await PrepareAsync(mp).ConfigureAwait(false);
            }
            catch
            {
                preparedCleanup(mp);
                throw;
            }
            _player = mp;
            return;
        }

        if (!File.Exists(filePath))
            throw new FileNotFoundException("音频文件不存在", filePath);

        try
        {
            mp.SetDataSource(filePath);

            // 该绑定里 PrepareAsync() 返回 void（异步结果由 Prepared 事件回调），
            // 这里用 TaskCompletionSource 桥接成可 await 的形式，避免阻塞 UI 线程。
            await PrepareAsync(mp).ConfigureAwait(false);
        }
        catch
        {
            preparedCleanup(mp);
            throw;
        }

        _player = mp;
    }

    private static Task PrepareAsync(global::Android.Media.MediaPlayer mp)
    {
        var prepared = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnPrepared(object? sender, EventArgs e) => prepared.TrySetResult(true);
        mp.Prepared += OnPrepared;
        try
        {
            mp.PrepareAsync();
            return prepared.Task;
        }
        finally
        {
            mp.Prepared -= OnPrepared;
        }
    }

    private static bool IsHttp(string s)
        => !string.IsNullOrEmpty(s) &&
           (s.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            s.StartsWith("https://", StringComparison.OrdinalIgnoreCase));

    private static void preparedCleanup(global::Android.Media.MediaPlayer mp)
    {
        try { mp.Release(); } catch { }
    }

    public void Play()
    {
        try { _player?.Start(); } catch { }
    }

    public void Pause()
    {
        try { if (_player?.IsPlaying == true) _player.Pause(); } catch { }
    }

    public void Stop()
    {
        try { if (_player?.IsPlaying == true) _player.Stop(); } catch { }
    }

    public void SeekTo(TimeSpan position)
    {
        try { _player?.SeekTo((int)position.TotalMilliseconds); } catch { }
    }

    private void Release()
    {
        try
        {
            _player?.Release();
        }
        catch { }
        finally
        {
            _player = null;
        }
    }

    public event EventHandler? Ended;
}
#endif
