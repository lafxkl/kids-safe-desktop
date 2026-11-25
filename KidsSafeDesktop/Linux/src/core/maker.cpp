#include "maker.h"

#include <QBuffer>
#include <QByteArray>
#include <QCoreApplication>
#include <QDebug>
#include <QDir>
#include <QFile>
#include <QFileInfo>
#include <QImage>
#include <QJsonDocument>
#include <QJsonObject>
#include <QLibraryInfo>
#include <QRegularExpression>
#include <QStandardPaths>
#include <QStringList>

namespace {

/// 与 playland.cpp 同一套探测：编得过不代表运行期能 import。
bool qmlModuleInstalled(const QString &moduleName)
{
    const QString relative = QString(moduleName).replace(QLatin1Char('.'), QLatin1Char('/'));

    QStringList roots;
    roots << QLibraryInfo::path(QLibraryInfo::QmlImportsPath);
    for (const char *var : {"QML_IMPORT_PATH", "QML2_IMPORT_PATH"}) {
        const QString value = qEnvironmentVariable(var);
        if (!value.isEmpty()) {
            roots += value.split(QDir::listSeparator(), Qt::SkipEmptyParts);
        }
    }

    for (const QString &root : roots) {
        if (QFileInfo::exists(root + QLatin1Char('/') + relative + QStringLiteral("/qmldir"))) {
            return true;
        }
    }
    return false;
}

QString mimeForSuffix(const QString &suffix)
{
    const QString s = suffix.toLower();
    if (s == QLatin1String("jpg") || s == QLatin1String("jpeg")) return QStringLiteral("image/jpeg");
    if (s == QLatin1String("gif")) return QStringLiteral("image/gif");
    if (s == QLatin1String("webp")) return QStringLiteral("image/webp");
    if (s == QLatin1String("svg")) return QStringLiteral("image/svg+xml");
    if (s == QLatin1String("bmp")) return QStringLiteral("image/bmp");
    return QStringLiteral("image/png");
}

/// 文件名清洗：去掉路径分隔符与 Linux 上真正会出问题的字符。
QString sanitizeFileName(const QString &raw)
{
    QString name = raw.trimmed();
    if (name.isEmpty()) {
        name = QStringLiteral("我的小游戏");
    }
    static const QString bad = QStringLiteral("/\\:*?\"<>|");
    for (const QChar c : bad) {
        name.replace(c, QLatin1Char('_'));
    }
    // 控制字符与换行也一并抹掉
    name.remove(QRegularExpression(QStringLiteral("[\\x00-\\x1f]")));
    if (name.size() > 60) {
        name = name.left(60);
    }
    return name;
}

} // namespace

Maker::Maker(QObject *parent)
    : QObject(parent)
{
#ifdef KIDSSAFE_WEBENGINE
    if (!qmlModuleInstalled(QStringLiteral("QtWebEngine"))) {
        qWarning() << "[maker] 找不到 QML 模块 QtWebEngine（deepin 上装 qml6-module-qtwebengine），"
                      "小游戏工坊入口已禁用";
        return;
    }

    QStringList candidates;

    const QString envDir = qEnvironmentVariable("KIDSSAFE_MAKER_DIR");
    if (!envDir.isEmpty()) {
        candidates << envDir;
    }

    const QString appDir = QCoreApplication::applicationDirPath();
    if (!appDir.isEmpty()) {
        candidates << appDir + QStringLiteral("/../share/kidssafe/maker");
        candidates << appDir + QStringLiteral("/share/kidssafe/maker");
    }

    candidates << QStringLiteral("/usr/share/kidssafe/maker");
    candidates << QStringLiteral("/usr/local/share/kidssafe/maker");

#ifdef KIDSSAFE_MAKER_SRC
    candidates << QStringLiteral(KIDSSAFE_MAKER_SRC);
#endif

    for (const QString &raw : candidates) {
        const QString dir = QDir::cleanPath(raw);
        const QFileInfo index(dir + QStringLiteral("/index.html"));
        if (index.exists() && index.isFile()) {
            m_baseDir = dir;
            m_url = QUrl::fromLocalFile(index.absoluteFilePath());
            break;
        }
    }

    if (available()) {
        qInfo() << "[maker] 编辑器目录：" << m_baseDir;
    } else {
        qWarning() << "[maker] 未找到编辑器资源（index.html），入口将不可用";
    }
#else
    qInfo() << "[maker] 本次构建未包含 QtWebEngine，小游戏工坊不可用";
#endif
}

bool Maker::webEngineBuilt() const
{
#ifdef KIDSSAFE_WEBENGINE
    return true;
#else
    return false;
#endif
}

