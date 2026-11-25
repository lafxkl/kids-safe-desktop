using System.Collections.ObjectModel;
using KidsSafeDesktop.Models;
using KidsSafeDesktop.Services;

namespace KidsSafeDesktop.ViewModels;

/// <summary>
/// 一个可点选的「小方块」（星级、分数、标签、问卷选项都用它）。
/// 选中状态用 <see cref="IsOn"/>，XAML 里靠 DataTrigger 改颜色，不需要写转换器。
/// </summary>
public sealed class PickOption : BaseViewModel
{
    private bool _isOn;

    public PickOption(string text, string value, Action<PickOption>? onPick = null)
    {
        Text = text;
        Value = value;
        OnPick = onPick;
    }

    public string Text { get; }
    public string Value { get; }

    public bool IsOn
    {
        get => _isOn;
        set => SetProperty(ref _isOn, value);
    }

    internal Action<PickOption>? OnPick { get; }

    public void Pick() => OnPick?.Invoke(this);
}

/// <summary>满意度里的一个分项（好不好上手 / 内容 / 稳定 / 界面 / 值不值）。</summary>
public sealed class DimRow : BaseViewModel
{
    public DimRow(string key, string label)
    {
        Key = key;
        Label = label;
        for (var i = 1; i <= 5; i++)
        {
            var n = i;
            Options.Add(new PickOption(n + " 分", n.ToString(), o =>
            {
                Score = n;
                foreach (var x in Options) x.IsOn = ReferenceEquals(x, o);
            }));
        }
    }

    public string Key { get; }
    public string Label { get; }

    public ObservableCollection<PickOption> Options { get; } = new();

    /// <summary>0 表示没评（提交时不带这一项，不当作 0 分算）。</summary>
    public int Score { get; private set; }
}

/// <summary>问卷里的一道单选题。</summary>
public sealed class QuestionRow : BaseViewModel
{
    public QuestionRow(string id, string title, params string[] options)
    {
        Id = id;
        Title = title;
        foreach (var opt in options)
        {
            Options.Add(new PickOption(opt, opt, picked =>
            {
                Answer = picked.Value;
                foreach (var x in Options) x.IsOn = ReferenceEquals(x, picked);
                Changed?.Invoke();
            }));
        }
    }

    /// <summary>答完一题时回调，用来刷新「已答 x / 6 题」与提交按钮。</summary>
    public Action? Changed { get; set; }

    public string Id { get; }
    public string Title { get; }
    public ObservableCollection<PickOption> Options { get; } = new();

    public string Answer { get; private set; } = string.Empty;
    public bool Answered => !string.IsNullOrEmpty(Answer);
}

/// <summary>三个反馈页面共用的提交逻辑：联系方式、是否公开、状态文字、待补传条数。</summary>
public abstract class FeedbackPageViewModelBase : BaseViewModel
{
    protected readonly IFeedbackService Service;

    private string _contact = string.Empty;
    private bool _allowPublic;
    private bool _isSending;
    private string _status = string.Empty;

    protected FeedbackPageViewModelBase(IFeedbackService service)
    {
        Service = service;
    }

    public string Contact
    {
        get => _contact;
        set => SetProperty(ref _contact, value);
    }

    /// <summary>默认关闭：不勾选就不公开，这条在隐私政策里写死了。</summary>
    public bool AllowPublic
    {
        get => _allowPublic;
        set => SetProperty(ref _allowPublic, value);
    }

    public bool IsSending
    {
        get => _isSending;
        protected set
        {
            if (SetProperty(ref _isSending, value)) OnPropertyChanged(nameof(CanSubmit));
        }
    }

    public string Status
    {
        get => _status;
        protected set
        {
            if (SetProperty(ref _status, value)) OnPropertyChanged(nameof(HasStatus));
        }
    }

    public bool HasStatus => !string.IsNullOrWhiteSpace(Status);

    /// <summary>本机还攒着几条没发出去的（断网时）。</summary>
    public string PendingText => Service.PendingCount == 0
        ? string.Empty
        : $"本机还有 {Service.PendingCount} 条没发出去，下次联网会自动补传";

    public bool HasPending => Service.PendingCount > 0;

    public abstract bool CanSubmit { get; }

