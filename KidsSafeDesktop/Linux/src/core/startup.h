#pragma once

#include <QObject>
#include <QString>

/**
 * 开机自启与「被结束后自动回来」（对应 Windows 的 --kiosk-guard、安卓的前台服务守护）。
 *
 * 两种落地方式，按可用性选择，**互斥**——同时开会起来两个实例：
 *
 *   A) XDG 自启（所有桌面环境都认）
 *      ~/.config/autostart/kidssafe.desktop
 *      登录后由桌面环境拉起一次。没有守护能力：进程被结束就结束了。
 *
 *   B) systemd 用户单元（有 systemd 的发行版）
 *      ~/.config/systemd/user/kidssafe-guard.service，Restart=always
 *      被 kill / 崩溃后 2 秒自动重启，等价于 Windows 的守护进程。
 *      开这一项时会顺手删掉 A 的文件，避免登录时被拉起两份。
 *
 * 两者都只写用户目录，不需要 root，也不会污染系统级配置——卸载残留最多是
 * 两个文本文件，家长删掉即恢复原状。
 */
class Startup : public QObject
{
    Q_OBJECT

    /// 是否已配置登录自启（XDG autostart 文件存在）。
    Q_PROPERTY(bool autostartEnabled READ autostartEnabled NOTIFY changed)

    /// 是否已配置 systemd 用户单元守护（文件存在且已 enable）。
    Q_PROPERTY(bool watchdogEnabled READ watchdogEnabled NOTIFY changed)

    /// 这台机器上 systemctl --user 是否可用（没有 systemd 的发行版为 false）。
    Q_PROPERTY(bool systemdAvailable READ systemdAvailable CONSTANT)

    /// 卡片上那行状态文案，含失败原因。
    Q_PROPERTY(QString statusText READ statusText NOTIFY changed)

public:
    explicit Startup(QObject *parent = nullptr);

    bool autostartEnabled() const;
    bool watchdogEnabled() const;
    bool systemdAvailable() const;
    QString statusText() const;

    /// 开/关登录自启。开启时若守护已开，会先关掉守护（互斥）。
    Q_INVOKABLE bool setAutostart(bool on);

    /// 开/关 systemd 守护。开启时若 XDG 自启已开，会删掉它（互斥）。
    Q_INVOKABLE bool setWatchdog(bool on);

    /// 重新探测状态（外部改了文件后调用）。
    Q_INVOKABLE void refresh();

Q_SIGNALS:
    void changed();

private:
    QString autostartFile() const;
    QString unitFile() const;
    QString appBinary() const;
    bool systemctl(const QStringList &args, int timeoutMs = 5000) const;
    void setStatus(const QString &text);

    QString m_status;
};
