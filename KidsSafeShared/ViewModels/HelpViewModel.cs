using System.Collections.ObjectModel;
using System.Windows.Input;
using KidsSafeDesktop.Helpers;
using KidsSafeDesktop.Models;

namespace KidsSafeDesktop.ViewModels;

/// <summary>
/// 操作帮助中心视图模型。
/// 提供：分分类浏览帮助条目 + 系统语音（TTS）自检与配置引导。
/// </summary>
public class HelpViewModel : BaseViewModel
{
    private string _selectedCategory = "操作指南";
    private bool _ttsReady;
    private bool _ttsChecking;
    private string _ttsStatusEmoji = "⏳";
    private string _ttsStatusText = "正在检测语音…";
    private string _ttsStatusDetail = string.Empty;
    private Color _ttsStatusColor = Colors.Orange;

    public ObservableCollection<string> Categories { get; } = new(HelpData.Categories);

    public ObservableCollection<HelpTopic> Topics { get; } = new();

    public string SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (!SetProperty(ref _selectedCategory, value)) return;
            ApplyCategory(value);
        }
    }

    public bool TtsReady
    {
        get => _ttsReady;
        set
        {
            if (SetProperty(ref _ttsReady, value))
                OnPropertyChanged(nameof(TtsHintVisible));
        }
    }

    public bool TtsChecking
    {
        get => _ttsChecking;
        set => SetProperty(ref _ttsChecking, value);
    }

    /// <summary>语音不可用时，显示「去配置」提示条。</summary>
    public bool TtsHintVisible => !_ttsReady;

    public string TtsStatusEmoji
    {
        get => _ttsStatusEmoji;
        set => SetProperty(ref _ttsStatusEmoji, value);
    }

    public string TtsStatusText
    {
        get => _ttsStatusText;
        set => SetProperty(ref _ttsStatusText, value);
    }

    public string TtsStatusDetail
    {
        get => _ttsStatusDetail;
        set => SetProperty(ref _ttsStatusDetail, value);
    }

    public Color TtsStatusColor
    {
        get => _ttsStatusColor;
        set => SetProperty(ref _ttsStatusColor, value);
    }

    public ICommand CheckTtsCommand { get; }

    public ICommand TestChineseCommand { get; }

    public ICommand TestEnglishCommand { get; }

    public HelpViewModel()
    {
        CheckTtsCommand = new Command(async () => await RefreshTtsAsync());
        TestChineseCommand = new Command(async () => await TestSpeakAsync("zh"));
        TestEnglishCommand = new Command(async () => await TestSpeakAsync("en"));
        ApplyCategory(_selectedCategory);
        _ = RefreshTtsAsync();
    }

    public void SelectCategory(string category) => SelectedCategory = category;

    /// <summary>点击条目展开 / 收起分步说明。</summary>
    public void ToggleTopic(HelpTopic topic) => topic.Toggle();

    private void ApplyCategory(string category)
    {
        Topics.Clear();
        foreach (var t in HelpData.GetByCategory(category))
        {
            t.IsExpanded = t.DefaultExpanded;
            Topics.Add(t);
        }
    }

    /// <summary>重新检测系统语音可用性并更新状态卡片。</summary>
    public async Task RefreshTtsAsync()
    {
        if (TtsChecking) return;
        TtsChecking = true;
        TtsStatusEmoji = "⏳";
        TtsStatusText = "正在检测语音…";
        TtsStatusDetail = "正在读取系统已安装的语音包列表…";
        TtsStatusColor = Colors.Orange;

        var (available, detail) = await Tts.ProbeAsync();

        TtsReady = available;
        TtsStatusEmoji = available ? "✅" : "⚠️";
        TtsStatusText = available ? "语音正常" : "未检测到语音包";
        TtsStatusDetail = detail;
        TtsStatusColor = available ? Colors.Green : Colors.Red;

        TtsChecking = false;
    }

    /// <summary>试听：发送一句朗读，并把结果反馈到状态卡片。</summary>
    public async Task TestSpeakAsync(string locale)
    {
        Tts.ClearError();

        if (locale == "en")
            await Tts.SpeakAsync("Hello! This is an English voice test.", "en");
        else
            await Tts.SpeakAsync("你好，我是儿童安全桌面，语音测试成功啦！", "zh");

        var err = Tts.LastError;
        if (string.IsNullOrEmpty(err))
        {
            TtsStatusDetail = "已发出朗读请求 ✅ 如果能听到声音，说明语音配置正常；听不到请按下方步骤安装语音包。";
        }
        else
        {
            TtsStatusDetail = "朗读失败：" + err + "\n请按下方对应系统的步骤安装语音包后重试。";
            TtsStatusColor = Colors.Red;
        }
    }
}
