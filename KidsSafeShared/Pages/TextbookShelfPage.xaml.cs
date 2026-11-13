using System.Collections.ObjectModel;
using KidsSafeDesktop.Models;
using KidsSafeDesktop.Services;
using Microsoft.Maui.Controls;

namespace KidsSafeDesktop.Pages;

/// <summary>
/// 电子课本书架：选版本、选年级，点一本进入翻页阅读。
///
/// 课本原件（每本十几兆的 PDF）留在服务端，这里只拿目录和封面；
/// 真正翻页时按「一页一张 jpg」取图，由 <see cref="ITextbookService"/> 负责缓存。
/// </summary>
public partial class TextbookShelfPage : ContentPage
{
    private static readonly Color BrandColor = Color.FromArgb("#00695C");
    private static readonly Color BrandSoft = Color.FromArgb("#B2DFDB");
    private static readonly Color ChipOn = Color.FromArgb("#00897B");

    private readonly ITextbookService _books;
    private readonly ObservableCollection<TextbookTile> _tiles = new();
    private readonly List<Button> _subjectChips = new();
    private readonly List<Button> _gradeChips = new();

    private TextbookShelf? _shelf;
    private string _subject = string.Empty;    // 当前科目：chinese / math
    private TextbookEdition? _edition;
    private int _grade;          // 0 = 全部
    private bool _loading;

