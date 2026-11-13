#if ANDROID
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.Graphics.Drawables;
using KidsSafeDesktop.Models;
using KidsSafeDesktop.Services;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;

namespace KidsSafeDesktop.Platforms.Android;

/// <summary>
/// 枚举 Android 上可启动的已安装应用，供家长加入白名单。
/// </summary>
public class AndroidAppCatalogService : IAppCatalogService
{
    public Task<List<AppItem>> GetInstalledAppsAsync()
    {
        var ctx = Platform.CurrentActivity ?? global::Android.App.Application.Context;
        var pm = ctx.PackageManager!;
        var intent = new Intent(Intent.ActionMain);
        intent.AddCategory(Intent.CategoryLauncher);

        var resolve = pm.QueryIntentActivities(intent, PackageInfoFlags.MatchAll);
        var list = new List<AppItem>();
        foreach (var ri in resolve)
        {
            var pkg = ri.ActivityInfo?.PackageName;
            if (string.IsNullOrEmpty(pkg) || pkg == ctx.PackageName)
                continue;

            var label = ri.LoadLabel(pm) ?? pkg;
            list.Add(new AppItem
            {
                Name = label,
                PackageName = pkg,
                Emoji = "📱",
                AccentColor = ColorFromHash(pkg)
            });
        }

        list.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCulture));
        return Task.FromResult(list);
    }

    /// <summary>
    /// 按包名获取应用真实启动图标，转成 MAUI 可用的 ImageSource。
    /// 自适应图标(AdaptiveIconDrawable)也能正确栅格化。
    /// </summary>
    public Task<ImageSource?> GetAppIconAsync(string packageName)
    {
        if (string.IsNullOrEmpty(packageName)) return Task.FromResult((ImageSource?)null);

        try
        {
            var ctx = Platform.CurrentActivity ?? global::Android.App.Application.Context;
            var pm = ctx.PackageManager!;
            var drawable = pm.GetApplicationIcon(packageName);
            if (drawable is null) return Task.FromResult((ImageSource?)null);

            using var bitmap = DrawableToBitmap(drawable);
            if (bitmap is null) return Task.FromResult((ImageSource?)null);

            using var ms = new MemoryStream();
            bitmap.Compress(Bitmap.CompressFormat.Png, 100, ms);
            var bytes = ms.ToArray();
            return Task.FromResult<ImageSource?>(ImageSource.FromStream(() => new MemoryStream(bytes)));
        }
        catch
        {
            // 任何异常（包不存在/无图标）都退化为 emoji
            return Task.FromResult((ImageSource?)null);
        }
    }

    private static Bitmap? DrawableToBitmap(Drawable drawable)
    {
        int w = drawable.IntrinsicWidth;
        int h = drawable.IntrinsicHeight;
        if (w <= 0) w = 192;
        if (h <= 0) h = 192;

        // 限制最大尺寸，避免超大自适应图标占用过多内存
        const int max = 256;
        if (w > max || h > max)
        {
            var scale = (double)max / Math.Max(w, h);
            w = (int)(w * scale);
            h = (int)(h * scale);
        }

        var bitmap = Bitmap.CreateBitmap(w, h, Bitmap.Config.Argb8888!);
        using var canvas = new Canvas(bitmap);
        drawable.SetBounds(0, 0, w, h);
        drawable.Draw(canvas);
        return bitmap;
    }

    private static string ColorFromHash(string s)
    {
        int h = 0;
        foreach (var c in s) h = (h * 31 + c) & 0xffffff;
        return "#" + h.ToString("X6").PadLeft(6, '0');
    }
}
#endif
