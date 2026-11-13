using KidsSafeDesktop.Models;
using KidsSafeDesktop.Services;
using Microsoft.Maui.Controls;

namespace KidsSafeDesktop.Pages;

/// <summary>
/// 某个年级的模拟卷列表：语文 / 数学 / 英语各一套，点开进入答题页。
/// </summary>
public partial class ExamPaperListPage : ContentPage
{
    private readonly int _grade;

    public ExamPaperListPage(int grade)
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);
        _grade = grade;
        TitleLabel.Text = $"{GradeName(grade)} · 模拟考试";
        HintLabel.Text = "选一套卷子，按顺序做完再交卷，会帮你打分并讲解错题～";
        LoadPapers();
    }

    private void LoadPapers()
    {
        var papers = QuestionBank.Instance.Papers(_grade);
        PaperList.ItemsSource = papers;

        if (papers.Count == 0)
            HintLabel.Text = "这个年级的模拟卷还在整理中，先练练口算吧～";
    }

    private void OnPaperTapped(object sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not ExamPaper paper) return;
        _ = Navigation.PushAsync(new ExamPage(paper));
    }

    private async void OnBack(object sender, EventArgs e) => await Navigation.PopAsync();

    internal static string GradeName(int grade) => grade switch
    {
        1 => "一年级",
        2 => "二年级",
        3 => "三年级",
        4 => "四年级",
        5 => "五年级",
        6 => "六年级",
        7 => "初一",
        8 => "初二",
        9 => "初三",
        _ => $"{grade} 年级"
    };
}
