namespace KidsSafeDesktop.Models;

/// <summary>故事分类（对应 mxnzp 的 type_id）。</summary>
public class StoryCategory
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>用于卡片展示的图标（按分类名启发，纯装饰）。</summary>
    public string Emoji { get; set; } = "📚";
}

/// <summary>故事列表/搜索项（不含正文）。</summary>
public class StorySummary
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    /// <summary>分类名，如「睡前故事」。</summary>
    public string Type { get; set; } = string.Empty;
    /// <summary>字数。</summary>
    public int Length { get; set; }
    /// <summary>预计朗读时长 HH:MM:SS。</summary>
    public string ReadTime { get; set; } = string.Empty;
    /// <summary>内容摘要（含换行，展示时截断）。</summary>
    public string ShortDesc { get; set; } = string.Empty;
}

/// <summary>故事正文（含完整内容，用于阅读/朗读）。</summary>
public class StoryDetail
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int Length { get; set; }
    public string ReadTime { get; set; } = string.Empty;
    /// <summary>完整故事文本（含换行，朗读时按段落切分）。</summary>
    public string Content { get; set; } = string.Empty;
}
