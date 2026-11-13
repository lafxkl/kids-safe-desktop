using KidsSafeDesktop.Helpers;
using Microsoft.Maui.ApplicationModel.DataTransfer;

namespace KidsSafeDesktop.Pages;

/// <summary>
/// 故事图片卡的预览页。
///
/// 为什么要有这一页：<c>CaptureAsync</c> 只能截「已经渲染在界面上的 View」，
/// 所以不能后台偷偷生成——必须先把它排在屏幕上。既然躲不过，就干脆做成预览：
/// 孩子看到的就是要发出去的那张卡，不至于发出去才发现排版难看。
/// 同时给了「复制文字」的退路：有些聊天软件会把长图压得看不清。
/// </summary>
public sealed class StoryCardPage : ContentPage
{
    private readonly View _card;
    private readonly string _title;
    private readonly IReadOnlyList<string> _paragraphs;
    private readonly string? _moral;

    private readonly Button _shareBtn;
    private readonly Label _status;
    private bool _busy;

    public StoryCardPage(
        string emoji,
        string title,
        string subtitle,
        IReadOnlyList<string> paragraphs,
        string? moral,
        string footer)
    {
        NavigationPage.SetHasNavigationBar(this, false);
        BackgroundColor = Color.FromArgb("#0A0E2A");

        _title = title;
        _paragraphs = paragraphs;
        _moral = moral;
        _card = StoryCardExporter.BuildCard(emoji, title, subtitle, paragraphs, moral, footer);

        // 卡片默认 340dp，窄屏上要收一点，否则右边会被裁掉（预览看到的就是截出去的图）
        try
        {
            var info = DeviceDisplay.Current.MainDisplayInfo;
            if (info.Density > 0)
            {
                var screenDp = info.Width / info.Density;
                _card.WidthRequest = Math.Min(StoryCardExporter.CardWidth, Math.Max(280, screenDp - 52));
            }
        }
        catch
        {
            // 取不到屏幕信息就用默认宽度
        }

        // ── 顶栏 ──
        var back = new Button
        {
            Text = "←",
            FontSize = 18,
            WidthRequest = 40,
            HeightRequest = 40,
            CornerRadius = 20,
            BackgroundColor = Color.FromArgb("#1C244E"),
            TextColor = Color.FromArgb("#EEF1FF")
        };
        back.Clicked += async (_, _) => await Navigation.PopAsync();

        var top = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)
            },
            Padding = new Thickness(14, 12),
            ColumnSpacing = 10
        };
        top.Add(back, 0, 0);
        top.Add(new Label
        {
            Text = "故事图片卡",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#FFE9A8"),
            VerticalOptions = LayoutOptions.Center
        }, 1, 0);

        // ── 卡片预览 ──
        var preview = new ScrollView { Padding = new Thickness(16, 4, 16, 10) };
        preview.Content = new VerticalStackLayout
        {
            Spacing = 12,
            HorizontalOptions = LayoutOptions.Center,
            Children =
            {
                _card,
                new Label
                {
                    Text = "分享出去的就是这张图",
                    FontSize = 11.5,
                    TextColor = Color.FromArgb("#7C86B8"),
                    HorizontalOptions = LayoutOptions.Center
                }
            }
        };

        // ── 底部操作 ──
        _shareBtn = new Button
        {
            Text = "📤 分享图片",
            FontSize = 15,
            FontAttributes = FontAttributes.Bold,
            HeightRequest = 48,
            CornerRadius = 16,
            BackgroundColor = Color.FromArgb("#FFD60A"),
            TextColor = Color.FromArgb("#2A2200")
        };
        _shareBtn.Clicked += OnShare;

        var copyBtn = new Button
        {
            Text = "📋 复制文字",
            FontSize = 15,
            HeightRequest = 48,
            CornerRadius = 16,
            BackgroundColor = Color.FromArgb("#1C244E"),
            TextColor = Color.FromArgb("#EEF1FF")
        };
        copyBtn.Clicked += OnCopy;

        _status = new Label
        {
            Text = "",
            FontSize = 11.5,
            TextColor = Color.FromArgb("#8C95C4"),
            HorizontalOptions = LayoutOptions.Center
        };

        var btnRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 10
        };
        btnRow.Add(_shareBtn, 0, 0);
        btnRow.Add(copyBtn, 1, 0);

        var actions = new VerticalStackLayout
        {
            Spacing = 8,
            Padding = new Thickness(16, 6, 16, 16),
            Children = { btnRow, _status }
        };

        var root = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto)
            }
        };
        root.Add(top, 0, 0);
        root.Add(preview, 0, 1);
        root.Add(actions, 0, 2);

        Content = root;
    }

    private async void OnShare(object? sender, EventArgs e)
    {
        if (_busy) return;
        _busy = true;
        _shareBtn.IsEnabled = false;
        _shareBtn.Text = "正在生成…";
        _status.Text = "";

        try
        {
            var path = await StoryCardExporter.ExportAsync(_card, _title);

            if (path is null)
            {
                _status.Text = "这台设备暂时没法生成图片，可以用「复制文字」发出去。";
                return;
            }

            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = _title,
                File = new ShareFile(path)
            });

            _status.Text = "发出去啦 🌙";
        }
        catch (Exception ex)
        {
            // 用户取消分享也会走到这里，不必当成错误大惊小怪
            _status.Text = "分享没有完成，可以再试一次。";
            System.Diagnostics.Debug.WriteLine($"[StoryCard] 分享失败：{ex}");
        }
        finally
        {
            _busy = false;
            _shareBtn.IsEnabled = true;
            _shareBtn.Text = "📤 分享图片";
        }
    }

    private async void OnCopy(object? sender, EventArgs e)
    {
        try
        {
            await Clipboard.Default.SetTextAsync(
                StoryCardExporter.ToText(_title, _paragraphs, _moral));
            _status.Text = "文字已复制，粘到聊天框里就行 📋";
        }
        catch (Exception ex)
        {
            _status.Text = "复制失败，可以手动选中正文。";
            System.Diagnostics.Debug.WriteLine($"[StoryCard] 复制失败：{ex}");
        }
    }
}
