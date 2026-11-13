namespace KidsSafeDesktop.Services;

/// <summary>未实现平台的空实现（静音）。</summary>
public class StubSoundService : ISoundService
{
    public void PlayClick() { }
    public void PlayCorrect() { }
    public void PlayWrong() { }
    public void PlayWin() { }
    public void PlayNotes(IReadOnlyList<(double Freq, int Ms)> notes) { }
}
