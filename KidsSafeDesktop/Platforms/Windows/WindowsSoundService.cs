using KidsSafeDesktop.Services;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace KidsSafeDesktop.Platforms.Windows;

public class WindowsSoundService : ISoundService
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

    /// <summary>持有正在播放的播放器，避免被 GC 提前回收导致「没声音」。</summary>
    private static readonly List<global::Windows.Media.Playback.MediaPlayer> Alive = new();

    private static void Play(byte[] wav)
    {
        // 在后台线程播放，绝不阻塞 UI 线程，保证点击反馈即时。
        _ = Task.Run(async () =>
        {
            try
            {
                var folder = global::Windows.Storage.ApplicationData.Current.TemporaryFolder;

                // 按内容命名：听觉训练要连续播不同音高，共用一个文件名会互相覆盖、
                // 正在播放的文件被 ReplaceExisting 截断，听起来就是「只响一声」。
                var file = await folder.CreateFileAsync(FileNameFor(wav),
                    global::Windows.Storage.CreationCollisionOption.OpenIfExists);
                var props = await file.GetBasicPropertiesAsync();
                if (props.Size == 0)
                    await global::Windows.Storage.FileIO.WriteBytesAsync(file, wav);

                var player = new global::Windows.Media.Playback.MediaPlayer();
                player.Source = global::Windows.Media.Core.MediaSource.CreateFromStorageFile(file);
                lock (Alive) Alive.Add(player);
                player.MediaEnded += (_, _) =>
                {
                    lock (Alive) Alive.Remove(player);
                    try { player.Dispose(); } catch { }
                };
                player.Play();
            }
            catch { /* 忽略音频不可用的情况 */ }
        });
    }

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
