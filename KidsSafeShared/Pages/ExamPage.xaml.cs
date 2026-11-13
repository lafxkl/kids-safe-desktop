using KidsSafeDesktop.Models;
using KidsSafeDesktop.Services;
using KidsSafeDesktop.ViewModels;
using Microsoft.Maui.Controls;

namespace KidsSafeDesktop.Pages;

/// <summary>
/// 模拟考试答题页：一次一题，可前后翻页、可改答案，最后统一批改。
/// 计时器在页面出现时启动、消失时停止，避免后台继续跑秒。
/// </summary>
public partial class ExamPage : ContentPage
{
    private readonly ExamViewModel _vm;
    private readonly ExamPaper _paper;
    private IDispatcherTimer? _timer;

    public ExamPage(ExamPaper paper)
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);
        _paper = paper;
        _vm = new ExamViewModel();
        BindingContext = _vm;
        TitleLabel.Text = paper.Title;
        _vm.Start(paper);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _timer ??= Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick -= OnTick;
        _timer.Tick += OnTick;
        _timer.Start();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _timer?.Stop();
    }

    private void OnTick(object? sender, EventArgs e) => _vm.Tick();

    private void OnOptionTapped(object sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is not ExamOption option) return;
        _vm.SelectOption(option);
    }

    private void OnFillUnfocused(object sender, FocusEventArgs e) => _vm.CommitFill();

    private void OnFillCompleted(object sender, EventArgs e) => _vm.CommitFill();

    private void OnPrev(object sender, EventArgs e) => _vm.Prev();

    private async void OnNext(object sender, EventArgs e)
    {
        // 先把输入框里可能还没提交的字符写进答案
        _vm.CommitFill();

        // 最后一道题按「交卷」：还有空题先提醒一次，避免手滑
        if (_vm.IsLastQuestion)
        {
            var left = _vm.UnansweredCount;
            if (left > 0)
            {
                var go = await DisplayAlert("还有题目没做", $"还有 {left} 道题空着，确定要交卷吗？", "交卷", "继续答题");
                if (!go) return;
            }
        }

        _vm.Next();
        if (_vm.Finished) _timer?.Stop();
    }

    private async void OnReplay(object sender, EventArgs e)
    {
        var again = await DisplayAlert("再做一次", "要清空答案重新做这套卷子吗？", "重新开始", "取消");
        if (!again) return;
        _vm.Start(_paper);
        _timer?.Start();
    }

    private async void OnBack(object sender, EventArgs e)
    {
        if (!_vm.Finished)
        {
            var leave = await DisplayAlert("要离开吗", "现在离开，已写的答案不会保存。", "离开", "继续答题");
            if (!leave) return;
        }
        await Navigation.PopAsync();
    }
}
