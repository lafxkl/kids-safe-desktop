#include "core/appcatalog.h"
#include "core/applauncher.h"
#include "core/codelab.h"
#include "core/eyecare.h"
#include "core/feedback.h"
#include "core/filemodel.h"
#include "core/hotkeyguard.h"
#include "core/kiosk.h"
#include "core/learnreport.h"
#include "core/maker.h"
#include "core/mediaplayer.h"
#include "core/netplay.h"
#include "core/playland.h"
#include "core/proctree.h"
#include "core/screentime.h"
#include "core/settings.h"
#include "core/startup.h"
#include "core/speech.h"
#include "core/terminal.h"
#include "core/storyservice.h"
#include "core/weather.h"
#include "core/themeicon.h"
#include "core/vnengine.h"
#include "nes/nesemulator.h"
#include "nes/nesview.h"

#include <QColor>
#include <QDateTime>
#include <QDebug>
#include <QDir>
#include <QFile>
#include <QFileInfo>
#include <QGuiApplication>
#include <QLibraryInfo>
#include <QPalette>
#include <QQmlApplicationEngine>
#include <QQmlEngine>
#include <QQuickStyle>
#include <QQuickWindow>
#include <QStandardPaths>
#include <QStringList>

#include <KAboutData>
#include <KLocalizedString>

// 只有打包时找到了 QtWebEngine 才会进来（见 CMakeLists.txt）。
// 网页小游戏用的就是它；没装时整个头文件都不存在，所以必须写在条件编译里。
#ifdef KIDSSAFE_WEBENGINE
#include <QtWebEngineQuick/QtWebEngineQuick>
#endif

#include <cstdio>