    public TextbookShelfPage()
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);

        _books = ServiceProvider.Get<ITextbookService>();
        BooksView.ItemsSource = _tiles;

        _ = LoadAsync();
    }

    private async Task LoadAsync(bool force = false)
    {
        if (_loading) return;
        _loading = true;
        SetBusy(true);

        try
        {
            _shelf = await _books.GetShelfAsync(force);
        }
        finally
        {
            _loading = false;
            SetBusy(false);
        }

        if (_shelf is null || _shelf.Editions.Count == 0)
        {
            ShowEmpty(
                _books.OnlineEnabled
                    ? "没有连上课本服务器。\n请家长确认服务端已启动、地址填写正确，再点下面的按钮重试。"
                    : "电子课本放在家长服务器上。\n请家长在「家长中心 → 在线内容」里打开开关并填写服务器地址。",
                showRetry: _books.OnlineEnabled);
            return;
        }

        // 老服务端没有 subjects 段时，按版本反推一个科目，页面照样能用
        if (_shelf.Subjects.Count == 0)
        {
            _shelf.Subjects.Add(new TextbookSubject { Id = string.Empty, Name = "课本", Icon = "📚" });
            foreach (var e in _shelf.Editions) e.Subject = string.Empty;
        }

        // 保留上次选中的科目与版本，避免刷新后又跳回第一个
        var keepSubject = _subject;
        _subject = _shelf.Subjects.Any(x => x.Id == keepSubject) ? keepSubject : _shelf.Subjects[0].Id;

        var keepEdition = _edition?.Id;
        var mine = EditionsOfSubject;
        _edition = mine.FirstOrDefault(e => e.Id == keepEdition) ?? mine.FirstOrDefault();

        BuildSubjectBar();
        BuildEditionBar();
        BuildGradeBar();
        RenderBooks();
    }

    /// <summary>当前科目下的版本。</summary>
    private List<TextbookEdition> EditionsOfSubject =>
        _shelf is null
            ? new List<TextbookEdition>()
            : _shelf.Editions
                .Where(e => string.Equals(e.Subject ?? string.Empty, _subject ?? string.Empty,
                                          StringComparison.OrdinalIgnoreCase))
                .ToList();

    // ── 科目切换 ───────────────────────────────────────────────
    private void BuildSubjectBar()
    {
        SubjectBar.Children.Clear();
        _subjectChips.Clear();
        if (_shelf is null) return;

        // 只有一个科目时不占地方
        SubjectBar.IsVisible = _shelf.Subjects.Count > 1;

        foreach (var sub in _shelf.Subjects)
        {
            var s2 = sub;
            var on = _subject == sub.Id;
            var books = _shelf.Editions
                .Where(e => string.Equals(e.Subject ?? string.Empty, sub.Id, StringComparison.OrdinalIgnoreCase))
                .SelectMany(e => e.Books)
                .ToList();
            var btn = new Button
            {
                Text = $"{sub.ChipText} {books.Count(b => !b.Legacy)} 册",
                FontSize = 15,
                FontAttributes = FontAttributes.Bold,
                HeightRequest = 40,
                CornerRadius = 20,
                Padding = new Thickness(18, 0),
                BackgroundColor = on ? BrandColor : Colors.White,
                TextColor = on ? Colors.White : BrandColor,
                BorderColor = BrandSoft,
                BorderWidth = on ? 0 : 1
            };
            btn.Clicked += (_, _) =>
            {
                if (_subject == s2.Id) return;
                _subject = s2.Id;
                _edition = EditionsOfSubject.FirstOrDefault();
                _grade = 0;
                BuildSubjectBar();
                BuildEditionBar();
                BuildGradeBar();
                RenderBooks();
            };
            _subjectChips.Add(btn);
            SubjectBar.Children.Add(btn);
        }
    }

    // ── 版本切换 ───────────────────────────────────────────────
    private void BuildEditionBar()
    {
        EditionBar.Children.Clear();
        if (_shelf is null) return;

        var eds = EditionsOfSubject;
        foreach (var ed in eds)
        {
            var ed2 = ed;
            var on = _edition?.Id == ed.Id;
            var btn = new Button
            {
                Text = $"{ed.ShortName} {ed.Books.Count(b => !b.Legacy)} 册",
                FontSize = 14,
                FontAttributes = FontAttributes.Bold,
                HeightRequest = 36,
                CornerRadius = 18,
                Padding = new Thickness(16, 0),
                BackgroundColor = on ? BrandColor : Colors.White,
                TextColor = on ? Colors.White : BrandColor,
                BorderColor = BrandSoft,
                BorderWidth = on ? 0 : 1
            };
            btn.Clicked += (_, _) =>
            {
                if (_edition?.Id == ed2.Id) return;
                _edition = ed2;
                _grade = 0;
                BuildEditionBar();
                BuildGradeBar();
                RenderBooks();
            };
            EditionBar.Children.Add(btn);
        }

        var all = eds.SelectMany(e => e.Books).ToList();
        var current = all.Count(b => !b.Legacy);
        CountLabel.Text = current == all.Count
            ? $"共 {all.Count} 册"
            : $"共 {all.Count} 册（现行 {current}）";
    }

    // ── 年级芯片 ───────────────────────────────────────────────
    private void BuildGradeBar()
    {
        GradeBar.Children.Clear();
        _gradeChips.Clear();
        if (_edition is null) return;

        AddGradeChip(0, "全部");

        var maxGrade = Math.Max(_edition.MaxGrade,
            _edition.Books.Count > 0 ? _edition.Books.Max(b => b.Grade) : 0);
        for (var g = 1; g <= maxGrade; g++)
        {
            if (_edition.Books.All(b => b.Grade != g)) continue;
            AddGradeChip(g, GradeText(g));
        }
    }

    private void AddGradeChip(int grade, string text)
    {
        var on = _grade == grade;
        var btn = new Button
        {
            Text = text,
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            HeightRequest = 32,
            CornerRadius = 16,
            Padding = new Thickness(14, 0),
            BackgroundColor = on ? ChipOn : Colors.White,
            TextColor = on ? Colors.White : BrandColor,
            BorderColor = BrandSoft,
            BorderWidth = on ? 0 : 1
        };
        btn.Clicked += (_, _) =>
        {
            if (_grade == grade) return;
            _grade = grade;
            foreach (var c in _gradeChips) StyleChip(c, c == btn);
            RenderBooks();
        };
        _gradeChips.Add(btn);
        GradeBar.Children.Add(btn);
    }

    private static void StyleChip(Button b, bool on)
    {
        b.BackgroundColor = on ? ChipOn : Colors.White;
        b.TextColor = on ? Colors.White : BrandColor;
        b.BorderWidth = on ? 0 : 1;
    }

    private static string GradeText(int grade) => grade switch
    {
        1 => "一年级",
        2 => "二年级",
        3 => "三年级",
        4 => "四年级",
        5 => "五年级",
        6 => "六年级",
        _ => $"{grade}年级"
    };

    // ── 书架 ───────────────────────────────────────────────────
    private void RenderBooks()
    {
        _tiles.Clear();
        if (_edition is null) return;

        var list = _edition.Books
            .Where(b => _grade == 0 || b.Grade == _grade)
            .OrderBy(b => b.Grade).ThenBy(b => b.Term).ThenBy(b => b.Legacy)
            .ToList();

        foreach (var b in list)
        {
            var url = _books.CoverUrl(b);
            _tiles.Add(new TextbookTile
            {
                Book = b,
                Cover = string.IsNullOrEmpty(url) ? null : ImageSource.FromUri(new Uri(url))
            });
        }

        var none = list.Count == 0;
        BooksView.IsVisible = !none;
        if (none) ShowEmpty("这个年级还没有课本，换一个年级看看～", showRetry: false);
        else EmptyPanel.IsVisible = false;
    }

    // ── 交互 ───────────────────────────────────────────────────
    private async void OnBookSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not TextbookTile tile) return;
        BooksView.SelectedItem = null;      // 消掉选中底色，下次还能点同一本

        if (tile.Book.Pages <= 0) return;
        await Navigation.PushAsync(new TextbookReaderPage(tile.Book));
    }

    private async void OnBack(object? sender, EventArgs e) => await Navigation.PopAsync();

    private async void OnRefresh(object? sender, EventArgs e)
    {
        EmptyPanel.IsVisible = false;
        BooksView.IsVisible = true;
        await LoadAsync(force: true);
    }

    // ── 状态 ───────────────────────────────────────────────────
    private void SetBusy(bool busy)
    {
        Busy.IsRunning = busy;
        Busy.IsVisible = busy;
        if (busy)
        {
            BooksView.IsVisible = false;
            EmptyPanel.IsVisible = false;
        }
    }

    private void ShowEmpty(string text, bool showRetry)
    {
        BooksView.IsVisible = false;
        EmptyPanel.IsVisible = true;
        EmptyText.Text = text;
        EmptyAction.IsVisible = showRetry;
    }
}
