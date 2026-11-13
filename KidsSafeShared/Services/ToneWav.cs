using System.Text;

namespace KidsSafeDesktop.Services;

/// <summary>
/// 用纯 C# 合成 WAV（16-bit PCM，单声道）。给定一串「频率+时长」的音符，
/// 生成可被 Windows SoundPlayer 与 Android MediaPlayer 直接播放的字节。
/// </summary>
public static class ToneWav
{
    public static byte[] Generate(IEnumerable<(double freq, int ms)> notes, int sampleRate = 22050)
    {
        var pcm = new List<byte>(8192);
        foreach (var (freq, ms) in notes)
        {
            int samples = sampleRate * ms / 1000;
            int fade = Math.Max(1, sampleRate / 100); // 淡入淡出，避免咔哒声
            for (int i = 0; i < samples; i++)
            {
                double t = (double)i / sampleRate;
                double env = Math.Min(1.0, (double)i / fade) * Math.Min(1.0, (double)(samples - i) / fade);
                double v = Math.Sin(2 * Math.PI * freq * t) * 0.32 * env;
                short s = (short)(v * 32767);
                pcm.Add((byte)(s & 0xFF));
                pcm.Add((byte)((s >> 8) & 0xFF));
            }
        }

        byte[] wav = new byte[44 + pcm.Count];
        Encoding.ASCII.GetBytes("RIFF").CopyTo(wav, 0);
        BitConverter.GetBytes(36 + pcm.Count).CopyTo(wav, 4);
        Encoding.ASCII.GetBytes("WAVE").CopyTo(wav, 8);
        Encoding.ASCII.GetBytes("fmt ").CopyTo(wav, 12);
        BitConverter.GetBytes(16).CopyTo(wav, 16);
        BitConverter.GetBytes((short)1).CopyTo(wav, 20);
        BitConverter.GetBytes((short)1).CopyTo(wav, 22);
        BitConverter.GetBytes(sampleRate).CopyTo(wav, 24);
        BitConverter.GetBytes(sampleRate * 2).CopyTo(wav, 28);
        BitConverter.GetBytes((short)2).CopyTo(wav, 32);
        BitConverter.GetBytes((short)16).CopyTo(wav, 34);
        Encoding.ASCII.GetBytes("data").CopyTo(wav, 36);
        BitConverter.GetBytes(pcm.Count).CopyTo(wav, 40);
        pcm.CopyTo(wav, 44);
        return wav;
    }
}
