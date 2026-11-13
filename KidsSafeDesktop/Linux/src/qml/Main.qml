import QtQuick
import QtQuick.Controls
import org.kde.kirigami as Kirigami

/*
 * 应用根窗口。
 *
 * 全屏、无边框、拒绝关闭：这三个是「体验层」的锁定手段，真正的锁在
 * kidssafe-session 会话脚本里（不启动 plasmashell、不注册全局快捷键）。
 * 详见 src/core/kiosk.h 的类注释。
 */
Kirigami.ApplicationWindow {
    id: root

    width: 1280
    height: 800
    minimumWidth: 800
    minimumHeight: 560
    visibility: Window.FullScreen
    title: "儿童安全桌面"
    color: "#FFF8EE"

    // 家长只能用「退出儿童桌面」按钮走正常流程，不允许 Alt+F4 / 关闭按钮关掉。
    // 注：Wayland 下这个拦截只对应用自己发出的 close 生效；窗口管理器层面的
    // 关闭请求由会话脚本守护重启兜底。
    onClosing: function (close) {
        close.accepted = false
    }

    StackView {
        id: nav
        anchors.fill: parent
        initialItem: "SplashPage.qml"

        pushEnter: Transition {
            NumberAnimation { property: "opacity"; from: 0; to: 1; duration: 220; easing.type: Easing.OutCubic }
            NumberAnimation { property: "scale"; from: 0.96; to: 1; duration: 220; easing.type: Easing.OutCubic }
        }
        pushExit: Transition {
            NumberAnimation { property: "opacity"; from: 1; to: 0.6; duration: 180 }
        }
        popEnter: Transition {
            NumberAnimation { property: "opacity"; from: 0.6; to: 1; duration: 180 }
        }
        popExit: Transition {
            NumberAnimation { property: "opacity"; from: 1; to: 0; duration: 180 }
        }
    }
}
