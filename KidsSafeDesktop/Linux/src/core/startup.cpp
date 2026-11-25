#include "startup.h"

#include <QCoreApplication>
#include <QDebug>
#include <QDir>
#include <QFile>
#include <QFileInfo>
#include <QProcess>
#include <QStandardPaths>
#include <QTextStream>

namespace {
/// systemd 用户单元名。改名字要同步改 uninstall 说明与文档。
const char *kUnitName = "kidssafe-guard.service";

QString configHome()
{
    // XDG_CONFIG_HOME 在 kde/gnome 下一般就是 ~/.config，但按规范读环境变量，
    // 免得容器或自定义 $HOME 的场景写到别处去。
    QString dir = QString::fromLocal8Bit(qgetenv("XDG_CONFIG_HOME"));
    if (dir.isEmpty()) {
        dir = QStandardPaths::writableLocation(QStandardPaths::GenericConfigLocation);
    }
    if (dir.isEmpty()) {
        dir = QDir::homePath() + QStringLiteral("/.config");
    }
    return dir;
}
}

Startup::Startup(QObject *parent)
    : QObject(parent)
{
}

QString Startup::autostartFile() const
{
    return configHome() + QStringLiteral("/autostart/kidssafe.desktop");
}

QString Startup::unitFile() const
{
    return configHome() + QStringLiteral("/systemd/user/") + QString::fromLatin1(kUnitName);
}

QString Startup::appBinary() const
{
    // 用当前可执行文件的绝对路径，而不是写死 /usr/bin/kidssafe：
    // 这套东西在 deepin 上装到 /usr/local、在 Arch 上装到 /usr/bin，
    // 用户也可能直接跑 build 目录里的二进制。
    return QCoreApplication::applicationFilePath();
}

bool Startup::autostartEnabled() const
{
    return QFileInfo::exists(autostartFile());
}

bool Startup::watchdogEnabled() const
{
    if (!QFileInfo::exists(unitFile())) {
        return false;
    }

    // 文件在，但可能被 `systemctl --user disable` 掉了（.wants 里的软链是真正的开关）。
    // 没有 systemctl 时只能以文件为准，够用。
    if (!systemdAvailable()) {
        return true;
    }
    QProcess p;
    p.start(QStringLiteral("systemctl"),
            { QStringLiteral("--user"), QStringLiteral("is-enabled"),
              QString::fromLatin1(kUnitName) });
    if (!p.waitForFinished(3000)) {
        return true;
    }
    const QString out = QString::fromUtf8(p.readAllStandardOutput()).trimmed();
    return out == QStringLiteral("enabled") || out == QStringLiteral("enabled-runtime");
}

bool Startup::systemdAvailable() const
{
    // 有 systemctl 还不够：容器里常见「有二进制但没有用户实例」，
    // 直接 `systemctl --user` 会报 "Failed to connect to bus"。
    // 判据取 DBUS_SESSION_BUS_ADDRESS + 二进制存在，够准确又不起进程。
    if (qEnvironmentVariableIsEmpty("DBUS_SESSION_BUS_ADDRESS")) {
        return false;
    }
    return !QStandardPaths::findExecutable(QStringLiteral("systemctl")).isEmpty();
}

QString Startup::statusText() const
{
    if (!m_status.isEmpty()) {
        return m_status;
    }
    if (watchdogEnabled()) {
        return QStringLiteral("守护已开启：进程被结束或崩溃后 2 秒自动重启（systemd 用户单元）");
    }
    if (autostartEnabled()) {
        return QStringLiteral("登录自启已开启：下次登录自动进入儿童桌面（无崩溃重启）");
    }
    return QStringLiteral("未配置：不影响本次会话，只是重启/退出后不会自动回来");
}

void Startup::setStatus(const QString &text)
{
    m_status = text;
    Q_EMIT changed();
}

void Startup::refresh()
{
    m_status.clear();
    Q_EMIT changed();
}

bool Startup::systemctl(const QStringList &args, int timeoutMs) const
{
    QProcess p;
    p.start(QStringLiteral("systemctl"), args);
    if (!p.waitForFinished(timeoutMs)) {
        p.kill();
        qWarning() << "[startup] systemctl 超时：" << args;
        return false;
    }
    if (p.exitCode() != 0) {
        qWarning() << "[startup] systemctl 失败：" << args
                   << QString::fromUtf8(p.readAllStandardError()).trimmed();
        return false;
    }
    return true;
}

