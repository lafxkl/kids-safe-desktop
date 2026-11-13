namespace KidsSafeDesktop.Models;

/// <summary>
/// 通用选择题选项。Display 既可是 emoji/图画，也可是拼音或汉字；
/// FontSize 用于区分展示（emoji 用大字号，拼音/汉字用适中字号）。
/// 选项是每轮静态生成、整批替换的，无需实现属性通知。
/// </summary>
public class ChoiceOption
{
    public string Display { get; }
    public bool IsAnswer { get; }
    public double FontSize { get; }

    public ChoiceOption(string display, bool isAnswer, double fontSize = 30)
    {
        Display = display;
        IsAnswer = isAnswer;
        FontSize = fontSize;
    }
}
