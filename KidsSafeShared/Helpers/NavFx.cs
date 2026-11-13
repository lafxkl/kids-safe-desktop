using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;

namespace KidsSafeDesktop.Helpers;

/// <summary>
/// 导航辅助：所有「点一下进入子页面」都走这里。
/// 好处有两个：
/// 1) 页面构造 / 导航失败时不再被「fire-and-forget」静默吞掉（此前安卓上点了没反应、
///    连日志都没有，就是异常被吞了）；
/// 2) 失败统一写入 AppData 下的 nav.log，方便定位问题，并且给小朋友一个友好提示。
/// </summary>
public static class NavFx
{
    /// <summary>安全地推入一个页面（不抛异常、不中断调用方）。</summary>
    public static async void Push(this INavigation nav, Page page)
    {
        try
        {
            await nav.PushAsync(page);
        }
        catch (Exception ex)
        {
            await ReportAsync("进入页面失败", ex);
        }
    }

    /// <summary>安全地回退一层（根页面时什么都不做）。</summary>
    public static async void PopSafe(this INavigation nav)
    {
        try
        {
            if (nav.NavigationStack.Count > 1)
                await nav.PopAsync();
        }
        catch (Exception ex)
        {
            Log(ex);
        }
    }

    /// <summary>
    /// 按下磁贴 / 按钮后进入子页面。
    ///
    /// 顺序很关键：先等「按下去」的缩放动画画完，再构造并推入新页面。
    /// 之前是「动画 fire-and-forget、页面立刻构造」——XAML 展开会占满 UI 线程，
    /// 把动画卡在半途，观感就是「点下去先顿一下，页面才跳出来」。
    ///
    /// 用 <paramref name="factory"/> 而不是直接传 Page，就是为了保证构造函数
    /// 在动画结束后才执行；如果调用方先 new 好再传进来，这一步就白做了。
    /// </summary>
    public static async Task PushAfterPressAsync(this INavigation nav, View? pressed, Func<Page> factory,
                                                 int shrinkMs = 60, int restoreMs = 90)
    {
        try
        {
            if (pressed is not null)
                await pressed.PopAsync(shrinkMs, restoreMs);   // 短促一点，别让小朋友等

            await nav.PushAsync(factory());
        }
        catch (Exception ex)
        {
            await ReportAsync("进入页面失败", ex);
        }
    }

    /// <summary>
    /// 让出一小段时间给转场动画。
    ///
    /// 页面在 <c>OnAppearing</c> 里做重活（建 WebView、解包资源、读库）时，
    /// 会和正在跑的转场动画抢 UI 线程。先让出约 120ms 把转场放过去，
    /// 用户看到的是「页面滑进来了 → 内容再出现」，而不是「卡住 → 一起出现」。
    /// 首屏有加载态遮罩的页面用这个不会有空白感。
    /// </summary>
    public static Task AfterTransitionAsync(int ms = 120) => Task.Delay(ms);

    /// <summary>记录异常到 nav.log，并在 Debug 下弹出提示，便于定位。</summary>
    public static async Task ReportAsync(string title, Exception ex)
    {
        Log(ex);

#if DEBUG
        try
        {
            var page = Application.Current?.Windows.FirstOrDefault()?.Page;
            if (page is not null)
                await page.DisplayAlert(title, ex.GetType().Name + "\n" + ex.Message, "好的");
        }
        catch
        {
            // 提示失败就算了
        }
#else
        await Task.CompletedTask;
#endif
    }

    /// <summary>把异常追加写入 nav.log（位置见 <see cref="DiagLog"/>，安卓落在 Android/data 下）。</summary>
    public static void Log(Exception ex) => DiagLog.Append(LogFileName, ex.ToString());

    /// <summary>写入一行诊断信息（非异常），用于排查全屏/布局等无异常的问题。</summary>
    public static void Log(string message) => DiagLog.Append(LogFileName, message);

    /// <summary>统一日志文件名。</summary>
    public const string LogFileName = "nav.log";
}
