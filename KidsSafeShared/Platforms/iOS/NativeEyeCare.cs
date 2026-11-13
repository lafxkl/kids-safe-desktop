namespace KidsSafeDesktop.Helpers;

/// <summary>
/// 其他平台（iOS / MacCatalyst）的护眼遮罩占位实现。
/// 当前项目仅面向 Android 与 Windows 构建；此处保留接口以便将来扩展。
/// </summary>
#if !ANDROID && !WINDOWS
public static class NativeEyeCare
{
    public static void Apply(object? windowObj, bool on, double opacity)
    {
        // 暂无平台实现，静默降级
    }
}
#endif
