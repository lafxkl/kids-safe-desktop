namespace KidsSafeDesktop.Services;

/// <summary>
/// 轻量音效服务：用合成音调播放反馈声（无需任何音频素材文件，完全离线）。
/// 语音播报（听音选物）直接使用 MAUI 内置的 TextToSpeech，不在此接口内。
/// </summary>
public interface ISoundService
{
    void PlayClick();
    void PlayCorrect();
    void PlayWrong();
    void PlayWin();

    /// <summary>
    /// 播放任意「频率 + 时长」序列（纯合成，离线）。
    /// 供视障听觉训练使用：不同音高代表不同方位 / 不同音符，
    /// 因此必须能自由指定频率，而不是固定的四个交互音效。
    /// </summary>
    void PlayNotes(IReadOnlyList<(double Freq, int Ms)> notes);
}
