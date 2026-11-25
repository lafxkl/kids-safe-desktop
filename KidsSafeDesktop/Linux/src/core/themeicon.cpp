#include "themeicon.h"

#include <QDir>
#include <QFileInfo>
#include <QGuiApplication>
#include <QIcon>
#include <QImage>
#include <QPainter>
#include <QPixmap>
#include <QScreen>
#include <QUrl>

ThemeIconProvider::ThemeIconProvider()
    : QQuickImageProvider(QQuickImageProvider::Image)
{
}

QImage ThemeIconProvider::requestImage(const QString &id, QSize *size, const QSize &requestedSize)
{
    // QML 会把图标名（可能含空格、斜杠、中文）percent-encode 后塞进 URL。
    // 用 QUrl 解一遍，path 去掉最前面的 '/' 就是原始图标名。
    const QUrl url(QStringLiteral("image://theme/") + id);
    QString name = url.path();
    if (name.startsWith(QLatin1Char('/'))) {
        name.remove(0, 1);
    }
    name = name.trimmed();

    if (size) {
        *size = QSize(0, 0);
    }
    if (name.isEmpty()) {
        return {};
    }

    // 请求尺寸是设备无关像素；按屏幕缩放比放大后再交给 QIcon 渲染，
    // 否则 2x 屏上的图标会被放大成马赛克。
    // 注意 QGuiApplication 本身不是 QPaintDevice，没有 devicePixelRatio()，
    // 要从 primaryScreen() 上取。
    QSize target = requestedSize.isValid() && !requestedSize.isEmpty() ? requestedSize : QSize(64, 64);
    target = target.expandedTo(QSize(16, 16));

    qreal dpr = 1.0;
    if (const QScreen *screen = QGuiApplication::primaryScreen()) {
        dpr = screen->devicePixelRatio();
    }
    const QSize physical(qRound(target.width() * dpr), qRound(target.height() * dpr));

    QIcon icon;
    // 两种来源：绝对路径（.desktop 里直接写 /usr/share/pixmaps/xxx.png）与主题名。
    // '~' 开头的要自己展开，QIcon 不认 shell 的波浪号。
    if (name.startsWith(QLatin1Char('~'))) {
        const QString expanded = QDir::homePath() + name.mid(1);
        if (QFileInfo::exists(expanded)) {
            icon = QIcon(expanded);
        }
    } else if (name.startsWith(QLatin1Char('/'))) {
        if (QFileInfo::exists(name)) {
            icon = QIcon(name);
        }
    } else {
        icon = QIcon::fromTheme(name);
    }

    if (icon.isNull()) {
        // 主题里没有这个名字：交给 QML 走首字兜底，不要画一个空方块上去。
        // 注意不能拿 availableSizes() 判空——纯 SVG 图标主题（breeze 就是）
        // 常常报不出尺寸表，但它照样渲染得出来。
        return {};
    }

    QPixmap pixmap = icon.pixmap(physical, dpr);
    if (pixmap.isNull()) {
        return {};
    }

    QImage image = pixmap.toImage().convertToFormat(QImage::Format_ARGB32_Premultiplied);
    image.setDevicePixelRatio(dpr);

    if (size) {
        *size = QSize(qRound(image.width() / dpr), qRound(image.height() / dpr));
    }
    return image;
}
