using KidsSafeDesktop.Models;

namespace KidsSafeDesktop.Pages;

/// <summary>心理小课堂列表（给孩子 / 给家长两种视角）。</summary>
public partial class CareLessonPage : ContentPage
{
    private readonly string _audience;

    public CareLessonPage(string audience = "kids")
    {
        InitializeComponent();
        NavigationPage.SetHasNavigationBar(this, false);
        _audience = audience;

        var forParents = audience == "parents";
        TitleLabel.Text = forParents ? "👨‍👩‍👧 给家长看的" : "📚 心理小课堂";
        BackgroundColor = forParents
            ? Color.FromArgb("#FAF7FF")
            : Color.FromArgb("#F2FAF4");

        var lessons = forParents ? CareCatalog.ParentLessons : CareCatalog.KidsLessons;
        LessonList.ItemsSource = lessons;
        CountLabel.Text = $"{lessons.Count} 节";

        IntroLabel.Text = forParents
            ? "这一组是写给爸爸妈妈、爷爷奶奶看的：怎么跟孩子解释外出、15 分钟电话怎么打、哪些信号需要专业帮助；也有几节讲日常怎么说话、怎么放手、怎么定目标。"
            : "这些课讲的是你心里可能遇到过、但没人跟你好好说过的事。不用一次看完，挑一节现在最想看的。";
    }

    private async void OnLessonTapped(object sender, TappedEventArgs e)
    {
        if (sender is not Frame frame || frame.BindingContext is not CareLesson lesson) return;
        await Navigation.PushAsync(new CareLessonDetailPage(lesson));
    }

    private async void OnBack(object sender, EventArgs e) => await Navigation.PopAsync();
}
