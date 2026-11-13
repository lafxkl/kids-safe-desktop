using KidsSafeDesktop.Helpers;
using KidsSafeDesktop.Models;

namespace KidsSafeDesktop.Pages;

/// <summary>心理小课堂正文页：分段阅读 + TTS 朗读 + 「我看完了」进入激励页。</summary>
public partial class CareLessonDetailPage : ContentPage
{
    private readonly CareLesson _lesson;
    private CancellationTokenSource? _cts;
    private bool _finished;

    public CareLessonDetailPage(CareLesson lesson)
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);

        _lesson = lesson;

        HeaderLabel.Text = lesson.Title;
        AudienceLabel.Text = lesson.AudienceText;
        EmojiLabel.Text = lesson.Emoji;
        TitleText.Text = lesson.Title;
        SubtitleText.Text = lesson.Subtitle;
        KeyLineText.Text = "“" + lesson.KeyLine + "”";

        BuildSections();
    }

    private void BuildSections()
    {
        SectionHost.Children.Clear();

        var no = 1;
        foreach (var (heading, body) in _lesson.Sections)
        {
            var card = new Frame
            {
                Padding = new Thickness(14, 12),
                CornerRadius = 16,
                HasShadow = false,
                BackgroundColor = Colors.White,
                BorderColor = Color.FromArgb("#E8F3E9")
            };

            var stack = new VerticalStackLayout { Spacing = 6 };
            stack.Add(new Label
            {
                Text = $"{no}. {heading}",
                FontSize = 15.5,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#2E5C3A")
            });
            stack.Add(new Label
            {
                Text = body,
                FontSize = 14,
                TextColor = Color.FromArgb("#41524A"),
                LineHeight = 1.6
            });

            card.Content = stack;
            SectionHost.Children.Add(card);
            no++;
        }
    }

    // ── 朗读 ────────────────────────────────────────────────

    private string FullText()
    {
        var parts = new List<string> { _lesson.Title, _lesson.Subtitle };
        parts.AddRange(_lesson.Sections.Select(s => s.Heading + "。" + s.Body));
        parts.Add(_lesson.KeyLine);
        return string.Join("\n", parts);
    }

    private async void OnSpeak(object sender, EventArgs e)
    {
        StopSpeak();

        _cts = new CancellationTokenSource();
        SpeakBtn.IsEnabled = false;
        StopBtn.IsEnabled = true;

        try
        {
            await Tts.SpeakAsync(FullText(), "zh", _cts.Token);
        }
        catch (OperationCanceledException) { /* 正常停止 */ }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Care] 朗读失败：{ex.Message}");
        }
        finally
        {
            SpeakBtn.IsEnabled = true;
            StopBtn.IsEnabled = false;
        }
    }

    private void OnStop(object sender, EventArgs e) => StopSpeak();

    private void StopSpeak()
    {
        try { _cts?.Cancel(); } catch { /* 已释放 */ }
        _cts?.Dispose();
        _cts = null;
        SpeakBtn.IsEnabled = true;
        StopBtn.IsEnabled = false;
    }

    // ── 完成 ────────────────────────────────────────────────

    private async void OnFinish(object sender, EventArgs e)
    {
        if (_finished) return;
        _finished = true;

        StopSpeak();
        await LearnCheckIn.ShowEncourageAsync(this, "心理小课堂", _lesson.Emoji, "care_lesson", 2,
            $"看完了「{_lesson.Title}」");
    }

    private async void OnBack(object sender, EventArgs e)
    {
        StopSpeak();
        await Navigation.PopAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        StopSpeak();
    }
}
