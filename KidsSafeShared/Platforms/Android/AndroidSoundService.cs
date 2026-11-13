#if ANDROID
using Android.App;
using Android.Media;
using Java.IO;
using KidsSafeDesktop.Services;
using Microsoft.Maui;
using Microsoft.Maui.ApplicationModel;
using System.Threading.Tasks;

namespace KidsSafeDesktop.Platforms.Android;

public class AndroidSoundService : ISoundService
{
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

    private static void Play(byte[] wav)
    {
        // 关键修复：在后台线程准备并播放，避免 MediaPlayer.Prepare() 阻塞 UI 线程，
        // 否则每次点击都会造成明显的卡顿（交互“反应迟钝”）。
        _ = Task.Run(() =>
        {
            try
            {
                var ctx = Platform.CurrentActivity ?? global::Android.App.Application.Context;

                // 文件名按内容变化：听觉训练会连续播放不同音高，若都用同一个 kidsfx.wav，
                // 前一个 MediaPlayer 还没播完文件就被覆盖，会出现「只听到最后一个音」。
                var file = new Java.IO.File(ctx.CacheDir, FileNameFor(wav));
                if (!file.Exists()) System.IO.File.WriteAllBytes(file.AbsolutePath, wav);

                var mp = new MediaPlayer();
                mp.SetDataSource(file.AbsolutePath);
                mp.Prepare();
                mp.Start();
                mp.Completion += (_, _) => mp.Release();
            }
            catch { /* 忽略音频不可用的情况 */ }
        });
    }

    /// <summary>按 WAV 内容算一个稳定的短名，同一段音频复用同一文件（不重复写盘）。</summary>
    private static string FileNameFor(byte[] wav)
    {
        unchecked
        {
            int h = 17;
            for (int i = 0; i < wav.Length; i += 13) h = h * 31 + wav[i];
            return $"kidsfx_{Math.Abs(h)}.wav";
        }
    }
}
#endif
