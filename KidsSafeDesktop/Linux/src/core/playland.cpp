#include "playland.h"

#include <QCoreApplication>
#include <QDebug>
#include <QDir>
#include <QFileInfo>
#include <QLibraryInfo>
#include <QStringList>

namespace {

/**
 * 探测某个 QML 模块（如 QtWebEngine）是不是真的装了。
 *
 * 编译期找到 WebEngine 不代表运行期能 `import QtWebEngine`：Debian/deepin 把
 * QML 模块单独打成一个包（qml6-module-qtwebengine），只装 -dev 时能编过、
 * 一打开网页页就报 `module "QtWebEngine" is not installed`，整页空白。
 * 所以这里按 QML 导入路径找 <模块路径>/qmldir，找不到就把入口降级成提示。
 * 逻辑与 main.cpp 里的 qmlStyleInstalled() 同一套。
 */
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

} // namespace

Playland::Playland(QObject *parent)
    : QObject(parent)
{
#ifdef KIDSSAFE_WEBENGINE
    if (!qmlModuleInstalled(QStringLiteral("QtWebEngine"))) {
        qWarning() << "[playland] 找不到 QML 模块 QtWebEngine"
                      "（deepin 上装 qml6-module-qtwebengine），网页游戏入口已禁用";
        return;
    }

    QStringList candidates;

    // 1. 环境变量。排障时用 KIDSSAFE_PLAYLAND_DIR=~/playland 指一份改过的资源，
    //    不用重新装包。
    const QString envDir = qEnvironmentVariable("KIDSSAFE_PLAYLAND_DIR");
    if (!envDir.isEmpty()) {
        candidates << envDir;
    }

    // 2. 二进制旁边的 share。装到 /usr/bin 时是 /usr/share/kidssafe/playland，
    //    解压到任意目录做绿色运行时是 <解压目录>/share/...，两种都试。
    const QString appDir = QCoreApplication::applicationDirPath();
    if (!appDir.isEmpty()) {
        candidates << appDir + QStringLiteral("/../share/kidssafe/playland");
        candidates << appDir + QStringLiteral("/share/kidssafe/playland");
    }

    // 3. 常见安装前缀（包管理器 / 源码 make install）。
    candidates << QStringLiteral("/usr/share/kidssafe/playland");
    candidates << QStringLiteral("/usr/local/share/kidssafe/playland");

    // 4. 源码树。CMake 把绝对路径编进来，`cmake --build` 完直接跑二进制也能用，
    //    不必先 make install。
#ifdef KIDSSAFE_PLAYLAND_SRC
    candidates << QStringLiteral(KIDSSAFE_PLAYLAND_SRC);
#endif

    for (const QString &raw : candidates) {
        const QString dir = QDir::cleanPath(raw);
        const QFileInfo index(dir + QStringLiteral("/index.html"));
        if (index.exists() && index.isFile()) {
            m_baseDir = dir;
            m_hall = QUrl::fromLocalFile(index.absoluteFilePath());
            break;
        }
    }

    // 5. qrc 兜底。当前构建把资源装在 share 里、没有打进 qrc（见 CMakeLists 的
    //    install(DIRECTORY assets/playland)），这一步是留给「单文件绿色包」的：
    //    哪天把资源编进 qrc，这里不用改代码就能接上。
    if (!available()) {
        const QFileInfo index(QStringLiteral(":/playland/index.html"));
        if (index.exists() && index.isFile()) {
            m_baseDir = QStringLiteral("qrc:/playland");
            m_hall = QUrl(QStringLiteral("qrc:/playland/index.html"));
        }
    }

    if (available()) {
        qInfo() << "[playland] 网页游戏目录：" << m_baseDir;
    } else {
        qWarning() << "[playland] 未找到网页游戏资源（index.html），入口将不可用";
    }
#else
    // 没链接 WebEngine 时整块功能不可用。这里不报警告：这是打包选择，
    // 不是错误，家长后台/自检会通过 available=false 显示「组件未安装」。
    qInfo() << "[playland] 本次构建未包含 QtWebEngine，网页小游戏不可用";
#endif
}

bool Playland::webEngineBuilt() const
{
#ifdef KIDSSAFE_WEBENGINE
    return true;
#else
    return false;
#endif
}

QUrl Playland::pageUrl(const QString &relative) const
{
    if (!available() || relative.isEmpty()) {
        return QUrl();
    }

    // 已经是完整地址（http/file/qrc）就原样返回，方便临时指向外部页面。
    const QUrl direct(relative);
    if (!direct.scheme().isEmpty()) {
        return direct;
    }

    QString base = m_hall.toString();
    const int slash = base.lastIndexOf(QLatin1Char('/'));
    if (slash < 0) {
        return QUrl();
    }
    return QUrl(base.left(slash + 1) + relative);
}