    /// <summary>进入页面时调用：顺手把上次没发出去的补传掉，并刷新提示。</summary>
    public async Task OnAppearingAsync()
    {
        try
        {
            var sent = await Service.FlushAsync();
            if (sent > 0) Status = Service.Status;
        }
        catch { /* 补传失败不影响填表 */ }
        RefreshPending();
    }

    protected void RefreshPending()
    {
        OnPropertyChanged(nameof(PendingText));
        OnPropertyChanged(nameof(HasPending));
    }

    protected FeedbackDraft NewDraft(string kind) => new()
    {
        Kind = kind,
        Contact = string.IsNullOrWhiteSpace(Contact) ? null : Contact.Trim(),
        AllowPublic = AllowPublic
    };

    protected async Task SubmitAsync(FeedbackDraft draft)
    {
        if (!CanSubmit || IsSending) return;

        IsSending = true;
        Status = "提交中…";
        try
        {
            var result = await Service.SubmitAsync(draft);
            Status = result.Message;
            if (result.Ok && !result.Queued) OnSubmitted();
        }
        catch (Exception ex)
        {
            Helpers.NavFx.Log(ex);
            Status = "提交失败：" + ex.Message;
        }
        finally
        {
            IsSending = false;
            RefreshPending();
        }
    }

    /// <summary>提交成功后清掉已发送的正文（联系方式保留，连着提两条不用重填）。</summary>
    protected virtual void OnSubmitted() { }
}

/// <summary>意见反馈。</summary>
public sealed class FeedbackViewModel : FeedbackPageViewModelBase
{
    private string _content = string.Empty;
    private string _selectedCategory = "问题反馈";

    public FeedbackViewModel(IFeedbackService service) : base(service)
    {
        foreach (var name in Categories)
        {
            var option = new PickOption(name, name, picked =>
            {
                _selectedCategory = picked.Value;
                foreach (var x in CategoryOptions) x.IsOn = ReferenceEquals(x, picked);
            });
            option.IsOn = name == _selectedCategory;
            CategoryOptions.Add(option);
        }
    }

    public ObservableCollection<string> Categories { get; } = new()
    {
        "问题反馈", "功能建议", "内容纠错", "使用疑问", "其他"
    };

    public ObservableCollection<PickOption> CategoryOptions { get; } = new();

    public string Content
    {
        get => _content;
        set
        {
            if (SetProperty(ref _content, value)) OnPropertyChanged(nameof(CanSubmit));
        }
    }

    public override bool CanSubmit => !IsSending && Content.Trim().Length >= 5;

    public Task SendAsync()
    {
        var draft = NewDraft("feedback");
        draft.Content = Content.Trim();
        draft.Tags = new List<string> { _selectedCategory };
        return SubmitAsync(draft);
    }

    protected override void OnSubmitted()
    {
        Content = string.Empty;
    }
}

/// <summary>满意度调查：整体星级 + 分项 + 推荐意愿 + 喜欢的模块 + 建议。</summary>
public sealed class SatisfactionViewModel : FeedbackPageViewModelBase
{
    private int _rating;
    private int _nps = -1;
    private string _content = string.Empty;

    public SatisfactionViewModel(IFeedbackService service) : base(service)
    {
        for (var i = 1; i <= 5; i++)
        {
            var n = i;
            Stars.Add(new PickOption("★", n.ToString(), picked =>
            {
                _rating = n;
                foreach (var x in Stars) x.IsOn = ReferenceEquals(x, picked);
                OnPropertyChanged(nameof(CanSubmit));
                OnPropertyChanged(nameof(RatingText));
            }));
        }

        for (var i = 0; i <= 10; i++)
        {
            var n = i;
            NpsOptions.Add(new PickOption(n.ToString(), n.ToString(), picked =>
            {
                _nps = n;
                foreach (var x in NpsOptions) x.IsOn = ReferenceEquals(x, picked);
                OnPropertyChanged(nameof(NpsText));
            }));
        }

        Dims.Add(new DimRow("ease", "好不好上手"));
        Dims.Add(new DimRow("content", "内容合不合适"));
        Dims.Add(new DimRow("stable", "稳不稳定"));
        Dims.Add(new DimRow("design", "界面好不好看"));
        Dims.Add(new DimRow("value", "值不值得推荐"));

        foreach (var name in LikeTags)
        {
            LikeOptions.Add(new PickOption(name, name, picked => picked.IsOn = !picked.IsOn));
        }
    }