namespace {

/**
 * 日志同时写一份到文件。
 *
 * 全屏 kiosk 会话里切不到终端，出了问题只能看见最后那一屏画面，日志全丢。
 * 这里把 qDebug/qInfo/qWarning 原样落盘到
 * `~/.local/share/kidssafe/kidssafe.log`（追加写），事后能翻。
 * 终端里照常输出，不影响原有习惯。
 */
QFile *g_logFile = nullptr;
/// 实际打开的日志路径。自检会把它打出来，省得满磁盘找文件。
QString g_logPath;

void fileMessageHandler(QtMsgType type, const QMessageLogContext &context, const QString &message)
{
    const char *level = "INFO";
    switch (type) {
    case QtDebugMsg:
        level = "DEBUG";
        break;
    case QtInfoMsg:
        level = "INFO";
        break;
    case QtWarningMsg:
        level = "WARN";
        break;
    case QtCriticalMsg:
        level = "ERROR";
        break;
    case QtFatalMsg:
        level = "FATAL";
        break;
    }

    Q_UNUSED(context)
    std::fprintf(stderr, "[%s] %s\n", level, message.toLocal8Bit().constData());
    std::fflush(stderr);

    if (g_logFile && g_logFile->isOpen()) {
        const QString stamp = QDateTime::currentDateTime().toString(QStringLiteral("yyyy-MM-dd HH:mm:ss.zzz"));
        const QString text = QStringLiteral("%1 [%2] %3\n")
                                 .arg(stamp, QString::fromLatin1(level), message);
        g_logFile->write(text.toUtf8());
        g_logFile->flush();
    }
}

void installFileLogger()
{
    // 候选目录按优先级排：
    //   1. AppDataLocation —— 正常情况就是 ~/.local/share/kidssafe；
    //   2. GenericDataLocation/kidssafe —— 万一以后把 organizationName 设上，
    //      AppDataLocation 会变成 ~/.local/share/<org>/kidssafe，与会话脚本的
    //      STATE_DIR 对不上，这里兜一层固定路径；
    //   3. 临时目录 —— 前两个都写不进去（权限异常、磁盘满）时留个现场。
    QStringList candidates;
    const QString appData = QStandardPaths::writableLocation(QStandardPaths::AppDataLocation);
    if (!appData.isEmpty()) {
        candidates << appData + QStringLiteral("/kidssafe.log");
    }
    candidates << QStandardPaths::writableLocation(QStandardPaths::GenericDataLocation)
                      + QStringLiteral("/kidssafe/kidssafe.log");
    candidates << QDir::tempPath() + QStringLiteral("/kidssafe.log");

    for (const QString &path : candidates) {
        QDir().mkpath(QFileInfo(path).absolutePath());
        auto *file = new QFile(path);
        if (file->open(QIODevice::WriteOnly | QIODevice::Append)) {
            g_logFile = file;
            g_logPath = path;
            break;
        }
        delete file;
    }

    qInstallMessageHandler(fileMessageHandler);

    if (g_logFile) {
        // 用 printf 打路径：这行要在终端里一定看得见（有人会把输出重定向掉），
        // 也顺手告诉排障的人该去哪儿找。
        std::printf("日志文件：%s\n", g_logPath.toLocal8Bit().constData());
        std::fflush(stdout);
        qInfo() << "[app] 日志开始记录：" << g_logPath;
    } else {
        std::printf("日志文件：无法创建（只输出到终端）\n");
        std::fflush(stdout);
    }
}

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
 * 探测某个 QML 样式模块（如 org.kde.breeze）是否真的装了。
 *
 * 必须提前探测，因为 QQuickStyle::setStyle() 指向一个不存在的样式模块时，
 * Qt 不会悄悄退回默认样式，而是让「第一个 import QtQuick.Controls 的文件」
 * 直接报 `module "org.kde.breeze" is not installed`，整个界面加载失败——
 * 表现就是全屏黑屏。deepin 的仓库里没有 qqc2-breeze-style（它用自家 DTK
 * 风格），所以在 deepin 上不做探测必然黑屏。
 *
 * 探测方式是按 QML 导入路径找 <模块路径>/qmldir，覆盖编译期导入路径和
 * 环境变量指定的路径（QML_IMPORT_PATH / QML2_IMPORT_PATH）。
 */
bool qmlStyleInstalled(const QString &moduleName)
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
        const QString qmldir = root + QLatin1Char('/') + relative + QStringLiteral("/qmldir");
        if (QFileInfo::exists(qmldir)) {
            return true;
        }
    }
    return false;
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
                Playland *playland,
                Weather *weather,
                NesEmulator *nes,
                const QString &styleName,
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
    line("日志文件", g_logPath.isEmpty() ? QStringLiteral("不可用（只输出到终端）") : g_logPath);
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
    // 可用性探测是异步的（要跑一次 spd-say -O），而自检这条链路没有事件循环，
    // 不在这里同步等一次的话，打印出来的永远是「正在检测…」。语音不可用不算
    // 失败项：缺 TTS 只是朗读降级，桌面本身照常能用。
    speech->waitForReady();
    line("语音合成 (spd-say)",
         (speech->available() ? QStringLiteral("可用 · ") : QStringLiteral("不可用 · "))
             + speech->detail());

    // 会话
    line("kiosk 会话模式", kiosk->sessionMode() ? QStringLiteral("是") : QStringLiteral("否（当前是普通会话）"));

    // 故事服务
    line("故事服务器", settings->serverUrl().isEmpty() ? QStringLiteral("未配置（离线）") : settings->serverUrl());
    line("在线内容", stories->onlineEnabled() ? QStringLiteral("已启用") : QStringLiteral("未启用"));

    // 网页小游戏。分三种情况：
    //   编译时没带 WebEngine —— 打包选择，不算失败；
    //   带了但找不到 index.html —— 装包漏了资源，算失败；
    //   都正常 —— 打印实际用的目录，排障时一眼看出读的是哪一份。
    if (!playland->webEngineBuilt()) {
        line("网页小游戏", QStringLiteral("未编译 QtWebEngine（原生小游戏可用）"));
    } else if (!playland->available()) {
        line("网页小游戏", QStringLiteral("资源缺失（找不到 index.html）"));
        ++failures;
    } else {
        line("网页小游戏", playland->baseDir());
    }

    // 首页天气。自检不联网（Weather 只是构造时读了缓存），所以这里反映的是
    // 「缓存里有什么」。排障时常见的问题是「天气预报一直空着」，先看这一行：
    //   已关闭       → 家长后台把开关关了；
    //   无缓存数据   → 从来没成功取过数，看日志里的 [weather] 行；
    //   有城市有温度 → 缓存正常，问题在网络或接口。
    if (!settings->weatherEnabled()) {
        line("首页天气", QStringLiteral("已关闭（家长后台）"));
    } else if (!weather->available()) {
        line("首页天气", QStringLiteral("无缓存数据（还没成功取过数）"));
    } else {
        line("首页天气",
             QStringLiteral("%1 %2 %3（%4）")
                 .arg(weather->city(), weather->tempText(), weather->descText(),
                      weather->statusText()));
    }

    // 红白机：只统计 ROM 数量，不实例化模拟器（自检不跑游戏）。
    // ROM 不随包分发（版权），所以「0 个」是正常状态，不算失败项。
    line("红白机 ROM",
         nes->roms().isEmpty()
             ? QStringLiteral("0 个（放到 %1）").arg(nes->romDir())
             : QStringLiteral("%1 个 · %2").arg(nes->roms().size()).arg(nes->romDir()));

    // QML
    line("QML 样式", styleName);
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

    // 调试模式：窗口化运行，不无边框、不全屏、允许关闭。
    // 全屏会话里切不到终端，排障时开着它，旁边摆个终端看日志。
    // 也认环境变量，方便 `.desktop` 或脚本里临时打开。
    const bool windowed = hasArg(argc, argv, "--windowed")
        || qEnvironmentVariableIntValue("KIDSSAFE_WINDOWED") == 1;

    if (selfTest) {
        // 自检要能在没有显示器的机器上跑（CI、SSH），所以先切到 offscreen。
        // 这必须在 QGuiApplication 构造之前设置。
        qputenv("QT_QPA_PLATFORM", "offscreen");
    }

    // 分数缩放的取整策略同样只能在 QGuiApplication 构造之前设定，
    // 晚了 Qt 会打印 "setHighDpiScaleFactorRoundingPolicy must be called
    // before creating the QGuiApplication instance"。用 PassThrough 保留
    // 1.25 / 1.5 这类真实缩放比例，避免界面上取整后忽大忽小。
    QGuiApplication::setHighDpiScaleFactorRoundingPolicy(
        Qt::HighDpiScaleFactorRoundingPolicy::PassThrough);

