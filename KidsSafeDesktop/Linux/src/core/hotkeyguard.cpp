#include "hotkeyguard.h"

#include <QDebug>
#include <QGuiApplication>
#include <QSocketNotifier>

#ifdef KIDSSAFE_X11
#include <X11/Xlib.h>
#include <X11/keysym.h>
#endif

namespace {

#ifdef KIDSSAFE_X11
struct Combo
{
    const char *label;
    unsigned int keysym;
    unsigned int mods;
};

/// 要抢的组合键。这里是「孩子能用来跑出儿童桌面」的那几个，
/// 不是把整个键盘都禁掉——禁整键盘会连本应用的输入一起废掉。
const Combo kCombos[] = {
    { "Alt+Tab", XK_Tab, Mod1Mask },
    { "Alt+Shift+Tab", XK_Tab, Mod1Mask | ShiftMask },
    { "Alt+F4", XK_F4, Mod1Mask },
    { "Alt+Esc", XK_Escape, Mod1Mask },
    { "Ctrl+Esc", XK_Escape, ControlMask },
    { "Ctrl+Shift+Esc", XK_Escape, ControlMask | ShiftMask },
    { "Ctrl+Alt+Tab", XK_Tab, ControlMask | Mod1Mask },
    { "Super", XK_Super_L, 0 },
    { "Super", XK_Super_R, 0 },
};

/// 抢的过程中会遇到 BadAccess（别的客户端已经抓了这组键）。
/// Xlib 默认的错误处理是「打印一行然后 exit()」——不接管的话，
/// 在普通桌面里点一下「屏蔽快捷键」进程就直接没了。
int g_lastXError = 0;
int quietXErrorHandler(Display *, XErrorEvent *event)
{
    g_lastXError = event->error_code;
    return 0;
}
#endif

}

HotkeyGuard::HotkeyGuard(QObject *parent)
    : QObject(parent)
{
}

HotkeyGuard::~HotkeyGuard()
{
    uninstall();
}

QString HotkeyGuard::statusText() const
{
    if (m_enabled) {
        return QStringLiteral("已抢占 %1 个组合键（%2）：Alt+Tab / Alt+F4 / Alt+Esc / Ctrl+Esc / Ctrl+Shift+Esc / Super")
            .arg(m_combos.size())
            .arg(m_method);
    }
    if (!m_error.isEmpty()) {
        return m_error;
    }
    if (m_method == QStringLiteral("会话裁剪")) {
        return QStringLiteral("Wayland 不允许应用抢占全局按键：本会话已不加载面板与窗口管理器快捷键，"
                              "Alt+Tab 没有可切换的对象");
    }
    return QStringLiteral("未启用快捷键抢占");
}

void HotkeyGuard::setMethod(const QString &method)
{
    if (m_method == method) {
        return;
    }
    m_method = method;
    Q_EMIT changed();
}

void HotkeyGuard::refreshStatus()
{
    Q_EMIT changed();
}

