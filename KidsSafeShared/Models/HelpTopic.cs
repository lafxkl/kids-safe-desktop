using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace KidsSafeDesktop.Models;

/// <summary>
/// 帮助中心的一个条目：分类 + 图标 + 标题 + 摘要 + 分步说明。
/// 实现 INotifyPropertyChanged 以便点击时展开/收起步骤。
/// </summary>
public class HelpTopic : INotifyPropertyChanged
{
    private bool _isExpanded;

    public string Id { get; set; } = string.Empty;

    /// <summary>所属分类：操作指南 / 语音配置 / 常见问题。</summary>
    public string Category { get; set; } = string.Empty;

    public string Emoji { get; set; } = "📌";
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;

    /// <summary>分步说明，展开后显示。</summary>
    public List<string> Steps { get; set; } = new();

    /// <summary>是否为重点条目（界面上高亮提示）。</summary>
    public bool IsImportant { get; set; }

    /// <summary>默认是否展开（语音配置等引导类条目默认展开）。</summary>
    public bool DefaultExpanded { get; set; }

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value) return;
            _isExpanded = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ExpandArrow));
        }
    }

    public string ExpandArrow => _isExpanded ? "▲" : "▼";

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public void Toggle() => IsExpanded = !IsExpanded;
}
