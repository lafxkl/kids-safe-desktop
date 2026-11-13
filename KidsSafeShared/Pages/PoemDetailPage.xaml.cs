using KidsSafeDesktop.Helpers;
using KidsSafeDesktop.Models;
using Microsoft.Maui.Controls;

namespace KidsSafeDesktop.Pages;

/// <summary>
/// 古诗详情：正文大字 + 注释 + 译文 + 小知识，可朗读、可翻上下首。
/// 朗读走系统 TTS（<see cref="Tts"/>），没有语音包时静默失败但不影响阅读。
/// </summary>
public partial class PoemDetailPage : ContentPage
{
    private readonly List<Poem> _list;
    private int _index;
    private Poem _poem;

    private bool _speaking;

    public PoemDetailPage(List<Poem> list, int index)
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);

        _list = list.Count > 0 ? list : PoemData.All;
        _index = Math.Clamp(index, 0, _list.Count - 1);
        _poem = _list[_index];

        Render();
    }

    private void Render()
    {
        _poem = _list[_index];

        BindingContext = _poem;
        BindableLayout.SetItemsSource(LinesLayout, _poem.Lines);
        BindableLayout.SetItemsSource(NotesLayout, _poem.Notes);

        ProgressLabel.Text = $"{_index + 1} / {_list.Count}";
        PrevBtn.IsEnabled = _index > 0;
        NextBtn.IsEnabled = _index < _list.Count - 1;
        PrevBtn.Opacity = PrevBtn.IsEnabled ? 1 : 0.4;
        NextBtn.Opacity = NextBtn.IsEnabled ? 1 : 0.4;

        _ = ContentScroll.ScrollToAsync(0, 0, false);
    }

    private void OnPrev(object sender, EventArgs e)
    {
        if (_index <= 0) return;
        _index--;
        Render();
    }

    private void OnNext(object sender, EventArgs e)
    {
        if (_index >= _list.Count - 1) return;
        _index++;
        Render();
    }

    private async void OnSpeak(object sender, EventArgs e)
    {
        if (_speaking) return;

        _speaking = true;
        SpeakBtn.IsEnabled = false;
        var original = SpeakBtn.Text;
        SpeakBtn.Text = "🔊 正在朗读…";

        try
        {
            await Tts.SpeakAsync(_poem.SpeakText);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[古诗] 朗读失败: {ex.Message}");
        }
        finally
        {
            _speaking = false;
            SpeakBtn.Text = original;
            SpeakBtn.IsEnabled = true;
        }
    }

    private async void OnBack(object sender, EventArgs e) => await Navigation.PopAsync();
}