bool HotkeyGuard::install()
{
    uninstall();
    m_error.clear();

    // 调试窗口模式（kidssafe --windowed）下不抢：排障时要能切窗口看日志。
    if (qEnvironmentVariableIntValue("KIDSSAFE_NO_HOTKEY_GUARD") == 1) {
        m_available = false;
        m_error = QStringLiteral("已通过 KIDSSAFE_NO_HOTKEY_GUARD=1 关闭抢占（排障用）");
        refreshStatus();
        return false;
    }

    const QString platform = QGuiApplication::platformName();
    if (platform.contains(QStringLiteral("wayland"), Qt::CaseInsensitive)) {
        // Wayland 没有可用的应用级抢占；把手段如实标成「会话裁剪」，
        // 家长页就不会出现「开关是开的但其实什么都没做」的假象。
        m_available = false;
        m_method = QStringLiteral("会话裁剪");
        m_enabled = false;
        refreshStatus();
        qInfo() << "[hotkey] Wayland：不做按键抢占，依赖会话裁剪";
        return false;
    }

#ifdef KIDSSAFE_X11
    Display *dpy = XOpenDisplay(nullptr);
    if (!dpy) {
        m_available = false;
        m_error = QStringLiteral("抢不到：连不上 X 服务器（DISPLAY 未设置？）");
        setMethod(QStringLiteral("不可用"));
        refreshStatus();
        qWarning() << "[hotkey]" << m_error;
        return false;
    }

    const Window root = DefaultRootWindow(dpy);
    const int numLock = XKeysymToKeycode(dpy, XK_Num_Lock) == 0 ? 0 : Mod2Mask;
    const int capsLock = LockMask;

    auto oldHandler = XSetErrorHandler(quietXErrorHandler);

    QStringList grabbed;
    for (const Combo &combo : kCombos) {
        const KeyCode code = XKeysymToKeycode(dpy, static_cast<KeySym>(combo.keysym));
        if (code == 0) {
            continue;
        }

        // 必须把 NumLock / CapsLock 的组合一起抢：开着大写锁时修饰符里多一个 LockMask，
        // 只抢「干净」的那一组会漏，孩子按一下 CapsLock 就绕过去了。
        const unsigned int variants[] = {
            combo.mods,
            combo.mods | static_cast<unsigned int>(numLock),
            combo.mods | static_cast<unsigned int>(capsLock),
            combo.mods | static_cast<unsigned int>(numLock) | static_cast<unsigned int>(capsLock),
        };

        bool any = false;
        for (unsigned int mods : variants) {
            g_lastXError = 0;
            XGrabKey(dpy, code, mods, root, True, GrabModeAsync, GrabModeAsync);
            XSync(dpy, False);
            if (g_lastXError == 0) {
                any = true;
            }
        }
        if (any) {
            grabbed << QString::fromLatin1(combo.label);
        } else {
            // 常见于「普通桌面会话」：Alt+Tab 已经被 KWin/Mutter 抓走了。
            qInfo() << "[hotkey] 抢不到" << combo.label << "（已被窗口管理器占用）";
        }
    }

    XSetErrorHandler(oldHandler);
    XSync(dpy, False);

    m_display = dpy;

    // 抓到手的事件会堆在这条连接上。不读的话缓冲区满了 X 会把我们当成
    // 「不响应的客户端」直接断连，抓取也一并失效。用 QSocketNotifier 排空。
    m_notifier = new QSocketNotifier(ConnectionNumber(dpy), QSocketNotifier::Read, this);
    connect(m_notifier, &QSocketNotifier::activated, this, [this]() {
        auto *dpy2 = static_cast<Display *>(m_display);
        if (!dpy2) {
            return;
        }
        while (XPending(dpy2) > 0) {
            XEvent event;
            XNextEvent(dpy2, &event);
            if (event.type == KeyPress) {
                // 只报告，不转发：事件到此为止，别的窗口收不到。
                Q_EMIT blocked(QStringLiteral("keycode %1").arg(event.xkey.keycode));
            }
        }
    });

    m_combos = grabbed;
    m_enabled = !grabbed.isEmpty();
    m_available = true;
    setMethod(QStringLiteral("X11 全局抢占"));
    refreshStatus();

    qInfo() << "[hotkey] 已抢占" << grabbed.size() << "个组合键：" << grabbed;
    return m_enabled;
#else
    // 编译时没找到 libX11（没装 *-dev 包）：只能走会话裁剪。
    m_available = false;
    m_enabled = false;
    setMethod(QStringLiteral("会话裁剪"));
    m_error = QStringLiteral("这个构建没有链接 X11，无法抢占按键；锁定依赖会话裁剪");
    refreshStatus();
    qWarning() << "[hotkey]" << m_error;
    return false;
#endif
}

void HotkeyGuard::uninstall()
{
    if (m_notifier) {
        m_notifier->setEnabled(false);
        m_notifier->deleteLater();
        m_notifier = nullptr;
    }

#ifdef KIDSSAFE_X11
    if (m_display) {
        auto *dpy = static_cast<Display *>(m_display);
        const Window root = DefaultRootWindow(dpy);
        const int numLock = XKeysymToKeycode(dpy, XK_Num_Lock) == 0 ? 0 : Mod2Mask;

        for (const Combo &combo : kCombos) {
            const KeyCode code = XKeysymToKeycode(dpy, static_cast<KeySym>(combo.keysym));
            if (code == 0) {
                continue;
            }
            const unsigned int variants[] = {
                combo.mods,
                combo.mods | static_cast<unsigned int>(numLock),
                combo.mods | static_cast<unsigned int>(LockMask),
                combo.mods | static_cast<unsigned int>(numLock) | static_cast<unsigned int>(LockMask),
            };
            for (unsigned int mods : variants) {
                XUngrabKey(dpy, code, mods, root);
            }
        }
        XSync(dpy, False);
        XCloseDisplay(dpy);
        m_display = nullptr;
    }
#endif

    if (m_enabled) {
        qInfo() << "[hotkey] 已解除抢占";
    }
    m_enabled = false;
    m_combos.clear();
    refreshStatus();
}
