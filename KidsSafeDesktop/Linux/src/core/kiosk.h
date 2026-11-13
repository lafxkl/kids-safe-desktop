#pragma once

#include <QObject>
#include <QPointer>
#include <QWindow>

/**
 * 对应 C# 的 IKioskService。
 *
 * ⚠️ 这里是 Linux 与 Windows 最大的语义差异，务必读完再改：
 *
 * Windows 版能 SetWindowsHookEx 全局屏蔽 Win/Alt+Tab/Alt+F4，还能 FindWindow
 * 把任务栏 ShowWindow(SW_HIDE)。Wayland 下这些统统做不到——普通应用没有
 * 拦截全局按键、强制置顶、枚举他人窗口的权限，这是协议层面的限制，不是
 * 缺 API。想在应用内部"锁死"系统是一厢情愿。
 *
 * 所以 Linux 版的锁定策略分两层：
 *   第一层（真正的锁）——会话层：由 kidssafe-session 脚本以独立 Wayland/X11
 *   会话启动本应用，不加载 plasmashell，不放全局快捷键服务。此时根本没有
 *   "别的窗口"可切换，Alt+Tab 落到 KWin 也无可切换对象。这是锁得住的部分。
 *   第二层（体验层）——本类负责：无边框全屏、置顶、拒绝关闭事件、
 *   崩溃后由会话脚本自动重启（守护进程式）。
 *
 * 结论：不要在 Linux 侧试图复刻 Windows 的按键钩子。可靠性来自会话裁剪，
 * 不来自应用内的小动作。
 */
class Kiosk : public QObject
{
    Q_OBJECT

    /// 是否运行在专属 kiosk 会话里（由会话脚本设置环境变量）。
    Q_PROPERTY(bool sessionMode READ sessionMode CONSTANT)

public:
    explicit Kiosk(QObject *parent = nullptr);

    bool sessionMode() const;

    /// 由 QML 在启动时调用，接管主窗口。幂等。
    Q_INVOKABLE void attach(QWindow *window);

    /// 把儿童桌面拉回前台（外部应用退出后调用）。
    Q_INVOKABLE void toFront();

    /// 家长正常退出：清标记后退出进程。会话脚本看到标记会结束整个会话。
    Q_INVOKABLE void quit();

    /// 紧急恢复：清除锁定标记并退出。帮助页/故障时用。
    Q_INVOKABLE void panicRestore();

private:
    void writeMarker();
    void clearMarker();

    QPointer<QWindow> m_window;
};
