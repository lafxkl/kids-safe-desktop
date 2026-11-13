using KidsSafeDesktop.Helpers;

namespace KidsSafeDesktop.Pages;

/// <summary>
/// 4-7-8 呼吸放松。
///
/// 为什么给孩子做这个：情绪上头时讲道理是无效的，先让身体慢下来才有用。
/// 4-7-8 的关键是「呼气比吸气长」，能激活副交感神经；
/// 界面上只给圆圈和数字，不讲术语，孩子跟着缩放走就行。
/// </summary>
public partial class BreathingPage : ContentPage
{
    private CancellationTokenSource? _cts;
    private bool _running;

    private const int Rounds = 4;

    public BreathingPage()
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);
    }

    private async void OnStart(object sender, EventArgs e)
    {
        if (_running) return;

        _running = true;
        StartBtn.IsEnabled = false;
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        try
        {
            for (var round = 1; round <= Rounds; round++)
            {
                RoundLabel.Text = $"第 {round} / {Rounds} 轮";

                // 吸气 4
                await PhaseAsync("吸气", 4, 1.0, 1.32, token);
                // 屏住 7
                await PhaseAsync("屏住", 7, 1.32, 1.34, token);
                // 呼气 8
                await PhaseAsync("呼气", 8, 1.34, 1.0, token);
            }

            PhaseLabel.Text = "做完了";
            CountLabel.Text = "🌿";
            RoundLabel.Text = $"{Rounds} / {Rounds} 轮";
            HintLabel.Text = "感觉一下：现在的心跳，是不是比刚才慢一点了？";

            await LearnCheckIn.ShowEncourageAsync(this, "呼吸放松", "🌬️", "breathing", 1,
                $"完成了 {Rounds} 轮呼吸放松");
        }
        catch (OperationCanceledException)
        {
            // 用户主动停下，属于正常流程
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Breathing] 练习中断：{ex.Message}");
        }
        finally
        {
            _running = false;
            StartBtn.IsEnabled = true;
        }
    }

    private async Task PhaseAsync(string name, int seconds, double fromScale, double toScale, CancellationToken token)
    {
        PhaseLabel.Text = name;
        HintLabel.Text = name switch
        {
            "吸气" => "用鼻子慢慢吸，把气吸到肚子里",
            "屏住" => "轻轻停住，不要用力",
            _ => "用嘴慢慢呼，像吹凉一碗热汤"
        };

        Circle.Scale = fromScale;
        _ = Circle.ScaleTo(toScale, (uint)(seconds * 1000), Easing.SinInOut);

        for (var i = seconds; i >= 1; i--)
        {
            token.ThrowIfCancellationRequested();
            CountLabel.Text = i.ToString();
            await Task.Delay(1000, token);
        }

        CountLabel.Text = string.Empty;
    }

    private void OnStop(object sender, EventArgs e)
    {
        Stop();
        PhaseLabel.Text = "停下了";
        CountLabel.Text = string.Empty;
        RoundLabel.Text = string.Empty;
        HintLabel.Text = "没关系，想练的时候再来。";
        _ = Circle.ScaleTo(1.0, 300);
    }

    private void Stop()
    {
        try { _cts?.Cancel(); } catch { /* 已释放 */ }
        _cts?.Dispose();
        _cts = null;
        _running = false;
        StartBtn.IsEnabled = true;
    }

    private async void OnBack(object sender, EventArgs e)
    {
        Stop();
        await Navigation.PopAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        Stop();
    }
}
