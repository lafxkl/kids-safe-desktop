#pragma once

#include <QObject>
#include <QString>
#include <QUrl>

/**
 * 「小游戏工坊」：网页编辑器的资源定位 + 素材/作品的文件桥。
 *
 * 编辑器本体是四端共用的一份 H5（桌面端 Resources/Raw/Maker/index.html），
 * Linux 端装到 `${datadir}/kidssafe/maker`，不释放、不复制，直接 file:// 打开。
 *
 * 为什么编辑器不放 qrc：web 页面要读写 localStorage（家长的作品就存在那里），
 * 而 `qrc:` 是自定义 scheme，Chromium 不给它 opaquestorage，存了下次也没了。
 * 走 file:// 才有稳定 origin。
 *
 * 文件和 JS 之间的搬运（这是 QML 做不了的部分）：
 *   - 选图 → `jsForImage()` 读文件、base64、拼出 `window.KS.importAsset({...})`
 *     的调用串，由 QML 丢给 WebEngineView.runJavaScript 执行；
 *   - 导出 → QML 取回 `KS.currentProjectBase64()` 的结果，交给
 *     `saveProjectBase64()` 落成 .json。
 * 之所以用「C++ 拼串、QML 执行」，是因为 runJavaScript 的返回值走的是 QML
 * 回调，C++ 侧拿不到；这样分工不引入额外的信号往返。
 */
class Maker : public QObject
{
    Q_OBJECT

    /// 能不能用：编译期带 WebEngine，且找得到 index.html。
    Q_PROPERTY(bool available READ available CONSTANT)
    /// 编辑器地址（file://）。
    Q_PROPERTY(QUrl editorUrl READ editorUrl CONSTANT)
    /// 实际使用的目录，仅用于自检展示。
    Q_PROPERTY(QString baseDir READ baseDir CONSTANT)
    /// 编译期是否链接了 WebEngine。
    Q_PROPERTY(bool webEngineBuilt READ webEngineBuilt CONSTANT)
    /// 导出默认目录（下载目录；没有就用家目录）。
    Q_PROPERTY(QString exportDir READ exportDir CONSTANT)

public:
    explicit Maker(QObject *parent = nullptr);

    bool available() const { return m_url.isValid() && !m_url.isEmpty(); }
    QUrl editorUrl() const { return m_url; }
    QString baseDir() const { return m_baseDir; }
    bool webEngineBuilt() const;
    QString exportDir() const;

    /// 把本地图片读成 data URL，拼出调用 importAsset 的 JS。失败返回空串。
    /// 图片会在这里的 Qt 侧先缩到 480px —— 家长选的常是手机原图（几 MB），
    /// 原样塞进 runJavaScript 的字符串在低配机器上会明显卡一下。
    Q_INVOKABLE QString jsForImage(const QUrl &fileUrl);

    /// 读一个作品 .json，拼出 `KS.loadProjectBase64("...")` 的 JS。
    Q_INVOKABLE QString jsForProject(const QUrl &fileUrl);

    /// 把 base64 的作品写进导出目录，返回落盘路径。失败返回空串。
    Q_INVOKABLE QString saveProjectBase64(const QString &b64, const QString &suggestedName);

    /// 给 UI 用的通用失败原因（最近一次操作的错误）。
    Q_PROPERTY(QString lastError READ lastError NOTIFY lastErrorChanged)
    QString lastError() const { return m_lastError; }

signals:
    void lastErrorChanged();

private:
    void setError(const QString &text);

    QUrl m_url;
    QString m_baseDir;
    QString m_lastError;
};