bool Startup::setAutostart(bool on)
{
    m_status.clear();

    if (!on) {
        if (QFileInfo::exists(autostartFile()) && !QFile::remove(autostartFile())) {
            setStatus(QStringLiteral("关闭失败：删不掉 %1（权限？）").arg(autostartFile()));
            return false;
        }
        Q_EMIT changed();
        return true;
    }

    // 互斥：守护已经负责启动，再加一条 XDG 自启会起来两份（两个全屏窗口抢焦点，
    // 表现是画面不停闪）。先关守护，再写自启文件。
    if (watchdogEnabled()) {
        setWatchdog(false);
    }

    const QString path = autostartFile();
    QDir().mkpath(QFileInfo(path).absolutePath());

    QFile f(path);
    if (!f.open(QIODevice::WriteOnly | QIODevice::Truncate | QIODevice::Text)) {
        setStatus(QStringLiteral("开启失败：写不了 %1").arg(path));
        return false;
    }

    QTextStream ts(&f);
    // 不写 OnlyShowIn：KDE / GNOME / XFCE / DDE 都读同一份 XDG autostart。
    // X-GNOME-Autostart-enabled 是 GNOME 的历史遗留键，留着对别的桌面无害。
    ts << "[Desktop Entry]\n"
       << "Type=Application\n"
       << "Name=儿童安全桌面\n"
       << "Name[en]=KidsSafe Desktop\n"
       << "Comment=登录后自动进入儿童桌面\n"
       << "Exec=\"" << appBinary() << "\"\n"
       << "Icon=kidssafe\n"
       << "Terminal=false\n"
       << "X-GNOME-Autostart-enabled=true\n"
       << "X-KDE-autostart-after=panel\n";
    f.close();

    setStatus(QStringLiteral("已开启登录自启：下次登录自动进入儿童桌面"));
    return true;
}

bool Startup::setWatchdog(bool on)
{
    m_status.clear();

    if (!on) {
        const bool hadUnit = QFileInfo::exists(unitFile());
        if (hadUnit) {
            if (systemdAvailable()) {
                systemctl({ QStringLiteral("--user"), QStringLiteral("disable"),
                            QString::fromLatin1(kUnitName) });
            }
            QFile::remove(unitFile());
            if (systemdAvailable()) {
                systemctl({ QStringLiteral("--user"), QStringLiteral("daemon-reload") });
            }
        }
        setStatus(QStringLiteral("已关闭守护"));
        Q_EMIT changed();
        return true;
    }

    if (!systemdAvailable()) {
        // 没有 systemd 就退化成登录自启，并把原因说清楚——不要让家长以为
        // 「开关打开了就一定有守护」。
        const bool ok = setAutostart(true);
        setStatus(ok
                      ? QStringLiteral("这台机器没有可用的 systemd 用户实例，已退回「登录自启」（无崩溃重启）")
                      : QStringLiteral("开启失败：既没有 systemd，也写不了自启文件"));
        return ok;
    }

    // 互斥：见 setAutostart 的说明。
    if (QFileInfo::exists(autostartFile())) {
        QFile::remove(autostartFile());
    }

    const QString path = unitFile();
    QDir().mkpath(QFileInfo(path).absolutePath());

    QFile f(path);
    if (!f.open(QIODevice::WriteOnly | QIODevice::Truncate | QIODevice::Text)) {
        setStatus(QStringLiteral("开启失败：写不了 %1").arg(path));
        return false;
    }

    QTextStream ts(&f);
    ts << "[Unit]\n"
       << "Description=儿童安全桌面（守护）\n"
       // StartLimitIntervalSec=0：默认策略是「10 秒内失败 5 次就彻底放弃」，
       // 对儿童桌面来说等于「崩几次守护就没了」。宁可慢一点也要一直重启。
       << "StartLimitIntervalSec=0\n"
       << "After=graphical-session.target\n"
       << "\n"
       << "[Service]\n"
       << "Type=simple\n"
       << "ExecStart=\"" << appBinary() << "\"\n"
       << "Restart=always\n"
       << "RestartSec=2\n"
       // 只给必要环境：会话总线与显示变量由 systemd 用户实例继承，
       // 这里补上 Wayland/X11 的常见变量，避免在会话外启动时找不到显示。
       << "Environment=KIDSSAFE_KIOSK_SESSION=1\n"
       << "\n"
       << "[Install]\n"
       << "WantedBy=default.target\n";
    f.close();

    systemctl({ QStringLiteral("--user"), QStringLiteral("daemon-reload") });
    // 只 enable 不 --now：现在启动会多起一个实例（当前这个还活着）。
    // 本次会话的守护由 kidssafe-session 的循环负责，下次登录交给 systemd。
    const bool ok = systemctl({ QStringLiteral("--user"), QStringLiteral("enable"),
                                QString::fromLatin1(kUnitName) });

    setStatus(ok
                  ? QStringLiteral("已开启守护：下次登录由 systemd 拉起，被结束后 2 秒自动重启")
                  : QStringLiteral("单元已写入，但启用失败（看 journalctl --user -u kidssafe-guard）"));
    return ok;
}
