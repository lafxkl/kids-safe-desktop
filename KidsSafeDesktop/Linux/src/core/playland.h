#pragma once

#include <QObject>
#include <QString>
#include <QUrl>

/**
 * 网页小游戏（Playland）资源定位。
 *
 * Windows / Android 端把这 15 款 HTML5 游戏放在 `Resources/Raw/Playland/`，
 * 运行时释放到可写目录再用 WebView 打开。Linux 端沿用同一份资源，但不需要
 * 释放：直接安装到 `${datadir}/kidssafe/playland`，同时打进 qrc 兜底，
 * 由本类算出实际可用的 `index.html` 地址。
 *
 * 为什么不写死一个路径：
 *   - 开发时从 build 目录直接跑，二进制不在 /usr/bin，`../share` 找不到；
 *   - 有的发行版装到 /usr/local（自己编译），有的装到 /usr/share（打包）；
 *   - 万一系统里没装运行时资源，qrc 里那份还能用。
 * 所以这里按「环境变量 → 二进制旁边的 ../share → 常见前缀 → 源码树 → qrc」
 * 依次探测，第一个存在 index.html 的目录胜出。
 *
 * 注意：没编译 QtWebEngine 时 `available` 恒为 false，`hallUrl` 为空。
 * 前端据此把入口显示成「组件未安装」，而不是给一个点进去白屏的卡片。
 */
class Playland : public QObject
{
    Q_OBJECT

    /// 网页游戏能不能用：编译期带 WebEngine，且找得到 index.html。
    Q_PROPERTY(bool available READ available CONSTANT)
    /// 游戏大厅地址（file:// 或 qrc:）。
    Q_PROPERTY(QUrl hallUrl READ hallUrl CONSTANT)
    /// 实际使用的目录，仅用于日志/自检展示，可能是 qrc:/playland。
    Q_PROPERTY(QString baseDir READ baseDir CONSTANT)
    /// 编译期是否链接了 WebEngine（与运行时是否能加载网页是两件事）。
    Q_PROPERTY(bool webEngineBuilt READ webEngineBuilt CONSTANT)

public:
    explicit Playland(QObject *parent = nullptr);

    bool available() const { return m_hall.isValid() && !m_hall.isEmpty(); }
    QUrl hallUrl() const { return m_hall; }
    QString baseDir() const { return m_baseDir; }
    bool webEngineBuilt() const;

    /// 相对路径 → 绝对地址，例如 "pages/gomoku.html"。
    /// 大厅是 HTML 自己跳转的，这个函数留给以后「直接进某一款」用。
    Q_INVOKABLE QUrl pageUrl(const QString &relative) const;

private:
    QUrl m_hall;
    QString m_baseDir;
};
