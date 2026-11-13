using System.Collections.ObjectModel;
using KidsSafeDesktop.Models;

namespace KidsSafeDesktop.ViewModels;

/// <summary>
/// 安全知识板块：展示安全主题分类（交通、防火、防骗等），供小朋友浏览学习。
/// </summary>
public class SafetyKnowledgeViewModel : BaseViewModel
{
    public ObservableCollection<SafetyTopic> Topics { get; } = new();

    public SafetyKnowledgeViewModel()
    {
        foreach (var t in SafetyKnowledgeData.Topics)
            Topics.Add(t);
    }
}
