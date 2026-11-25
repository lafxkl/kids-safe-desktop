using System.Text.Json.Serialization;

namespace KidsSafeDesktop.Models;

/// <summary>
/// 一条待提交的反馈 / 满意度 / 问卷。
///
/// 字段与 KidsSafeServer 的 <c>FeedbackItem</c> 一一对应（camelCase 序列化）。
/// 这里刻意不放任何「孩子是谁」的字段：没有昵称、没有账号、没有设备号，
/// 只有用户自己写的内容和平台/版本号——版本号是为了定位「哪个版本出的问题」。
/// </summary>
public sealed class FeedbackDraft
{
    /// <summary>客户端生成的唯一 id。断网重试时服务端靠它去重，不会多算一份。</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>feedback / satisfaction / survey</summary>
    public string Kind { get; set; } = "feedback";

    public string? Platform { get; set; }

    public string? AppVersion { get; set; }

    /// <summary>从哪个入口来的，例如 parent-dashboard。</summary>
    public string? Channel { get; set; }

    public string? Contact { get; set; }

    /// <summary>整体满意度 1–5。</summary>
    public int? Rating { get; set; }

    /// <summary>推荐意愿 0–10。</summary>
    public int? Nps { get; set; }

    public Dictionary<string, int>? Dims { get; set; }

    public List<string>? Tags { get; set; }

    public Dictionary<string, string>? Answers { get; set; }

    public string? Content { get; set; }

    /// <summary>是否同意在官网匿名展示。默认 false：不勾选就不公开。</summary>
    [JsonPropertyName("public")]
    public bool AllowPublic { get; set; }
}

/// <summary>提交结果。断网时 <see cref="Queued"/> 为 true —— 已经存本机，联网后自动补传。</summary>
public sealed record FeedbackResult(bool Ok, bool Queued, string Message);
