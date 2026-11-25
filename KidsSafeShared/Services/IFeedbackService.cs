using KidsSafeDesktop.Models;

namespace KidsSafeDesktop.Services;

/// <summary>
/// 用户反馈上报（意见反馈 / 满意度调查 / 使用问卷）。
/// 数据统一进开发者服务器的 /api/feedback/submit，官网页面读同一份数据做公开统计。
/// </summary>
public interface IFeedbackService
{
    /// <summary>本机还有几条没发出去（断网时攒下的）。</summary>
    int PendingCount { get; }

    /// <summary>最近一次操作的结果文字，给界面直接显示。</summary>
    string Status { get; }

    /// <summary>启动/进页面时调一次：把上次没发出去的补传掉。失败不抛异常。</summary>
    Task<int> FlushAsync();

    Task<FeedbackResult> SubmitAsync(FeedbackDraft draft);
}
