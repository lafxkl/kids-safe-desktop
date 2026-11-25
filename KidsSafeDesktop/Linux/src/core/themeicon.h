#pragma once

#include <QQuickImageProvider>

/**
 * 把系统图标主题里的应用图标喂给 QML：`image://theme/<图标名>`。
 *
 * 为什么需要它：Windows 版的磁贴显示的是真实应用图标（快捷方式里的
 * 图标资源），而 QML 的 `Image` 不认主题图标名——直接写 `source: "firefox"`
 * 会被当成相对路径的文件，什么也加载不出来。Qt 又没有内置的
 * `image://icon/…` provider（那是 KDE 的 Kirigami.Icon 才有的东西，
 * 而 Kirigami.Icon 会去查 KIconLoader / ksycoca，在精简发行版上不一定可用）。
 *
 * 这里用 QIcon::fromTheme 直接查图标主题（hicolor / breeze 都在标准路径下），
 * 查不到再按「绝对路径」当图片文件读。两者都失败时返回空图，
 * QML 侧的 Image 会进入 Error 状态，磁贴自动退回「色块 + 名称首字」，
 * 也就是 AppTile.qml 里那套永远渲染得出来的兜底。
 *
 * id 形如 `/firefox?size=64`；带 `?` 之后的查询串只用于调试，
 * 真正的尺寸由 Image 的 sourceSize 通过 requestedSize 传进来。
 */
class ThemeIconProvider : public QQuickImageProvider
{
public:
    ThemeIconProvider();

    QImage requestImage(const QString &id, QSize *size, const QSize &requestedSize) override;
};