QString Maker::exportDir() const
{
    QString dir = QStandardPaths::writableLocation(QStandardPaths::DownloadLocation);
    if (dir.isEmpty()) {
        dir = QStandardPaths::writableLocation(QStandardPaths::HomeLocation);
    }
    if (dir.isEmpty()) {
        dir = QDir::tempPath();
    }
    return dir;
}

void Maker::setError(const QString &text)
{
    if (m_lastError == text) {
        return;
    }
    m_lastError = text;
    emit lastErrorChanged();
}

QString Maker::jsForImage(const QUrl &fileUrl)
{
    setError(QString());

    const QString path = fileUrl.isLocalFile() ? fileUrl.toLocalFile() : fileUrl.toString();
    QFile file(path);
    if (!file.open(QIODevice::ReadOnly)) {
        setError(QStringLiteral("读不了这个文件：%1").arg(path));
        return QString();
    }
    const QByteArray raw = file.readAll();
    file.close();
    if (raw.isEmpty()) {
        setError(QStringLiteral("文件是空的"));
        return QString();
    }

    const QFileInfo info(path);
    QByteArray payload = raw;
    QString mime = mimeForSuffix(info.suffix());

    // 先缩到 480px：原图 base64 之后小则几百 KB、大则十几 MB，
    // 一次性当 JS 字符串塞进渲染进程，低配机器会卡；网页自己还会再压到 320px。
    QImage image;
    if (image.loadFromData(raw) && !image.isNull()) {
        if (image.width() > 480 || image.height() > 480) {
            const QImage small = image.scaled(480, 480, Qt::KeepAspectRatio, Qt::SmoothTransformation);
            QByteArray png;
            QBuffer buffer(&png);
            buffer.open(QIODevice::WriteOnly);
            if (small.save(&buffer, "PNG")) {
                buffer.close();
                // PNG 有透明通道，贴纸/图标不会变黑底
                payload = png;
                mime = QStringLiteral("image/png");
            }
        }
    }

    QJsonObject obj;
    obj.insert(QStringLiteral("name"), info.fileName());
    obj.insert(QStringLiteral("mime"), mime);
    obj.insert(QStringLiteral("dataUrl"),
               QStringLiteral("data:%1;base64,%2")
                   .arg(mime, QString::fromLatin1(payload.toBase64())));

    const QByteArray json = QJsonDocument(obj).toJson(QJsonDocument::Compact);
    return QStringLiteral("window.KS.importAsset(%1);").arg(QString::fromUtf8(json));
}

QString Maker::jsForProject(const QUrl &fileUrl)
{
    setError(QString());

    const QString path = fileUrl.isLocalFile() ? fileUrl.toLocalFile() : fileUrl.toString();
    QFile file(path);
    if (!file.open(QIODevice::ReadOnly)) {
        setError(QStringLiteral("读不了这个文件：%1").arg(path));
        return QString();
    }
    const QByteArray raw = file.readAll();
    file.close();
    if (raw.isEmpty()) {
        setError(QStringLiteral("文件是空的"));
        return QString();
    }

    // 网页按 UTF-8 解 base64，这里必须先把文本规范成 UTF-8 字节。
    // 顺带去掉 BOM：Windows 记事本另存的 .json 常带 EF BB BF，
    // 而 JS 的 JSON.parse 遇到开头 BOM 会直接抛「Unexpected token」。
    QString text = QString::fromUtf8(raw);
    if (text.startsWith(QChar(0xFEFF))) {
        text.remove(0, 1);
    }
    const QByteArray utf8 = text.toUtf8();

    return QStringLiteral("window.KS.loadProjectBase64(\"%1\");")
        .arg(QString::fromLatin1(utf8.toBase64()));
}

QString Maker::saveProjectBase64(const QString &b64, const QString &suggestedName)
{
    setError(QString());

    const QByteArray json = QByteArray::fromBase64(b64.toLatin1());
    if (json.isEmpty()) {
        setError(QStringLiteral("没有可导出的内容"));
        return QString();
    }

    QDir dir(exportDir());
    if (!dir.exists()) {
        dir.mkpath(QStringLiteral("."));
    }

    QString base = sanitizeFileName(suggestedName);
    if (base.endsWith(QLatin1String(".json"), Qt::CaseInsensitive)) {
        base.chop(5);
    }

    // 同名不覆盖：家长可能就是想留两版
    QString path = dir.filePath(base + QStringLiteral(".json"));
    for (int i = 2; QFileInfo::exists(path) && i < 100; ++i) {
        path = dir.filePath(QStringLiteral("%1-%2.json").arg(base).arg(i));
    }

    QFile out(path);
    if (!out.open(QIODevice::WriteOnly | QIODevice::Truncate)) {
        setError(QStringLiteral("写不进导出目录：%1").arg(dir.absolutePath()));
        return QString();
    }
    out.write(json);
    out.close();

    qInfo() << "[maker] 作品已导出：" << path;
    return path;
}
