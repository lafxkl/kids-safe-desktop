#pragma once

#include <QObject>
#include <QString>
#include <QStringList>

class QSocketNotifier;

/**
 * 抢占系统快捷键（对应 Windows 的 SetWindowsHookEx、安卓的无障碍拦截）。
 *
 * ⚠️ 先读这段，别对 Linux 抱有不切实际的期待：
 *
 *   · **X11**：可以抢。XGrabKey 把指定组合键**独占**过来，事件只投递给本进程，
 *     窗口管理器再也收不到，于是 Alt+Tab / Alt+F4 / Ctrl+Shift+Esc 全部失效。
 *     前提是这组键还没被别人抓走——正常桌面里的 Alt+Tab 是由 KWin/Mutter
 *     先 grab 的，我们抢不到（BadAccess）。所以这招在「没有窗口管理器的
 *     kiosk 会话」里最有效，那时本来也没人跟我们抢。
 *
 *   · **Wayland**：不行。协议里就没有「应用抢占全局快捷键」这个能力，
 *     连拿到全局按键都做不到。Wayland 下的锁定只能靠会话裁剪
 *     （kidssafe-wayland-session 里不启 plasmashell、关掉 KWin 的总览手势）。
 *
 *   · **VT 切换（Ctrl+Alt+F1..F12）**：X 服务器自己处理，XGrabKey 抢不到。
 *     要堵住只能在 xorg.conf 里写 `Option "DontVTSwitch" "true"`（需要 root），
 *     见 Docs/开机自启与快捷键拦截.md。
 *
 * 抢到之后事件被**丢弃**（不转发给任何窗口），所以孩子按键完全没反应；
 * 本应用自己的按键不受影响（那是窗口内事件，不用全局 grab）。
 */
class HotkeyGuard : public QObject
{
    Q_OBJECT

    /// 是否正在抢占（安装成功才算）。
    Q_PROPERTY(bool enabled READ enabled NOTIFY changed)

    /// 当前平台是否具备抢占能力（Wayland / 没有 X11 时为 false）。
    Q_PROPERTY(bool available READ available NOTIFY changed)

    /// 实际生效的手段，用于在家长页如实显示：「X11 全局抢占」/「会话裁剪」。
    Q_PROPERTY(QString method READ method NOTIFY changed)

    /// 已抢占的组合键列表（给人看）。
    Q_PROPERTY(QStringList blockedCombos READ blockedCombos NOTIFY changed)

    /// 一行状态说明（含失败原因）。
    Q_PROPERTY(QString statusText READ statusText NOTIFY changed)

public:
    explicit HotkeyGuard(QObject *parent = nullptr);
    ~HotkeyGuard() override;

    bool enabled() const { return m_enabled; }
    bool available() const { return m_available; }
    QString method() const { return m_method; }
    QStringList blockedCombos() const { return m_combos; }
    QString statusText() const;

    /// 安装抢占。返回是否真的抢到了。
    Q_INVOKABLE bool install();
    /// 解除抢占（退出 kiosk 前必须调用）。
    Q_INVOKABLE void uninstall();

Q_SIGNALS:
    void changed();
    /// 有被拦截的按键（组合键名）。可用于日志/统计。
    void blocked(const QString &combo);

private:
    void setMethod(const QString &method);
    void refreshStatus();

    bool m_enabled = false;
    bool m_available = false;
    QString m_method;
    QStringList m_combos;
    QString m_error;

    // X11 的句柄刻意用 void*：头文件里不引 Xlib，
    // 找不到 X11 开发包时这个类仍然能编译（只是 available 永远 false）。
    void *m_display = nullptr;
    QSocketNotifier *m_notifier = nullptr;
};
