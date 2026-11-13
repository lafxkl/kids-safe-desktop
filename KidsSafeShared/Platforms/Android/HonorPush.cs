#if ANDROID
using Android.Content;
using Android.Runtime;
using Java.Interop;
using KidsSafeDesktop.Helpers;

namespace KidsSafeDesktop;

/// <summary>
/// 荣耀推送（Push Kit）客户端接入。
///
/// 分层说明：
///   C#（本文件）→ JNI → Java 桥（Platforms/Android/Java/com/kidssafe/push/HonorPushBridge.java）
///   → 荣耀 SDK（com.hihonor.mcs:push，本地 AAR）。
///
/// 为什么走 Java 桥而不是直接绑定 AAR：荣耀推送没有官方 C# 绑定，
/// 用十几行 Java 做桥接，比给一个混淆过的商业 SDK 生成绑定（还要写 Metadata 修修补补）
/// 成本低、升级也不容易断。
///
/// 结果不回传（避免跨线程 JNI 回调），统一写 SharedPreferences("honor_push")，
/// 这里用纯 C# 读，任何一步失败都返回 null，绝不影响桌面启动。
/// </summary>
public static class HonorPush
{
    private const string BridgeClass = "com/kidssafe/push/HonorPushBridge";
    private const string PrefsName = "honor_push";

    private static bool _started;

    /// <summary>本机是否支持荣耀推送（非荣耀设备为 false，属于正常情况）。</summary>
    public static bool IsSupported => Read("support") == "true";

    /// <summary>推送 token；未注册成功时为 null。服务端下发消息需要它。</summary>
    public static string? Token => Read("token");

    /// <summary>最近一次失败原因（"token:错误码:描述"），排查用。</summary>
    public static string? LastError => Read("diag");

    /// <summary>最近一条透传消息原文。</summary>
    public static string? LastMessage => Read("last_msg");

    /// <summary>
    /// 在 Activity 创建后调用一次。内部起后台线程，不阻塞启动；
    /// 任何异常（含 ClassNotFound——比如有人删了 AAR）都只写日志。
    /// </summary>
    public static void Start()
    {
        if (_started) return;
        _started = true;

        _ = Task.Run(() =>
        {
            try
            {
                var ctx = global::Android.App.Application.Context;

                var cls = JNIEnv.FindClass(BridgeClass);
                var mid = JNIEnv.GetStaticMethodID(cls, "start",
                    "(Landroid/content/Context;)Ljava/lang/String;");
                var raw = JNIEnv.CallStaticObjectMethod(cls, mid, new JValue(ctx));
                var status = raw == IntPtr.Zero
                    ? "(null)"
                    : JNIEnv.GetString(raw, JniHandleOwnership.TransferLocalRef);

                // token 是异步回调写进 prefs 的，这里只记录「是否已发起」
                NavFx.Log($"[推送] 荣耀 start={status}");
            }
            catch (Exception ex)
            {
                NavFx.Log($"[推送] 荣耀初始化失败(已忽略) {ex.GetType().Name}: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// 重新申请一次 token。用于「刚在荣耀控制台补完证书指纹」后立即验证，
    /// 不必杀掉进程重开应用。
    /// 注意荣耀侧有频控（10214 HTTP 操作过于频繁），调用方需自行限制频率。
    /// </summary>
    public static void Restart()
    {
        _started = false;
        Start();
    }

    /// <summary>
    /// 解绑推送 token（家长关闭推送 / 退出登录时调用）。
    /// 不调用也不影响使用，只是设备会继续收到该 app 的消息。
    /// </summary>
    public static void Stop()
    {
        try
        {
            var cls = JNIEnv.FindClass(BridgeClass);
            var mid = JNIEnv.GetStaticMethodID(cls, "deleteToken", "(Landroid/content/Context;)V");
            JNIEnv.CallStaticVoidMethod(cls, mid,
                new JValue(global::Android.App.Application.Context));
        }
        catch (Exception ex)
        {
            NavFx.Log($"[推送] 荣耀解绑失败(已忽略) {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static string? Read(string key)
    {
        try
        {
            var ctx = global::Android.App.Application.Context;
            var prefs = ctx.GetSharedPreferences(PrefsName, FileCreationMode.Private);
            if (prefs is null) return null;

            // 兼容历史上被写成 boolean 的键（旧版本 bridge 的 bug），读不到再按 boolean 试一次
            string? value;
            try { value = prefs.GetString(key, null); }
            catch { value = null; }

            if (value is null)
            {
                try
                {
                    if (prefs.All is not null && prefs.All.ContainsKey(key))
                        value = prefs.GetBoolean(key, false) ? "true" : "false";
                }
                catch { /* 类型还是不对就放弃 */ }
            }

            return string.IsNullOrEmpty(value) ? null : value;
        }
        catch
        {
            return null;
        }
    }
}
#endif