#ifdef KIDSSAFE_WEBENGINE
    // ---- Chromium 渲染参数的默认值（必须早于 initialize()）----
    //
    // 症状：一打开网页游戏，终端就被这类日志刷屏——
    //   [ERROR:gbm_wrapper.cc(76)] Failed to get fd for plane.: 没有那个文件或目录
    //   [ERROR:gbm_wrapper.cc(259)] Failed to export buffer to dma_buf: ...
    //
    // 含义：Chromium 的 GPU 进程在走 GBM/DMA-BUF 这条「零拷贝」送图路径，
    // 但当前环境拿不到可导出的 dma-buf 缓冲。最常见于
    //   1) 虚拟机（VirtualBox / VMware / QEMU-KVM）里的虚拟显卡；
    //   2) 远程桌面 / VNC 会话；
    //   3) 没装好闭源驱动的 NVIDIA，或内核 DRM 权限不对；
    //   4) Wayland 会话下没有可用的 DRM render node。
    // Chromium 遇到这种失败会自动退回软件合成，所以**多数情况下画面仍然正常，
    // 只是日志脏**（本机实测：15 张卡片正常显示，但每帧刷两行 ERROR）；
    // 偶发会看到网页区域全黑或闪白。
    //
    // 两层处理，缺一不可：
    //   1. QTWEBENGINE_FORCE_USE_GBM=0 —— 这是 QtWebEngine 自己的开关，
    //      关掉「网页帧用 dma-buf 直接交给 Qt 合成器」这条通路，改走共享内存
    //      拷贝。**只加 --disable-gpu 是盖不住这些报错的**，因为那只是不让
    //      Chromium 用硬件合成，GBM 送图那段仍然会被执行到。
    //   2. --disable-gpu —— 虚拟机的虚拟显卡（llvmpipe / vmwgfx）本来就跑不动
    //      硬件合成，索性锁死软件路径，省得来回探测。
    //
    // 我们这 15 款都是 Canvas 2D 小游戏，软件合成完全够用。
    // 真机有独显、想试硬件加速：KIDSSAFE_WEB_GPU=1 ./kidssafe --windowed
    // （该开关只放开 --disable-gpu，GBM 仍然保持关闭，避免又开始刷屏。）
    if (!selfTest) {
        if (qEnvironmentVariableIsEmpty("QTWEBENGINE_FORCE_USE_GBM")) {
            qputenv("QTWEBENGINE_FORCE_USE_GBM", "0");
        }
        if (qEnvironmentVariableIntValue("KIDSSAFE_WEB_GPU") != 1) {
            QString flags = qEnvironmentVariable("QTWEBENGINE_CHROMIUM_FLAGS");
            if (!flags.contains(QLatin1String("--disable-gpu"))) {
                if (!flags.isEmpty()) {
                    flags += QLatin1Char(' ');
                }
                // --disable-gpu：不用 GPU 进程，合成/光栅都在 CPU 上做；
                // --disable-dev-shm-usage：/dev/shm 太小（容器、部分定制镜像）时
                // 改用 /tmp，否则 Chromium 会随机崩渲染进程。
                flags += QStringLiteral("--disable-gpu --disable-dev-shm-usage");
            }
            qputenv("QTWEBENGINE_CHROMIUM_FLAGS", flags.toUtf8());
        }
    }

    // 必须在 QGuiApplication 之前：它会设置 ShareOpenGLContexts 等属性，
    // 晚了 Chromium 起不来（日志里是 "Qt WebEngine seems to be initialized
    // from a plugin, which is not supported"）。
    // --self-test 用 offscreen 平台，起 WebEngine 只会拖慢并刷一堆警告，
    // 自检也不碰网页游戏，所以直接跳过。
    if (!selfTest) {
        QtWebEngineQuick::initialize();
    }
