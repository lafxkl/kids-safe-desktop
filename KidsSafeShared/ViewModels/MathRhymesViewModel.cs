using System.Collections.ObjectModel;
using KidsSafeDesktop.Models;

namespace KidsSafeDesktop.ViewModels;

/// <summary>口诀乐园：展示乘法 / 加减法 / 除法口诀三个主题，供小朋友浏览学习。</summary>
public class MathRhymesViewModel : BaseViewModel
{
    public ObservableCollection<RhymeTopic> Topics { get; } = new();

    public MathRhymesViewModel()
    {
        foreach (var t in MathRhymeData.Topics)
            Topics.Add(t);
    }
}
