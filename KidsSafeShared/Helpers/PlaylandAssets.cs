namespace KidsSafeDesktop.Helpers;

/// <summary>
/// 「游戏乐园」网页游戏资源管理器。
/// 游戏以 MauiAsset 打包在 Resources/Raw/Playland 下，运行时释放到应用数据目录，
/// 再用 file:// URL 交给 WebView 加载——这样各页面之间的相对路径引用（如 ../index.html、
/// assets/images/xxx.png）全部保持原样可用。
/// </summary>
public static class PlaylandAssets
{
    /// <summary>MauiAsset 逻辑名前缀（对应 Resources/Raw/Playland 目录）。</summary>
    private const string AssetPrefix = "Playland/";

    /// <summary>释放到应用数据目录后的文件夹名。</summary>
    private const string Folder = "playland";

    /// <summary>资源清单版本号：改动打包的游戏文件后请 +1，用于触发重新释放。</summary>
    private const string Version = "3";

    /// <summary>打包资源清单（相对 Playland/ 的路径）。</summary>
    private static readonly string[] Files =
    {
        "index.html",

        // 十三款原有网页游戏
        "pages/plane_war.html",         // 🚀 雷霆战机
        "pages/gomoku.html",            // 🎯 欢乐五子棋
        "pages/snake_kids.html",        // 🐍 贪吃蛇大冒险
        "pages/gem_match.html",         // 💎 萌趣宝石消消乐
        "pages/animal_chess.html",      // 🦁 森林斗兽棋
        "pages/chinese_chess.html",     // ♟️ 中国象棋
        "pages/penguin_hit.html",       // 🐧 冰雪打企鹅
        "pages/make_ten.html",          // 🔟 萌趣凑十消消乐
        "pages/math_pilot.html",        // 🛩️ 算术小飞行员
        "pages/poem_puzzle.html",       // 📜 点字成诗大闯关
        "pages/radical_merge.html",     // 🧩 汉字合体消消乐
        "pages/word_balloon.html",      // 🎈 听音识字打气球
        "pages/pictograph_match.html",  // 🖼️ 象形字翻翻看

        // 两款新增网页游戏
        "pages/car_rush.html",          // 🚗 小汽车大冒险
        "pages/light_puzzle.html",      // 💎 光线宝石反射解谜

        // 手机端适配样式表（所有页面共用；漏了它网页在手机上会横向溢出）
        "pages/assets/mobile.css",

        // 雷霆战机用到的素材图（只列页面实际引用的切图；原始 jpg 底图不进包）
        "pages/assets/images/boss.png",
        "pages/assets/images/enemy.png",
        "pages/assets/images/hero_aurora.png",
        "pages/assets/images/hero_phantom.png",
        "pages/assets/images/hero_titan.png",
        "pages/assets/images/space_bg.jpg"
    };

    /// <summary>释放后的根目录。</summary>
    public static string RootPath => Path.Combine(FileSystem.AppDataDirectory, Folder);

    /// <summary>大厅页面路径。</summary>
    public static string IndexPath => Path.Combine(RootPath, "index.html");

    private static string VersionFilePath => Path.Combine(RootPath, ".assetversion");

    /// <summary>
    /// 确保资源已释放到可访问目录，返回大厅页面的 file:// URL。
    /// 已有相同版本时直接复用，避免每次启动重复 IO。
    /// </summary>
    public static async Task<string> EnsureExtractedAsync()
    {
        var root = RootPath;
        Directory.CreateDirectory(root);

        if (!IsUpToDate())
        {
            foreach (var rel in Files)
            {
                var dest = Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar));
                var dir = Path.GetDirectoryName(dest);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                try
                {
                    await using var src = await FileSystem.OpenAppPackageFileAsync(AssetPrefix + rel);
                    await using var dst = File.Create(dest);
                    await src.CopyToAsync(dst);
                }
                catch
                {
                    // 单个文件缺失不影响其余游戏
                }
            }

            try { await File.WriteAllTextAsync(VersionFilePath, Version); }
            catch { /* 忽略：无法写标记文件时下次会重新释放 */ }
        }

        return new Uri(IndexPath).AbsoluteUri;
    }

    private static bool IsUpToDate()
    {
        try
        {
            return File.Exists(IndexPath)
                && File.Exists(VersionFilePath)
                && File.ReadAllText(VersionFilePath).Trim() == Version;
        }
        catch
        {
            return false;
        }
    }
}