#endif

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

    // 必须在 setApplicationName 之后：日志路径取自 AppDataLocation。
    installFileLogger();

    // Breeze 是 Plasma 默认样式，儿童桌面也用同一套，观感才和系统一致。
    // 但样式模块缺了会让 QML 整个加载失败（不是回退，是报错），所以先探测：
    // deepin 这类不提供 qqc2-breeze-style 的发行版上退回 Qt 自带样式，
    // 界面颜色本来就写死在 QML 里，观感差异只在控件细节上。
    // 带点号的样式名是「QML 模块样式」（如 org.kde.desktop），缺了同样会炸；
    // 内置样式（Basic / Fusion / Material …）不在此列，不碰。
    const QString envStyle = qEnvironmentVariable("QT_QUICK_CONTROLS_STYLE");
    if (envStyle.contains(QLatin1Char('.')) && !qmlStyleInstalled(envStyle)) {
        qunsetenv("QT_QUICK_CONTROLS_STYLE");
    }

    const bool breezeAvailable = qmlStyleInstalled(QStringLiteral("org.kde.breeze"));
    if (breezeAvailable) {
        QQuickStyle::setStyle(QStringLiteral("org.kde.breeze"));
    }
    const QString styleName = breezeAvailable ? QStringLiteral("Breeze")
                                              : QStringLiteral("内置（未装 qqc2-breeze-style）");

    // 儿童界面的配色是写死的浅色（米色背景 / 白色卡片 / 棕色文字），
    // 但 QtQuick Controls 里还有一批控件的外观取自系统调色板。系统用深色
    // 主题时两者会打架：deepin 上实测引导页的姓名输入框变成白字白底
    // （看上去就是「没有输入框」），应用开关变成一颗纯黑的药丸。
    // 这里固定一套浅色调色板，让所有控件都跟设计稿一致；家长把系统切成
    // 深色模式也不会影响孩子这边。
    QPalette kidsPalette;
    kidsPalette.setColor(QPalette::Window, QColor(0xFF, 0xF8, 0xEE));
    kidsPalette.setColor(QPalette::WindowText, QColor(0x4A, 0x3B, 0x32));
    kidsPalette.setColor(QPalette::Base, QColor(0xFF, 0xFF, 0xFF));
    kidsPalette.setColor(QPalette::AlternateBase, QColor(0xFF, 0xF1, 0xE2));
    kidsPalette.setColor(QPalette::Text, QColor(0x4A, 0x3B, 0x32));
    kidsPalette.setColor(QPalette::Button, QColor(0xFF, 0xF8, 0xEE));
    kidsPalette.setColor(QPalette::ButtonText, QColor(0x4A, 0x3B, 0x32));
    kidsPalette.setColor(QPalette::Highlight, QColor(0xFF, 0x8A, 0x5C));
    kidsPalette.setColor(QPalette::HighlightedText, QColor(0xFF, 0xFF, 0xFF));
    kidsPalette.setColor(QPalette::PlaceholderText, QColor(0xC4, 0xAE, 0x99));
    kidsPalette.setColor(QPalette::ToolTipBase, QColor(0xFF, 0xFF, 0xFF));
    kidsPalette.setColor(QPalette::ToolTipText, QColor(0x4A, 0x3B, 0x32));
    QGuiApplication::setPalette(kidsPalette);

    auto *settings = new Settings(&app);
    auto *screenTime = new ScreenTime(settings, &app);
    auto *catalog = new AppCatalog(&app);
    auto *launcher = new AppLauncher(settings, &app);
    auto *kiosk = new Kiosk(&app);
    kiosk->setWindowed(windowed);
    auto *speech = new Speech(&app);
    auto *stories = new StoryService(settings, &app);
    auto *eyeCare = new EyeCare(settings, &app);
    auto *weather = new Weather(settings, &app);
    auto *playland = new Playland(&app);
    // 小游戏工坊：一份 H5 编辑器 + 文件桥（图片导入 / 作品导入导出）。
    // 与 Playland 一样依赖 WebEngine；没有时 available=false，入口降级成提示。
    auto *maker = new Maker(&app);
    auto *nes = new NesEmulator(&app);
    // 吉里吉里（KiriKiri）游戏的启动桥：不链引擎，只负责起外部进程。
    // 许可（GPLv2）与平台（Win32）两方面的原因见 core/vnengine.h。
    auto *vn = new VnEngine(&app);
    // 媒体中心（听一听）：QtMultimedia 是可选依赖，没装时 available=false。
    auto *media = new Media(&app);
    // 小小程序员：积木 → C 源码 → 系统 GCC 编译运行。编译器是可选依赖，
    // 没有 gcc 时页面照样能看代码，只是点「运行」会提示怎么装。
    auto *codeLab = new CodeLab(&app);
    // 学习情况上报：关卡进度 + 作业成绩 → 家长后台（自己的故事服务端）。
    // 没填服务器地址时不发任何请求，也不影响孩子做题。
    auto *reports = new LearnReport(settings, codeLab, &app);
    // 联机对战：和同一个小伙伴开一局（华容道竞速 / 国际象棋）。
    // 只认 4 位房间号，没有公开匹配；家长两个开关都打开才会真的发请求。
    auto *netplay = new Netplay(settings, &app);
    // 桌面上的三个工具。都做成单例：kiosk 里一次只可能开一个窗口，
    // 每个页面各 new 一份会让「终端」在切页面时留下孤儿 shell 进程。
    auto *files = new FileModel(&app);
    auto *procs = new ProcModel(&app);
    auto *term = new Terminal(&app);
    // 开机自启 / 崩溃自愈：只写用户目录（XDG autostart 或 systemd --user 单元）。
    auto *startup = new Startup(&app);
    // 系统快捷键抢占：X11 用 XGrabKey，Wayland 诚实降级为「会话裁剪」。
    auto *hotkey = new HotkeyGuard(&app);
    // 用户反馈：意见反馈 / 满意度 / 问卷统一上报官网，断网先存本机队列。
    auto *feedback = new Feedback(&app);

    // 逐个注册为 QML 单例。刻意不用 QML_SINGLETON 宏：
    // 显式注册能让「谁依赖谁」在 main 里一眼看全，也便于将来换实现。
    qmlRegisterSingletonInstance("KidsSafe", 1, 0, "Settings", settings);
    qmlRegisterSingletonInstance("KidsSafe", 1, 0, "ScreenTime", screenTime);
    qmlRegisterSingletonInstance("KidsSafe", 1, 0, "AppCatalog", catalog);
    qmlRegisterSingletonInstance("KidsSafe", 1, 0, "AppLauncher", launcher);
    qmlRegisterSingletonInstance("KidsSafe", 1, 0, "Kiosk", kiosk);
    qmlRegisterSingletonInstance("KidsSafe", 1, 0, "Speech", speech);
    qmlRegisterSingletonInstance("KidsSafe", 1, 0, "Stories", stories);
    qmlRegisterSingletonInstance("KidsSafe", 1, 0, "EyeCare", eyeCare);
    qmlRegisterSingletonInstance("KidsSafe", 1, 0, "Weather", weather);
    qmlRegisterSingletonInstance("KidsSafe", 1, 0, "Playland", playland);
    qmlRegisterSingletonInstance("KidsSafe", 1, 0, "Maker", maker);
    qmlRegisterSingletonInstance("KidsSafe", 1, 0, "Nes", nes);
    qmlRegisterSingletonInstance("KidsSafe", 1, 0, "Vn", vn);
    qmlRegisterSingletonInstance("KidsSafe", 1, 0, "Feedback", feedback);
    qmlRegisterSingletonInstance("KidsSafe", 1, 0, "Media", media);
    qmlRegisterSingletonInstance("KidsSafe", 1, 0, "CodeLab", codeLab);
    qmlRegisterSingletonInstance("KidsSafe", 1, 0, "Reports", reports);
    qmlRegisterSingletonInstance("KidsSafe", 1, 0, "Netplay", netplay);
    qmlRegisterSingletonInstance("KidsSafe", 1, 0, "Files", files);
    qmlRegisterSingletonInstance("KidsSafe", 1, 0, "Processes", procs);
    qmlRegisterSingletonInstance("KidsSafe", 1, 0, "Term", term);
    qmlRegisterSingletonInstance("KidsSafe", 1, 0, "Startup", startup);
    qmlRegisterSingletonInstance("KidsSafe", 1, 0, "HotkeyGuard", hotkey);

    // 不等 QML 加载完再抢：从窗口出现到 QML 就绪有几百毫秒，
    // 这段时间正好够孩子按 Alt+Tab 跑掉。窗口模式下不抢（排障要能切窗口）。
    if (!windowed) {
        hotkey->install();
        qInfo() << "[hotkey]" << hotkey->statusText();
    }

    // 红白机的画面用自定义 Item 画（QQuickPaintedItem），不能像单例那样注册实例，
    // 必须注册成类型让 QML 自己 new。
    qmlRegisterType<NesView>("KidsSafe", 1, 0, "NesView");

    qInfo() << "[app] 启动，模式："
            << (windowed ? "窗口（调试）"
                         : (kiosk->sessionMode() ? "kiosk 会话" : "全屏"));

