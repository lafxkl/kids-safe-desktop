using Microsoft.Maui.Controls;

namespace KidsSafeDesktop.Helpers;

/// <summary>
/// 在 WebView 里执行 JS 并取回结果。
///
/// MAUI 的 <see cref="WebView"/> 没有跨平台的 JS 执行接口，只能落到平台实现：
/// Windows 是 WebView2.ExecuteScriptAsync，Android 是 WebView.EvaluateJavascript。
/// 两者返回值都是 **JSON 编码过的**（字符串带引号、null 就是 null），
/// 所以这里统一解一层再给调用方。
/// </summary>
public static class WebViewBridge
{
    /// <summary>执行一段 JS，不关心返回值。</summary>
    public static void FireAndForget(WebView view, string script)
        => _ = EvaluateAsync(view, script);

    /// <summary>执行一段 JS，返回解开 JSON 编码后的字符串结果；失败返回 null。</summary>
    public static async Task<string?> EvaluateAsync(WebView view, string script)
    {
        try
        {
#if WINDOWS
            if (view.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.WebView2 wv2 &&
                wv2.CoreWebView2 is not null)
            {
                var raw = await wv2.ExecuteScriptAsync(script);
                return Unwrap(raw);
            }
#elif ANDROID
            if (view.Handler?.PlatformView is Android.Webkit.WebView awv)
            {
                var tcs = new TaskCompletionSource<string?>();
                awv.EvaluateJavascript(script, new JsCallback(v => tcs.TrySetResult(Unwrap(v))));
                return await tcs.Task;
            }
#elif IOS
            // WKWebView 直接返回对象（不是 Android/WebView2 那样的 JSON 文本），
            // 所以不解包；字符串就是字符串，null 用 NSNull 表示。
            if (view.Handler?.PlatformView is WebKit.WKWebView wk)
            {
                var result = await wk.EvaluateJavaScriptAsync(script);
                if (result is null || result is Foundation.NSNull) return null;
                return result.ToString();
            }
#endif
            await Task.CompletedTask;
            return null;
        }
        catch (Exception ex)
        {
            AppLog.W("网页桥", "执行 JS 失败：" + ex.Message);
            return null;
        }
    }

    /// <summary>WebView2 / Android 都返回 JSON 文本，解一层拿到真正的字符串。</summary>
    private static string? Unwrap(string? raw)
    {
        if (string.IsNullOrEmpty(raw) || raw == "null") return null;
        if (raw.Length >= 2 && raw[0] == '"')
        {
            try { return System.Text.Json.JsonSerializer.Deserialize<string>(raw); }
            catch { return raw.Trim('"'); }
        }
        return raw;
    }

#if ANDROID
    private sealed class JsCallback : Java.Lang.Object, Android.Webkit.IValueCallback
    {
        private readonly Action<string?> _onValue;
        public JsCallback(Action<string?> onValue) => _onValue = onValue;
        public void OnReceiveValue(Java.Lang.Object? value)
            => _onValue(value?.ToString());
    }
#endif
}
