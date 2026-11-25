using Microsoft.Maui.Graphics;
using Microsoft.Maui.Graphics.Platform;

namespace KidsSafeDesktop.Helpers;

/// <summary>
/// 把相册原图缩小后再交给网页。
///
/// 为什么要在宿主侧先缩一次：网页里的 compressImage 会把图压到 320px，但那是**在 JS 里**做的，
/// 图得先完整穿过 JS 桥（base64 字符串）。手机照片 4-8 MB，base64 后 6-11 MB 的字符串
/// 一次性塞进 EvaluateJavascript，安卓上会卡顿甚至 OOM。
/// 这里先缩到 480px，过桥的字符串就只剩几十 KB。
///
/// 任何一步失败都返回 null，调用方退回原图 —— 缩图是优化，不是功能依赖。
/// </summary>
public static class ImageShrinker
{
    public static byte[]? TryShrink(byte[] original, int maxSize)
    {
        try
        {
            using var input = new MemoryStream(original, writable: false);
            using var image = PlatformImage.FromStream(input);
            if (image is null) return null;

            // 本来就比目标小，没必要重编码（还可能有损）
            if (image.Width <= maxSize && image.Height <= maxSize) return null;

            using var small = image.Downsize(maxSize, disposeOriginal: false);

            // 有透明通道的素材（贴纸、图标）必须用 PNG，转 JPEG 会变黑底
            using var output = small.AsStream(ImageFormat.Png, 1f);
            if (output is null) return null;

            using var buffer = new MemoryStream();
            output.CopyTo(buffer);
            var result = buffer.ToArray();

            // 缩完反而更大（小图被 PNG 重编码），那就用原图
            return result.Length > 0 && result.Length < original.Length ? result : null;
        }
        catch
        {
            return null;
        }
    }
}
