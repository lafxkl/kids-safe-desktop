#if ANDROID
using Android.Runtime;
using Java.Interop;

namespace KidsSafeDesktop;

/// <summary>
/// 读取本安装包的包名与签名指纹（安卓）。
///
/// 荣耀推送 10202 = 「证书指纹或包名与平台登记不一致」，成因只有这两种。
/// 把这两个值直接显示在诊断页，用户就能和开发者服务平台逐字核对。
/// 结果缓存，失败返回 null，绝不抛异常。
/// </summary>
public static class AppSignature
{
    private static bool _loaded;
    private static string? _package;
    private static string? _sha256;

    /// <summary>应用包名（如 com.companyname.kidssafedesktop）。</summary>
    public static string PackageName
    {
        get
        {
            Ensure();
            return _package ?? global::Android.App.Application.Context.PackageName ?? "(未知)";
        }
    }

    /// <summary>签名证书 SHA-256，带冒号（AA:BB:…）；取不到为 null。</summary>
    public static string? Sha256
    {
        get
        {
            Ensure();
            return _sha256;
        }
    }

    /// <summary>去掉冒号的大写形式，荣耀平台填这个。</summary>
    public static string? Sha256Plain => _sha256?.Replace(":", "").ToUpperInvariant();

    /// <summary>清单里声明的荣耀推送 App ID（meta-data: com.hihonor.push.app_id）。</summary>
    public static string? HonorAppId => ReadMeta("com.hihonor.push.app_id");

    /// <summary>清单里声明的荣耀推送 SDK 版本（由 AAR 合并进来，排查用）。</summary>
    public static string? HonorSdkVersion => ReadMeta("com.hihonor.push.sdk_version");

    /// <summary>
    /// 读一个 meta-data，兼容「值被 aapt 编成整型」的情况。
    ///
    /// 坑：`android:value="104600830"` 全是数字时，aapt2 会把它编成 int 而不是 String
    /// （反编译 APK 清单能看到 `value=104600830` 没有引号）。此时 Bundle.GetString
    /// 取到的是 null —— v1.10 诊断页因此误显示「App ID：(清单未配置)」。
    /// 荣耀 SDK 自己用 String.valueOf(bundle.get(...)) 读，所以推送链路不受影响，
    /// 只是我们这层显示必须按 Object 读再转字符串。
    /// </summary>
    private static string? ReadMeta(string key)
    {
        try
        {
            var ctx = global::Android.App.Application.Context;
            var ai = ctx.PackageManager?.GetApplicationInfo(
                ctx.PackageName, global::Android.Content.PM.PackageInfoFlags.MetaData);
            var bundle = ai?.MetaData;
            if (bundle is null) return null;

            try
            {
                var s = bundle.GetString(key);
                if (!string.IsNullOrWhiteSpace(s)) return s;
            }
            catch
            {
                // 类型不是 String 时 GetString 会抛/返回 null，继续走下面的 Object 分支
            }

            var obj = bundle.Get(key);
            if (obj is null) return null;
            if (obj is Java.Lang.Integer ji) return ji.IntValue().ToString();

            var text = obj.ToString();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        catch
        {
            return null;
        }
    }

    private static void Ensure()
    {
        if (_loaded) return;
        _loaded = true;

        try
        {
            var ctx = global::Android.App.Application.Context;
            var cls = JNIEnv.FindClass("com/kidssafe/push/AppSignature");
            var mid = JNIEnv.GetStaticMethodID(cls, "get", "(Landroid/content/Context;)Ljava/lang/String;");
            var raw = JNIEnv.CallStaticObjectMethod(cls, mid, new JValue(ctx));
            if (raw == IntPtr.Zero) return;

            var text = JNIEnv.GetString(raw, JniHandleOwnership.TransferLocalRef) ?? "";
            var parts = text.Split('|', 2);
            if (parts.Length > 0 && !string.IsNullOrWhiteSpace(parts[0]))
                _package = parts[0];
            if (parts.Length > 1 && !parts[1].StartsWith("error", StringComparison.OrdinalIgnoreCase)
                                 && !parts[1].StartsWith("(", StringComparison.Ordinal))
                _sha256 = parts[1];
        }
        catch
        {
            // 取不到就算了，诊断页会显示「(读取失败)」
        }
    }
}
#endif
