using System.Collections.ObjectModel;
using System.Linq;
using KidsSafeDesktop.Helpers;
using KidsSafeDesktop.Models;

namespace KidsSafeDesktop.ViewModels;

/// <summary>
/// 身体百科：按部位分类浏览，点选查看说明与趣味知识，可语音朗读部位名称。
/// </summary>
public class BodyEncyclopediaViewModel : BaseViewModel
{
    private string _selectedCategory = "全部";
    private BodyPart? _selectedPart;

    public ObservableCollection<string> Categories { get; }

    public ObservableCollection<BodyPart> Parts { get; } = new();

    public string SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (!SetProperty(ref _selectedCategory, value)) return;
            ApplyCategory(value);
        }
    }

    public BodyPart? SelectedPart
    {
        get => _selectedPart;
        set
        {
            if (SetProperty(ref _selectedPart, value))
                OnPropertyChanged(nameof(HasSelection));
        }
    }

    public bool HasSelection => _selectedPart != null;

    public BodyEncyclopediaViewModel()
    {
        Categories = new ObservableCollection<string>(BodyData.Categories);
        SelectedCategory = "全部";
        ApplyCategory("全部");
    }

    private void ApplyCategory(string category)
    {
        var list = category == "全部"
            ? BodyData.Parts
            : BodyData.Parts.Where(p => p.Category == category).ToList();
        Parts.Clear();
        foreach (var p in list) Parts.Add(p);
        SelectedPart = null;
    }

    public void SelectPart(BodyPart part)
    {
        SelectedPart = part;
        _ = Tts.SpeakAsync(part.Name, "zh");
    }

    public void SpeakCurrent() => _ = Tts.SpeakAsync(_selectedPart?.Name ?? string.Empty, "zh");
}