    public ObservableCollection<PickOption> Stars { get; } = new();
    public ObservableCollection<PickOption> NpsOptions { get; } = new();
    public ObservableCollection<DimRow> Dims { get; } = new();

    public ObservableCollection<string> LikeTags { get; } = new()
    {
        "护眼提醒", "小游戏", "故事屋", "学习内容", "家长管控", "日记本", "关爱中心", "开机自启"
    };

    public ObservableCollection<PickOption> LikeOptions { get; } = new();

    public string RatingText => _rating == 0 ? "还没打分" : new string('★', _rating) + new string('☆', 5 - _rating);

    public string NpsText => _nps < 0 ? "还没选" : _nps + " 分";

    public string Content
    {
        get => _content;
        set => SetProperty(ref _content, value);
    }

    public override bool CanSubmit => !IsSending && _rating > 0;

    public Task SendAsync()
    {
        var draft = NewDraft("satisfaction");
        draft.Rating = _rating;
        if (_nps >= 0) draft.Nps = _nps;

        var dims = Dims.Where(d => d.Score > 0).ToDictionary(d => d.Key, d => d.Score);
        if (dims.Count > 0) draft.Dims = dims;

        var tags = LikeOptions.Where(t => t.IsOn).Select(t => t.Value).ToList();
        if (tags.Count > 0) draft.Tags = tags;

        if (!string.IsNullOrWhiteSpace(Content)) draft.Content = Content.Trim();

        return SubmitAsync(draft);
    }

    protected override void OnSubmitted()
    {
        Content = string.Empty;
    }
}

/// <summary>使用问卷：6 道单选题，至少答 3 题。</summary>
public sealed class SurveyViewModel : FeedbackPageViewModelBase
{
    private string _content = string.Empty;

    public SurveyViewModel(IFeedbackService service) : base(service)
    {
        Questions.Add(new QuestionRow("孩子年龄", "孩子现在多大？", "3–5 岁", "6–8 岁", "9–12 岁", "12 岁以上"));
        Questions.Add(new QuestionRow("主要设备", "主要在什么设备上用？", "Windows 电脑", "安卓平板", "安卓手机", "Linux", "鸿蒙"));
        Questions.Add(new QuestionRow("使用频率", "大概多久用一次？", "几乎每天", "每周几次", "偶尔用", "刚装上"));
        Questions.Add(new QuestionRow("最常用", "孩子最常用哪一块？", "小游戏", "学习/口算", "故事屋", "关爱中心", "家长管控", "日记本"));
        Questions.Add(new QuestionRow("最希望新增", "最希望我们下一步做什么？", "更多课程", "更多小游戏", "学习报告", "多孩管理", "离线内容", "其他"));
        Questions.Add(new QuestionRow("愿意推荐吗", "愿意推荐给别的家长吗？", "愿意", "看情况", "不愿意"));

        foreach (var q in Questions)
        {
            q.Changed = RefreshProgress;
        }
    }

    public ObservableCollection<QuestionRow> Questions { get; } = new();

    public string Content
    {
        get => _content;
        set => SetProperty(ref _content, value);
    }

    public int AnsweredCount => Questions.Count(q => q.Answered);

    public string ProgressText => $"已答 {AnsweredCount} / {Questions.Count} 题（至少 3 题）";

    public override bool CanSubmit => !IsSending && AnsweredCount >= 3;

    public Task SendAsync()
    {
        var draft = NewDraft("survey");
        draft.Answers = Questions.Where(q => q.Answered)
                                 .ToDictionary(q => q.Id, q => q.Answer);
        if (!string.IsNullOrWhiteSpace(Content)) draft.Content = Content.Trim();
        return SubmitAsync(draft);
    }

    /// <summary>每答一题刷新一次进度与按钮可用状态。</summary>
    public void RefreshProgress()
    {
        OnPropertyChanged(nameof(AnsweredCount));
        OnPropertyChanged(nameof(ProgressText));
        OnPropertyChanged(nameof(CanSubmit));
    }
}
