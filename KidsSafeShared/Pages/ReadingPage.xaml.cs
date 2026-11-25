using KidsSafeDesktop.Models;

namespace KidsSafeDesktop.Pages;

/// <summary>
/// 关爱中心 →「绘本怎么找」。
///
/// 这个页面只做一件事：告诉家长哪几家能合法免费看、每家是什么形式、经典绘本去哪借。
/// 不托管、不转存、不给任何 PDF 直链——原因写在页尾那张提示卡里，也写在 ReadingCatalog 的注释里。
/// </summary>
public partial class ReadingPage : ContentPage
{
    private static readonly Color Ink = Color.FromArgb("#3A2B22");
    private static readonly Color Soft = Color.FromArgb("#7C6353");
    private static readonly Color Faint = Color.FromArgb("#A1887F");
    private static readonly Color Accent = Color.FromArgb("#E2603D");
    private static readonly Color CardBorder = Color.FromArgb("#F4E5D3");

    public ReadingPage()
    {
        InitializeComponent();
        Build();
    }

    private void Build()
    {
        Host.Children.Add(TipCard(
            "📖 只收「能合法免费看」的地方",
            "下面三张卡都是公益图书馆、基金会项目或开放目录，点进去就能看。但它们的形式不一样："
            + "有的是扫描版电子书，有的是演员朗读视频，有的是线上借阅。都没有「下载 PDF」这一步，"
            + "因为这些书本来就不允许整本下载。"));

        Host.Children.Add(SectionTitle("三个可以免费看的来源"));

        foreach (var source in ReadingCatalog.Sources)
        {
            Host.Children.Add(SourceCard(source));
        }

        Host.Children.Add(SectionTitle("常被问到的几本，去哪儿找"));
        Host.Children.Add(new Label
        {
            Text = "这几本都还在版权保护期内。本页不提供 PDF，也不建议去网盘找 —— 可以先去 Open Library 查有没有电子版或馆藏，"
                 + "没有的话去少儿图书馆借，或者买一本平装版。",
            FontSize = 12.5,
            TextColor = Soft,
            LineHeight = 1.6,
            Margin = new Thickness(2, -2, 2, 2)
        });

        foreach (var book in ReadingCatalog.Picks)
        {
            Host.Children.Add(BookCard(book));
        }

        Host.Children.Add(RulesCard());

        Host.Children.Add(new Label
        {
            Text = $"清单更新于 {ReadingCatalog.UpdatedAt}　·　出处、授权与核对方式见官网「关爱中心 · 内容来源」",
            FontSize = 11,
            TextColor = Faint,
            HorizontalTextAlignment = TextAlignment.Center,
            Margin = new Thickness(6, 6, 6, 0)
        });
    }

    // ── 卡片 ────────────────────────────────────────────────────

    private static Frame TipCard(string title, string body)
    {
        var stack = new VerticalStackLayout { Spacing = 6 };
        stack.Children.Add(new Label
        {
            Text = title,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#8A6D00")
        });
        stack.Children.Add(new Label
        {
            Text = body,
            FontSize = 12.5,
            TextColor = Color.FromArgb("#6B5A2A"),
            LineHeight = 1.65
        });

        return new Frame
        {
            Padding = new Thickness(16, 14),
            CornerRadius = 20,
            HasShadow = false,
            BackgroundColor = Color.FromArgb("#FFF8E1"),
            BorderColor = Color.FromArgb("#FFE082"),
            Content = stack
        };
    }

    private static Label SectionTitle(string text) => new()
    {
        Text = text,
        FontSize = 15.5,
        FontAttributes = FontAttributes.Bold,
        TextColor = Ink,
        Margin = new Thickness(2, 10, 2, 0)
    };

