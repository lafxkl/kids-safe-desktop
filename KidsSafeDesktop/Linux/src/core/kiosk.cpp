#include "kiosk.h"

#include <QCoreApplication>
#include <QDateTime>
#include <QDebug>
#include <QDir>
#include <QFile>
#include <QGuiApplication>
#include <QScreen>
#include <QStandardPaths>

namespace {
/// 标记文件：存在即表示"上次是 kiosk 会话中途被外力结束的"。
/// 作用与 Windows 版的 .taskbar_hidden_by_kids 一致，但 Linux 下它的意义
/// 从"恢复任务栏"变成"下次启动时提示家长：上次异常退出，要不要跳过锁定"。
QString markerPath()
{
    const QString dir = QStandardPaths::writableLocation(QStandardPaths::AppDataLocation);
    return dir + QStringLiteral("/.kiosk_active");
}

QString sessionExitPath()
{
    const QString dir = QStandardPaths::writableLocation(QStandardPaths::AppDataLocation);
    return dir + QStringLiteral("/.session_exit");
}
}

Kiosk::Kiosk(QObject *parent)
    : QObject(parent)
{
    QDir().mkpath(QStandardPaths::writableLocation(QStandardPaths::AppDataLocation));
}

bool Kiosk::sessionMode() const
{
    return qEnvironmentVariableIntValue("KIDSSAFE_KIOSK_SESSION") == 1;
}

bool Kiosk::windowed() const
{
    return m_windowed;
}

void Kiosk::setWindowed(bool on)
{
    if (m_windowed == on) {
        return;
    }
    m_windowed = on;
    Q_EMIT windowedChanged();
}

void Kiosk::attach(QWindow *window)
{
    if (!window) {
        return;
    }
    m_window = window;

    // 调试模式（kidssafe --windowed）：什么锁定手段都不上，就是一个普通窗口。
    // 全屏无边框的窗口切不到终端，排障时等于蒙着眼改代码。
    if (m_windowed) {
        window->setFlag(Qt::FramelessWindowHint, false);
        window->setFlag(Qt::WindowStaysOnTopHint, false);
        window->resize(1180, 800);
        window->show();
        window->requestActivate();
        qInfo() << "[kiosk] 窗口模式（调试用，不做全屏/置顶/锁定标记）";
        return;
    }

    // 无边框 + 全屏。置顶只在非会话模式下有意义（会话模式本来就没有别的窗口），
    // 但两种模式都设上，代价为零。
    window->setFlags(window->flags() | Qt::FramelessWindowHint);
    window->setFlag(Qt::WindowStaysOnTopHint, true);
    window->showFullScreen();
    window->requestActivate();

    // 「全屏」在 X11 上只是一个发给窗口管理器的请求（_NET_WM_STATE_FULLSCREEN），
    // 而 kiosk 会话刻意不启动 kwin / plasmashell —— 没有 WM，就没有人执行它，
    // 窗口会停在 QML 里写的 1280×800，贴在左上角，其余屏幕是黑色的根窗口。
    // QML 那边已经把宽高绑到 Screen.width/height，这里再按屏幕几何兜一次底
    // （同时负责「分辨率变了要跟着变」）。
    QScreen *screen = window->screen() ? window->screen() : QGuiApplication::primaryScreen();
    if (screen) {
        const QRect geometry = screen->geometry();
        if (window->geometry() != geometry) {
            qInfo() << "[kiosk] 窗口没铺满（多半是没有窗口管理器），按屏幕几何对齐"
                    << geometry;
            window->setGeometry(geometry);
        }
        // 以窗口为 context：窗口销毁时连接自动断开，不会留下悬空 lambda。
        QObject::connect(screen, &QScreen::geometryChanged, window,
                         [window](const QRect &rect) { window->setGeometry(rect); });
    }

    writeMarker();
}

void Kiosk::toFront()
{
    if (!m_window) {
        return;
    }
    if (m_windowed) {
        m_window->show();
    } else {
        m_window->showFullScreen();
    }
    m_window->raise();
    m_window->requestActivate();
}

void Kiosk::quit()
{
    qInfo() << "[kiosk] 家长确认退出，结束进程";

    // 先写退出标记，会话脚本据此判断"这是家长主动退出，不要重启"。
    QFile marker(sessionExitPath());
    if (marker.open(QIODevice::WriteOnly)) {
        marker.write(QDateTime::currentDateTime().toString(Qt::ISODate).toUtf8());
        marker.close();
    }
    clearMarker();

    // 先把窗口收起来再退。这家机器上仅靠 quit() 有时看不到任何变化：
    // 无边框 + 置顶的窗口在某些合成器上会留下最后一帧，看起来像"点了没反应"。
    // 显式 hide + 处理一次事件，再 exit()，屏幕上是立刻消失的。
    if (m_window) {
        m_window->hide();
    }
    QCoreApplication::processEvents();
    QCoreApplication::exit(0);
}

void Kiosk::panicRestore()
{
    clearMarker();
    if (m_window) {
        m_window->setFlag(Qt::WindowStaysOnTopHint, false);
        m_window->setFlag(Qt::FramelessWindowHint, false);
        m_window->showNormal();
    }
}

void Kiosk::writeMarker()
{
    QFile marker(markerPath());
    if (marker.open(QIODevice::WriteOnly)) {
        marker.write(QDateTime::currentDateTime().toString(Qt::ISODate).toUtf8());
    }
}

void Kiosk::clearMarker()
{
    QFile::remove(markerPath());
}
