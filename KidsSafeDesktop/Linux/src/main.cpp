#include "core/appcatalog.h"
#include "core/applauncher.h"
#include "core/kiosk.h"
#include "core/screentime.h"
#include "core/settings.h"
#include "core/speech.h"
#include "core/storyservice.h"

#include <QDir>
#include <QFileInfo>
#include <QGuiApplication>
#include <QQmlApplicationEngine>
#include <QQmlEngine>
#include <QQuickStyle>
#include <QQuickWindow>
#include <QStandardPaths>

#include <KAboutData>
#include <KLocalizedString>

#include <cstdio>

namespace {

bool hasArg(int argc, char *argv[], const char *flag)
{
    for (int i = 1; i < argc; ++i) {
        if (qstrcmp(argv[i], flag) == 0) {
            return true;
        }
    }
    return false;
}

void line(const char *label, const QString &value)
{
    std::printf("  %-18s %s\n", label, value.toUtf8().constData());
}

/**
 * 无界面自检。check-host.sh 会调用它，也可以在 CI 或排障时单独跑：
 *   kidssafe --self-test
 *
 * 它做的是「不需要人看屏幕就能判定」的那部分验证：配置能不能写、
 * 应用清单扫不扫得到、QML 能不能实例化。至于按键拦不拦得住、
 * 崩溃会不会自动重启，只能人工在真机会话里试。
 */
int runSelfTest(Settings *settings,
                AppCatalog *catalog,
                Speech *speech,
                Kiosk *kiosk,
                StoryService *stories,
                bool qmlLoaded)
{
    std::printf("\n儿童安全桌面 · 自检\n\n");

    int failures = 0;

    // 配置
    const QString configPath =
        QStandardPaths::writableLocation(QStandardPaths::ConfigLocation)
        + QStringLiteral("/kidssafe/kidssafe.conf");
    const QFileInfo configInfo(configPath);
    const bool configWritable = configInfo.exists()
        ? configInfo.isWritable()
        : QFileInfo(configInfo.absolutePath()).isWritable();

    line("配置文件", configPath);
    line("可写", configWritable ? QStringLiteral("是") : QStringLiteral("否"));
    line("家长密码", settings->hasPin() ? QStringLiteral("已设置") : QStringLiteral("未设置"));
    if (!configWritable) {
        ++failures;
    }

    // 应用清单
    catalog->refresh();
    const int appCount = catalog->rowCount();
    line("可加入白名单的应用", QString::number(appCount));
    if (appCount == 0) {
        ++failures;
    }

    // 语音
    line("语音合成 (spd-say)", speech->available() ? QStringLiteral("可用") : QStringLiteral("缺失（朗读降级）"));

    // 会话
    line("kiosk 会话模式", kiosk->sessionMode() ? QStringLiteral("是") : QStringLiteral("否（当前是普通会话）"));

    // 故事服务
    line("故事服务器", settings->serverUrl().isEmpty() ? QStringLiteral("未配置（离线）") : settings->serverUrl());
    line("在线内容", stories->onlineEnabled() ? QStringLiteral("已启用") : QStringLiteral("未启用"));

    // QML
    line("QML 界面加载", qmlLoaded ? QStringLiteral("成功") : QStringLiteral("失败"));
    if (!qmlLoaded) {
        ++failures;
    }

    std::printf("\n  %s\n\n",
                failures == 0 ? "自检通过" : "自检发现问题，见上方标红的项");
    return failures == 0 ? 0 : 1;
}

} // namespace

int main(int argc, char *argv[])
{
    const bool selfTest = hasArg(argc, argv, "--self-test");
    if (selfTest) {
        // 自检要能在没有显示器的机器上跑（CI、SSH），所以先切到 offscreen。
        // 这必须在 QGuiApplication 构造之前设置。
        qputenv("QT_QPA_PLATFORM", "offscreen");
    }

    QGuiApplication app(argc, argv);
    KLocalizedString::setApplicationDomain("kidssafe");

    KAboutData about(QStringLiteral("kidssafe"),
                     i18n("儿童安全桌面"),
                     QStringLiteral("1.19.0"),
                     i18n("给小朋友的专属安全桌面"),
                     KAboutLicense::GPL,
                     i18n("© 2026 KidsSafeDesktop"));
    KAboutData::setApplicationData(about);

    QGuiApplication::setOrganizationDomain(QStringLiteral("kidssafe.local"));
    QGuiApplication::setApplicationName(QStringLiteral("kidssafe"));
    QGuiApplication::setDesktopFileName(QStringLiteral("kidssafe"));

    // Breeze 是 Plasma 默认样式，儿童桌面也用同一套，观感才和系统一致
    QQuickStyle::setStyle(QStringLiteral("org.kde.breeze"));

    auto *settings = new Settings(&app);
    auto *screenTime = new ScreenTime(settings, &app);
    auto *catalog = new AppCatalog(&app);
    auto *launcher = new AppLauncher(settings, &app);
    auto *kiosk = new Kiosk(&app);
    auto *speech = new Speech(&app);
    auto *stories = new StoryService(settings, &app);

    // 逐个注册为 QML 单例。刻意不用 QML_SINGLETON 宏：
    // 显式注册能让「谁依赖谁」在 main 里一眼看全，也便于将来换实现。
    qmlRegisterSingletonInstance("KidsSafe", 1, 0, "Settings", settings);
    qmlRegisterSingletonInstance("KidsSafe", 1, 0, "ScreenTime", screenTime);
    qmlRegisterSingletonInstance("KidsSafe", 1, 0, "AppCatalog", catalog);
    qmlRegisterSingletonInstance("KidsSafe", 1, 0, "AppLauncher", launcher);
    qmlRegisterSingletonInstance("KidsSafe", 1, 0, "Kiosk", kiosk);
    qmlRegisterSingletonInstance("KidsSafe", 1, 0, "Speech", speech);
    qmlRegisterSingletonInstance("KidsSafe", 1, 0, "Stories", stories);

    QQmlApplicationEngine engine;
    engine.load(QUrl(QStringLiteral("qrc:/qml/Main.qml")));
    const bool qmlLoaded = !engine.rootObjects().isEmpty();

    if (selfTest) {
        return runSelfTest(settings, catalog, speech, kiosk, stories, qmlLoaded);
    }

    if (!qmlLoaded) {
        return -1;
    }

    // 窗口一出来就接管：全屏、无边框、写锁定标记
    if (auto *window = qobject_cast<QQuickWindow *>(engine.rootObjects().constFirst())) {
        kiosk->attach(window);
    }

    return app.exec();
}
