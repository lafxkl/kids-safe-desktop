using System.Collections.ObjectModel;
using KidsSafeDesktop.Models;

namespace KidsSafeDesktop.ViewModels;

/// <summary>英语学习首页：列出一年级～四年级入口。</summary>
public class EnglishGradeCard
{
    public EnglishGrade Grade { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Emoji { get; init; } = "🔤";
    public string Desc { get; init; } = string.Empty;
    public string Color { get; init; } = "#7E57C2";
}

public class EnglishWorldViewModel : BaseViewModel
{
    public ObservableCollection<EnglishGradeCard> Grades { get; } = new()
    {
        new EnglishGradeCard { Grade = EnglishGrade.Grade1, Title = "一年级", Emoji = "🍎", Desc = "字母启蒙 · 动物水果 · 颜色数字", Color = "#EF5350" },
        new EnglishGradeCard { Grade = EnglishGrade.Grade2, Title = "二年级", Emoji = "🐼", Desc = "食物衣物 · 学校用品 · 身体部位", Color = "#26A69A" },
        new EnglishGradeCard { Grade = EnglishGrade.Grade3, Title = "三年级", Emoji = "🌞", Desc = "天气动作 · 形容词 · 常见职业", Color = "#FFA726" },
        new EnglishGradeCard { Grade = EnglishGrade.Grade4, Title = "四年级", Emoji = "🏠", Desc = "家居学科 · 衣物描述 · 丰富词汇", Color = "#5C6BC0" },
        new EnglishGradeCard { Grade = EnglishGrade.Grade5, Title = "五年级", Emoji = "🚌", Desc = "交通地点 · 日常动作 · 频率时间", Color = "#FFA726" },
        new EnglishGradeCard { Grade = EnglishGrade.Grade6, Title = "六年级", Emoji = "✈️", Desc = "比较级 · 旅行自然 · 过去式", Color = "#EC407A" },
        new EnglishGradeCard { Grade = EnglishGrade.Grade7, Title = "初一", Emoji = "🎒", Desc = "校园生活 · 时间表达 · 情绪动作", Color = "#7E57C2" },
        new EnglishGradeCard { Grade = EnglishGrade.Grade8, Title = "初二", Emoji = "🔭", Desc = "健康建议 · 环境科技 · 连接副词", Color = "#42A5F5" },
        new EnglishGradeCard { Grade = EnglishGrade.Grade9, Title = "初三", Emoji = "🎓", Desc = "中考高频抽象词 · 动词 · 固定短语", Color = "#26A69A" }
    };
}
