using System.Diagnostics;
using RoundRectangle = Microsoft.Maui.Controls.Shapes.RoundRectangle;

namespace KidsSafeDesktop.Helpers;

/// <summary>
/// 把一段故事排成一张可分享的图片卡。
///
/// 做法说明：没有引入 SkiaSharp（APK 已经 110MB+，为一张卡再加十几 MB 不划算），
/// 而是用正常 XAML 排好版面，再调 MAUI 10 的 <c>View.CaptureAsync()</c> 截图。
/// 好处是「看到的卡片」和「导出的图片」完全一致，不用维护两套绘制代码。
/// 前提是这张卡必须已经挂在页面上渲染过——所以导出动作写在预览页里，而不是后台静默生成。
/// </summary>
public static class StoryCardExporter
{
    /// <summary>图片卡的固定宽度（dp）。窄于这个值正文会排得太挤，宽了在聊天窗口里缩得看不清。</summary>
    public const double CardWidth = 340;

    /// <summary>排版一张故事卡。返回的 View 需要放进页面里才会真正渲染。</summary>
    public static View BuildCard(
        string emoji,
        string title,
        string subtitle,
        IReadOnlyList<string> paragraphs,
        string? moral,
        string footer)
    {
        var stack = new VerticalStackLayout
        {
            Spacing = 0,
            Padding = new Thickness(26, 26, 26, 22)
        };

        // ── 顶部：emoji + 分类小字 ──
        var head = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 10
        };
        head.Add(new Label
        {
            Text = string.IsNullOrWhiteSpace(emoji) ? "🌙" : emoji,
            FontSize = 30,
            VerticalOptions = LayoutOptions.Center
        }, 0, 0);
        head.Add(new Label
        {
            Text = subtitle,
            FontSize = 11,
            TextColor = Color.FromArgb("#9AA6D8"),
            VerticalOptions = LayoutOptions.End,
            Margin = new Thickness(0, 0, 0, 7),
            LineBreakMode = LineBreakMode.TailTruncation
        }, 1, 0);
        stack.Children.Add(head);

        // ── 标题 ──
        stack.Children.Add(new Label
        {
            Text = title,
            FontSize = 21,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#FFE9A8"),
            Margin = new Thickness(0, 12, 0, 0),
            LineBreakMode = LineBreakMode.WordWrap
        });

        stack.Children.Add(new BoxView
        {
            HeightRequest = 2,
            WidthRequest = 34,
            Color = Color.FromArgb("#FFD98A"),
            HorizontalOptions = LayoutOptions.Start,
            Margin = new Thickness(0, 12, 0, 14)
        });

        // ── 正文 ──
        foreach (var p in paragraphs)
        {
            stack.Children.Add(new Label
            {
                Text = p,
                FontSize = 13,
                LineHeight = 1.7,
                TextColor = Color.FromArgb("#E4E9FF"),
                Margin = new Thickness(0, 0, 0, 10),
                LineBreakMode = LineBreakMode.WordWrap
            });
        }

        // ── 寓意 ──
        if (!string.IsNullOrWhiteSpace(moral))
        {
            stack.Children.Add(new Label
            {
                Text = "🌱 " + moral,
                FontSize = 11.5,
                TextColor = Color.FromArgb("#FFD98A"),
                Margin = new Thickness(0, 6, 0, 0),
                LineBreakMode = LineBreakMode.WordWrap
            });
        }

        // ── 落款 ──
        stack.Children.Add(new BoxView
        {
            HeightRequest = 1,
            Color = Color.FromArgb("#2A3568"),
            Margin = new Thickness(0, 16, 0, 10)
        });
        stack.Children.Add(new Label
        {
            Text = footer,
            FontSize = 10,
            TextColor = Color.FromArgb("#7C86B8")
        });
        stack.Children.Add(new Label
        {
            Text = DateTime.Now.ToString("yyyy 年 M 月 d 日"),
            FontSize = 10,
            TextColor = Color.FromArgb("#5E6894"),
            Margin = new Thickness(0, 2, 0, 0)
        });

        return new Border
        {
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 26 },
            Background = new LinearGradientBrush(
                new GradientStopCollection
                {
                    new GradientStop(Color.FromArgb("#2B3470"), 0f),
                    new GradientStop(Color.FromArgb("#1A2149"), 0.55f),
                    new GradientStop(Color.FromArgb("#111631"), 1f)
                },
                new Point(0.15, 0),
                new Point(0.85, 1)),
            Content = stack,
            WidthRequest = CardWidth
        };
    }

    /// <summary>
    /// 把已经渲染出来的卡片截成 PNG，返回文件路径；失败返回 null。
    /// 存到缓存目录而不是相册：孩子没点「分享」之前，不该有任何东西留在相册里。
    /// </summary>
    public static async Task<string?> ExportAsync(View card, string title)
    {
        try
        {
            var shot = await card.CaptureAsync();

            var dir = Path.Combine(FileSystem.CacheDirectory, "story_cards");
            Directory.CreateDirectory(dir);

            var file = Path.Combine(dir, $"{SafeName(title)}-{DateTime.Now:yyyyMMdd-HHmmss}.png");

            await using (var src = await shot.OpenReadAsync())
            await using (var dst = File.Create(file))
            {
                await src.CopyToAsync(dst);
            }

            return file;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[StoryCard] 导出图片失败：{ex}");
            return null;
        }
    }

    /// <summary>故事卡的纯文本版，用于「复制文字」——有些家长的聊天软件发图会被压缩。</summary>
    public static string ToText(string title, IReadOnlyList<string> paragraphs, string? moral)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(title);
        sb.AppendLine();
        foreach (var p in paragraphs)
        {
            sb.AppendLine(p);
            sb.AppendLine();
        }
        if (!string.IsNullOrWhiteSpace(moral)) sb.AppendLine("🌱 " + moral);
        sb.AppendLine();
        sb.AppendLine("—— 来自 儿童安全桌面 · 星夜故事屋");
        return sb.ToString().Trim();
    }

    private static string SafeName(string name)
    {
        var bad = Path.GetInvalidFileNameChars();
        var chars = name.Select(ch => bad.Contains(ch) ? '_' : ch).ToArray();
        var s = new string(chars).Trim();
        if (string.IsNullOrWhiteSpace(s)) s = "story";
        return s.Length > 24 ? s[..24] : s;
    }
}