    private static Frame SourceCard(ReadingSource s)
    {
        var stack = new VerticalStackLayout { Spacing = 8 };

        // 第一行：图标 + 名称 + 语言/年龄
        var head = new HorizontalStackLayout { Spacing = 10 };
        head.Children.Add(new Label
        {
            Text = s.Emoji,
            FontSize = 26,
            VerticalOptions = LayoutOptions.Center
        });

        var titleCol = new VerticalStackLayout { Spacing = 2 };
        titleCol.Children.Add(new Label
        {
            Text = s.Name,
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = Ink
        });
        titleCol.Children.Add(new Label
        {
            Text = $"{s.Lang}　·　{s.Ages}",
            FontSize = 11.5,
            TextColor = Faint
        });
        head.Children.Add(titleCol);
        stack.Children.Add(head);

        stack.Children.Add(Body(s.What, 13, Ink));

        var how = new VerticalStackLayout { Spacing = 1 };
        how.Children.Add(new Label
        {
            Text = "怎么用",
            FontSize = 11.5,
            FontAttributes = FontAttributes.Bold,
            TextColor = Accent
        });
        how.Children.Add(Body(s.How, 12.5, Soft));
        stack.Children.Add(how);

        stack.Children.Add(Body("注意：" + s.Note, 11.5, Faint));

        var open = new Button
        {
            Text = "打开网站",
            FontSize = 13.5,
            HeightRequest = 38,
            CornerRadius = 19,
            Padding = new Thickness(20, 0),
            BackgroundColor = Accent,
            TextColor = Colors.White,
            HorizontalOptions = LayoutOptions.Start
        };
        open.Clicked += async (_, _) => await OpenAsync(s.Url);
        stack.Children.Add(open);

        stack.Children.Add(new Label
        {
            Text = new Uri(s.Url).Host,
            FontSize = 10.5,
            TextColor = Faint
        });

        return new Frame
        {
            Padding = new Thickness(16, 14),
            CornerRadius = 20,
            HasShadow = false,
            BackgroundColor = Colors.White,
            BorderColor = CardBorder,
            Content = stack
        };
    }

    private static Frame BookCard(BookPick b)
    {
        var stack = new VerticalStackLayout { Spacing = 5 };

        stack.Children.Add(new Label
        {
            Text = b.Title,
            FontSize = 15,
            FontAttributes = FontAttributes.Bold,
            TextColor = Ink
        });
        stack.Children.Add(new Label
        {
            Text = $"{b.Original}　·　{b.Author}",
            FontSize = 11.5,
            TextColor = Faint
        });
        stack.Children.Add(Body(b.Why, 13, Soft));

        var lookup = new Button
        {
            Text = "查电子版 / 馆藏",
            FontSize = 13,
            HeightRequest = 36,
            CornerRadius = 18,
            Padding = new Thickness(18, 0),
            BackgroundColor = Colors.White,
            TextColor = Accent,
            BorderColor = Color.FromArgb("#F3D2B8"),
            BorderWidth = 1,
            HorizontalOptions = LayoutOptions.Start,
            Margin = new Thickness(0, 4, 0, 0)
        };
        lookup.Clicked += async (_, _) => await OpenAsync(b.SearchUrl);
        stack.Children.Add(lookup);

        return new Frame
        {
            Padding = new Thickness(16, 13),
            CornerRadius = 18,
            HasShadow = false,
            BackgroundColor = Colors.White,
            BorderColor = CardBorder,
            Content = stack
        };
    }

    private static Frame RulesCard()
    {
        var stack = new VerticalStackLayout { Spacing = 7 };
        stack.Children.Add(new Label
        {
            Text = "⚠️ 关于网盘里的“绘本合集”",
            FontSize = 13.5,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#B23A48")
        });

        foreach (var rule in ReadingCatalog.Rules)
        {
            var row = new HorizontalStackLayout { Spacing = 7 };
            row.Children.Add(new Label
            {
                Text = "•",
                FontSize = 13,
                TextColor = Color.FromArgb("#B23A48"),
                VerticalOptions = LayoutOptions.Start
            });
            row.Children.Add(new Label
            {
                Text = rule,
                FontSize = 12,
                TextColor = Color.FromArgb("#8A6B70"),
                LineHeight = 1.65
            });
            stack.Children.Add(row);
        }

        return new Frame
        {
            Padding = new Thickness(16, 14),
            CornerRadius = 20,
            HasShadow = false,
            BackgroundColor = Color.FromArgb("#FFF1F0"),
            BorderColor = Color.FromArgb("#FFCDD2"),
            Content = stack
        };
    }

    private static Label Body(string text, double size, Color color) => new()
    {
        Text = text,
        FontSize = size,
        TextColor = color,
        LineHeight = 1.65
    };

    private static async Task OpenAsync(string url)
    {
        try
        {
            await Microsoft.Maui.ApplicationModel.Browser.Default.OpenAsync(url);
        }
        catch
        {
            // 没有默认浏览器（极少数定制系统）时不要崩，用户自己复制域名也能进
        }
    }
}
