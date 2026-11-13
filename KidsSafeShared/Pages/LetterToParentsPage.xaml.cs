using KidsSafeDesktop.Helpers;
using KidsSafeDesktop.Services;

namespace KidsSafeDesktop.Pages;

/// <summary>
/// 「想对爸妈说」：把想念写成一句话，可以存下来，也可以让系统读出来。
///
/// 为什么有用：留守儿童最常见的问题是「想说但说不出口」，
/// 而电话里往往只剩「嗯」「还好」。先写下来，是把情绪从身体里搬出来；
/// 用 TTS 读出来，则是让孩子听见自己的声音被认真对待。
/// 内容只存本机，不上传。
/// </summary>
public partial class LetterToParentsPage : ContentPage
{
    private static readonly string[] Starters =
    {
        "妈妈，我今天学会了一件事：",
        "爸爸，我有句话一直没说：",
        "我这周最想告诉你们的是：",
        "你们不在的时候，我有点怕的是：",
        "我想你们了，我最近过得……",
        "等你们回来，我想和你们一起："
    };

    public LetterToParentsPage()
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);

        BuildStarters();
        RenderLetters();
    }

    private void BuildStarters()
    {
        StarterHost.Children.Clear();

        foreach (var text in Starters)
        {
            var btn = new Button
            {
                Text = text,
                FontSize = 12.5,
                HeightRequest = 40,
                Padding = new Thickness(14, 0),
                CornerRadius = 14,
                BackgroundColor = Color.FromArgb("#F2F7FF"),
                TextColor = Color.FromArgb("#31597F"),
                HorizontalOptions = LayoutOptions.Start
            };
            var copy = text;
            btn.Clicked += (_, _) =>
            {
                LetterEditor.Text = (LetterEditor.Text ?? string.Empty) + copy;
            };
            StarterHost.Children.Add(btn);
        }
    }

    private void RenderLetters()
    {
        LetterHost.Children.Clear();
        var letters = CareJournal.Letters();

        if (letters.Count == 0)
        {
            LetterHost.Children.Add(new Label
            {
                Text = "还没有写过。想他们的时候，就写一句吧。",
                FontSize = 13,
                TextColor = Color.FromArgb("#8AA3BC"),
                HorizontalOptions = LayoutOptions.Center,
                Margin = new Thickness(0, 16)
            });
            return;
        }

        for (var i = 0; i < letters.Count; i++)
        {
            var letter = letters[i];
            var index = i;

            var grid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)
                },
                ColumnSpacing = 8
            };

            var info = new VerticalStackLayout { Spacing = 4 };
            info.Add(new Label
            {
                Text = letter.Text,
                FontSize = 14,
                TextColor = Color.FromArgb("#2F4A6B"),
                LineHeight = 1.45
            });
            info.Add(new Label
            {
                Text = letter.TimeText,
                FontSize = 10.5,
                TextColor = Color.FromArgb("#9AB0C6")
            });
            grid.Add(info, 0, 0);

            var actions = new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Start };
            var speak = new Button
            {
                Text = "🔊",
                FontSize = 14,
                WidthRequest = 36,
                HeightRequest = 34,
                Padding = 0,
                CornerRadius = 10,
                BackgroundColor = Color.FromArgb("#E3F2FD"),
                TextColor = Color.FromArgb("#1565C0")
            };
            speak.Clicked += (_, _) => _ = Tts.SpeakAsync(letter.Text, "zh");

            var del = new Button
            {
                Text = "🗑",
                FontSize = 13,
                WidthRequest = 36,
                HeightRequest = 34,
                Padding = 0,
                CornerRadius = 10,
                BackgroundColor = Color.FromArgb("#FDECEA"),
                TextColor = Color.FromArgb("#C04848")
            };
            del.Clicked += async (_, _) =>
            {
                var ok = await DisplayAlert("删掉这一句？", letter.Text, "删掉", "算了");
                if (!ok) return;
                CareJournal.DeleteLetter(index);
                RenderLetters();
            };

            actions.Add(speak);
            actions.Add(del);
            grid.Add(actions, 1, 0);

            LetterHost.Children.Add(new Frame
            {
                Padding = new Thickness(12, 10),
                CornerRadius = 16,
                HasShadow = false,
                BackgroundColor = Colors.White,
                BorderColor = Color.FromArgb("#E3EEFB"),
                Content = grid
            });
        }
    }

    private void OnSpeakDraft(object sender, EventArgs e)
    {
        var text = LetterEditor.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            _ = DisplayAlert("还没写呢", "先写一句，再读给你听。", "好");
            return;
        }
        _ = Tts.SpeakAsync(text!, "zh");
    }

    private async void OnSave(object sender, EventArgs e)
    {
        var text = LetterEditor.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            await DisplayAlert("还没写呢", "写一句想对他们说的话吧。", "好");
            return;
        }

        CareJournal.AddLetter(text!);
        LetterEditor.Text = string.Empty;
        RenderLetters();

        try { ServiceProvider.Get<ISoundService>().PlayCorrect(); } catch { /* 忽略 */ }
        await DisplayAlert("存好了", "这句话已经保存在这台手机上了。\n\n想让他们听到的话，可以点 🔊 读出来，再念给他们听。", "好");
    }

    private async void OnBack(object sender, EventArgs e) => await Navigation.PopAsync();
}
