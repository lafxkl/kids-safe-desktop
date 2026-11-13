#if ANDROID
using Android.Content;
using Android.Content.PM;
using KidsSafeDesktop.Services;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Essentials;

namespace KidsSafeDesktop.Platforms.Android;

/// <summary>
/// 安卓：优先启动喜马拉雅儿童 App（儿童版包名优先，其次主端），未安装则打开儿童网页。
/// </summary>
public class XimalayaService : IXimalayaService
{
    public async Task OpenAsync(string categoryKey)
    {
        var uri = BuildUri(categoryKey);
        var ctx = Platform.CurrentActivity ?? global::Android.App.Application.Context;

        // 优先尝试启动 App（儿童版 -> 主端）
        foreach (var pkg in new[] { "com.ximalaya.ting.android.child", "com.ximalaya.ting.android" })
        {
            try
            {
                var intent = ctx.PackageManager?.GetLaunchIntentForPackage(pkg);
                if (intent is not null)
                {
                    intent.AddFlags(ActivityFlags.NewTask);
                    ctx.StartActivity(intent);
                    return;
                }
            }
            catch { /* 继续尝试下一个 */ }
        }

        // 未安装 App，退回到网页
        try { await Browser.Default.OpenAsync(uri, BrowserLaunchMode.SystemPreferred); }
        catch { /* 忽略：环境无法打开浏览器 */ }
    }

    /// <summary>按分类拼出喜马拉雅儿童对应页面（统一进入官方儿童频道）。</summary>
    private static Uri BuildUri(string categoryKey)
    {
        // 官方儿童频道首页；后续如有分类深链可在此扩展。
        return new Uri("https://www.ximalaya.com/child/");
    }
}
#endif