#ifdef KIDSSAFE_WEBENGINE
    // 网页渲染走的是 GPU 还是软件。排障第一条就看这里：
    // 如果后面仍在刷 gbm_wrapper 报错，先确认 GBM 那项是不是 0。
    qInfo() << "[app] 网页渲染："
            << (qEnvironmentVariableIntValue("KIDSSAFE_WEB_GPU") == 1 ? "硬件加速"
                                                                     : "软件（默认）")
            << "| GBM:" << qEnvironmentVariable("QTWEBENGINE_FORCE_USE_GBM", "(未设置)")
            << "| Chromium flags:"
            << qEnvironmentVariable("QTWEBENGINE_CHROMIUM_FLAGS", "(未设置)");
#endif

    // 故事剧场依赖外部运行时，日志里直接写明挑中了谁 —— 孩子那边只看到
    // 「没有故事」时，家长要能在一行内知道是引擎没装还是目录空的。
    qInfo() << "[app] 吉里吉里："
            << (vn->available() ? vn->backend() : "未找到运行时")
            << "|" << vn->gamesDir();

    // 媒体中心：内容表找到没有、播放器能不能用，一行说清。
    qInfo() << "[app] 媒体中心："
            << (media->available() ? "可播放" : "只读（没有播放器或没有内容）")
            << "|" << media->detail();

    // 小小程序员：有没有编译器、关卡表读没读到，两件事一起说清 ——
    // 这个模块最容易出的问题就是「gcc 没装」或「lessons.json 没跟着装」。
    qInfo() << "[app] 小小程序员："
            << (codeLab->compilerAvailable() ? codeLab->compilerName()
                                             : QStringLiteral("没有编译器，只能看代码"))
            << "| 关卡" << codeLab->lessons().size() << "个";

    QQmlApplicationEngine engine;
    // 主题图标：磁贴里的 image://theme/<图标名> 由它渲染成 QImage。
    // 必须在 load() 之前注册，否则首屏那几个磁贴会先遇到「未知 provider」
    // 而停在 Error 状态。
    engine.addImageProvider(QStringLiteral("theme"), new ThemeIconProvider);
    engine.load(QUrl(QStringLiteral("qrc:/qml/Main.qml")));
    const bool qmlLoaded = !engine.rootObjects().isEmpty();

    if (selfTest) {
        return runSelfTest(settings, catalog, speech, kiosk, stories, playland, weather, nes,
                           styleName, qmlLoaded);
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
