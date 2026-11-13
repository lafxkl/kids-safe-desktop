#include "kiosk.h"

#include <QCoreApplication>
#include <QDateTime>
#include <QDir>
#include <QFile>
#include <QGuiApplication>
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

void Kiosk::attach(QWindow *window)
{
    if (!window) {
        return;
    }
    m_window = window;

    // 无边框 + 全屏。置顶只在非会话模式下有意义（会话模式本来就没有别的窗口），
    // 但两种模式都设上，代价为零。
    window->setFlags(window->flags() | Qt::FramelessWindowHint);
    window->setFlag(Qt::WindowStaysOnTopHint, true);
    window->showFullScreen();
    window->requestActivate();

    writeMarker();
}

void Kiosk::toFront()
{
    if (!m_window) {
        return;
    }
    m_window->showFullScreen();
    m_window->raise();
    m_window->requestActivate();
}

void Kiosk::quit()
{
    // 先写退出标记，会话脚本据此判断"这是家长主动退出，不要重启"。
    QFile marker(sessionExitPath());
    if (marker.open(QIODevice::WriteOnly)) {
        marker.write(QDateTime::currentDateTime().toString(Qt::ISODate).toUtf8());
        marker.close();
    }
    clearMarker();
    QCoreApplication::quit();
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
