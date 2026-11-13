using KidsSafeDesktop.Helpers;
using KidsSafeDesktop.Models;

namespace KidsSafeDesktop.ViewModels;

/// <summary>
/// 眼保健操引导：按步骤播放动作说明与倒计时，每节自动朗读指导语。
/// 由页面用 DispatcherTimer 每帧调用 Tick(dt) 推进。
/// </summary>
public class EyeExerciseViewModel : BaseViewModel
{
    private readonly List<EyeExerciseStep> _steps = EyeExerciseData.Steps;
    private int _index;
    private float _timeLeft;
    private bool _playing;
    private bool _finished;

    public int TotalSteps => _steps.Count;

    public string StepName => _finished ? "完成啦" : Current.Name;
    public string StepEmoji => _finished ? "🌟" : Current.Emoji;
    public string StepInstruction => _finished ? "眼睛操做完啦，记得平时多看看远处哦！" : Current.Instruction;

    public int StepIndex => _index + 1; // 1-based，便于显示「第 X 节」

    public string StepLabel => $"第 {StepIndex} 节 / 共 {TotalSteps} 节";

    public string TimeText => $"⏱ {System.Math.Max(0, (int)System.Math.Ceiling(_timeLeft))} 秒";

    /// <summary>当前节的进度（0~1，用于进度条）。</summary>
    public double Progress
    {
        get
        {
            var total = Current.Seconds;
            return total > 0 ? System.Math.Max(0, System.Math.Min(1, 1 - _timeLeft / total)) : 0;
        }
    }

    public bool IsPlaying
    {
        get => _playing;
        private set => SetProperty(ref _playing, value);
    }

    public bool IsFinished
    {
        get => _finished;
        private set => SetProperty(ref _finished, value);
    }

    public bool NotFinished => !_finished;

    private EyeExerciseStep Current => _index < _steps.Count ? _steps[_index] : _steps[^1];

    public void Start()
    {
        _index = 0;
        IsFinished = false;
        LoadStep(true);
        IsPlaying = true;
    }

    private void LoadStep(bool speak)
    {
        if (_index >= _steps.Count)
        {
            Finish();
            return;
        }
        _timeLeft = Current.Seconds;
        OnPropertyChanged(nameof(StepName));
        OnPropertyChanged(nameof(StepEmoji));
        OnPropertyChanged(nameof(StepInstruction));
        OnPropertyChanged(nameof(StepIndex));
        OnPropertyChanged(nameof(TimeText));
        OnPropertyChanged(nameof(Progress));
        if (speak)
            _ = Tts.SpeakAsync($"第{StepIndex}节，{Current.Name}。{Current.Instruction}", "zh");
    }

    /// <summary>由页面定时器每帧调用（dt 为秒）。</summary>
    public void Tick(float dt)
    {
        if (!_playing || _finished) return;
        _timeLeft -= dt;
        if (_timeLeft <= 0)
        {
            _index++;
            LoadStep(true);
            return;
        }
        OnPropertyChanged(nameof(TimeText));
        OnPropertyChanged(nameof(Progress));
    }

    public void TogglePlay()
    {
        if (_finished) { Start(); return; }
        IsPlaying = !_playing;
        if (_playing)
            _ = Tts.SpeakAsync(Current.Instruction, "zh");
    }

    public void Prev()
    {
        if (_finished) { Start(); return; }
        if (_index > 0) _index--;
        LoadStep(true);
        IsPlaying = true;
    }

    public void Next()
    {
        if (_finished) { Start(); return; }
        _index++;
        LoadStep(true);
        IsPlaying = true;
    }

    public void Restart() => Start();

    private void Finish()
    {
        IsFinished = true;
        IsPlaying = false;
        OnPropertyChanged(nameof(StepName));
        OnPropertyChanged(nameof(StepEmoji));
        OnPropertyChanged(nameof(StepInstruction));
        _ = Tts.SpeakAsync("眼睛操做完啦，记得平时多看看远处哦", "zh");
    }
}
