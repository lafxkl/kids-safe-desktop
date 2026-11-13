using System.Collections.ObjectModel;
using KidsSafeDesktop.Models;

namespace KidsSafeDesktop.ViewModels;

/// <summary>学习天地首页：列出一年级～四年级入口。</summary>
public class GradeCard
{
    public MathGrade Grade { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Emoji { get; init; } = "📘";
    public string Desc { get; init; } = string.Empty;
    public string Color { get; init; } = "#29B6F6";
}

public class LearnWorldViewModel : BaseViewModel
{
    public ObservableCollection<GradeCard> Grades { get; } = new()
    {
        new GradeCard { Grade = MathGrade.Grade1, Title = "一年级", Emoji = "🍎", Desc = "20 以内加减 · 100 以内不进位", Color = "#EF5350" },
        new GradeCard { Grade = MathGrade.Grade2, Title = "二年级", Emoji = "🐰", Desc = "表内乘除 · 有余数 · 两步混合", Color = "#AB47BC" },
        new GradeCard { Grade = MathGrade.Grade3, Title = "三年级", Emoji = "🚀", Desc = "万以内加减 · 多位数乘 · 除一位数", Color = "#26C6DA" },
        new GradeCard { Grade = MathGrade.Grade4, Title = "四年级", Emoji = "🌟", Desc = "三位数乘两位 · 除两位数 · 四则混合", Color = "#66BB6A" },
        new GradeCard { Grade = MathGrade.Grade5, Title = "五年级", Emoji = "🌈", Desc = "小数乘除 · 简易方程 · 图形面积", Color = "#FFA726" },
        new GradeCard { Grade = MathGrade.Grade6, Title = "六年级", Emoji = "🎯", Desc = "分数 · 百分数 · 比例 · 圆", Color = "#EC407A" },
        new GradeCard { Grade = MathGrade.Grade7, Title = "初一", Emoji = "🧭", Desc = "有理数 · 整式 · 一元一次方程", Color = "#7E57C2" },
        new GradeCard { Grade = MathGrade.Grade8, Title = "初二", Emoji = "🔭", Desc = "幂与根式 · 一次函数 · 方程组", Color = "#42A5F5" },
        new GradeCard { Grade = MathGrade.Grade9, Title = "初三", Emoji = "🎓", Desc = "二次根式 · 一元二次方程 · 函数", Color = "#26A69A" }
    };
}
