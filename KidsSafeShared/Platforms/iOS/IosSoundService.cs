#if IOS
using AVFoundation;
using Foundation;
using KidsSafeDesktop.Services;

namespace KidsSafeDesktop.Platforms.iOS;

/// <summary>
/// iOS 端轻音效：复用跨平台的 <see cref="ToneWav"/> 合成 WAV，再交给 AVAudioPlayer 播放。
///
/// 与安卓实现的两个关键差异：
///   1) 不走文件：AVAudioPlayer.FromData 直接吃内存里的 WAV 字节，
///      省掉了「写缓存文件、文件名按内容哈希」这套安卓特有的处理；
///   2) 必须**强引用**正在播的播放器 —— 一旦被 GC 回收，声音会立刻断（iOS 上没有
///      MediaPlayer 那种原生对象自己活着的行为）。这里用一个列表持有，播完再移除。
/// </summary>
public class IosSoundService : ISoundService
{
    // 播放器与它的 NSData 必须一起持有：AVAudioPlayer 只持有一份对 data 的引用，
    // 我们的托管包装一旦被 GC / Dispose，原生 data 的引用计数可能归零，声音就会断。
    private readonly Dictionary<AVAudioPlayer, NSData> _alive = new();
    private readonly object _gate = new();

    public void PlayClick() => Play(ToneWav.Generate(new[] { (880.0, 50) }));

    public void PlayCorrect() => Play(ToneWav.Generate(new[] { (523.25, 120), (659.25, 120), (783.99, 180) }));

    public void PlayWrong() => Play(ToneWav.Generate(new[] { (196.0, 320) }));

    public void PlayWin() => Play(ToneWav.Generate(new[] { (523.25, 140), (659.25, 140), (783.99, 140), (1046.5, 280) }));

    /// <summary>视障听觉训练用：任意音高序列（音高辨别 / 听觉定位提示音）。</summary>
    public void PlayNotes(IReadOnlyList<(double Freq, int Ms)> notes)
    {
        if (notes is null || notes.Count == 0) return;

        var arr = new (double, int)[notes.Count];
        for (int i = 0; i < notes.Count; i++) arr[i] = (notes[i].Freq, notes[i].Ms);
        Play(ToneWav.Generate(arr));
    }

    private void Play(byte[] wav)
    {
        try
        {
            var data = NSData.FromArray(wav);
            var player = AVAudioPlayer.FromData(data, out var error);
            if (player is null || error is not null)
            {
                data.Dispose();
                return;
            }

            lock (_gate) { _alive[player] = data; }

            player.FinishedPlaying += (_, _) => Release(player);
            // 音量压到 0.35：交互音效只是反馈，不该盖过儿歌与朗读
            player.Volume = 0.35f;
            player.Play();
        }
        catch
        {
            // 音频不可用（模拟器无音频设备、静音开关等）时静默降级
        }
    }

    private void Release(AVAudioPlayer player)
    {
        try
        {
            lock (_gate)
            {
                if (_alive.Remove(player, out var data)) data.Dispose();
            }
            player.Dispose();
        }
        catch { }
    }
}
#endif
