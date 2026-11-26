import QtQuick
import QtQuick.Layouts

/*
 * 桌面上的一扇「窗口」。
 *
 * 不是真的顶层窗口 —— kiosk 会话里没有窗口管理器，开第二个顶层窗口会盖住
 * 整个屏幕、也拖不动。这里是画在桌面页面里的一个卡片，自己实现标题栏拖动、
 * 最大化、关闭，z 序由桌面统一管。
 *
 * 为什么要做成窗口而不是全屏页面：孩子对「软件」的第一印象来自形状。
 * 全屏切换看起来像「换了一个世界」，卡片式的窗口才像「打开了一个东西」，
 * 而且能同时开着终端和文件管理器 —— 一边看文件一边敲命令，这才是电脑。
 */
Item {
    id: win

    property string title: ""
    property string glyph: "folder"
    property color accent: "#4C8BF5"
    property Component page: null
    property bool maximized: false
    property bool activeWindow: true

    /// 窗口在桌面上的「正常」位置和大小，最大化时用它还原
    property real normX: 0
    property real normY: 0
    property real normW: 0
    property real normH: 0

    signal requestClose()
    signal requestActivate()

    // 打开时的入场动画。0.96 → 1 的缩放比从 0 开始更「稳」，
    // 大窗口从小放大有点像东西砸下来。
    opacity: 0
    scale: 0.97

    Component.onCompleted: reveal.start()

    ParallelAnimation {
        id: reveal

        NumberAnimation {
            target: win
            property: "opacity"
            to: 1
            duration: 160
            easing.type: Easing.OutCubic
        }
        NumberAnimation {
            target: win
            property: "scale"
            to: 1
            duration: 200
            easing.type: Easing.OutCubic
        }
    }

    // 阴影：三层递减的圆角矩形。Qt6 的 MultiEffect 要额外装 quickeffects，
    // 而这里只需要「有一点点浮起来」的感觉，不值得为它加一个依赖。
    Rectangle {
        anchors.fill: frame
        anchors.margins: -14
        radius: frame.radius + 14
        color: "#0F000000"
    }

    Rectangle {
        anchors.fill: frame
        anchors.margins: -8
        radius: frame.radius + 8
        color: "#14000000"
    }

    Rectangle {
        id: frame

        anchors.fill: parent
        radius: maximized ? 0 : 18
        color: "#FFFFFF"
        border.width: 1
        border.color: win.activeWindow ? "#E2E8F0" : "#EDF0F4"
        clip: true

        ColumnLayout {
            anchors.fill: parent
            spacing: 0

            // ---- 标题栏 ----
            Rectangle {
                Layout.fillWidth: true
                Layout.preferredHeight: 52
                color: win.activeWindow ? "#FFFFFF" : "#FAFBFC"

                // 顶部一条 3px 的强调色，和桌面图标同色，一眼能认出是谁
                Rectangle {
                    anchors.top: parent.top
                    anchors.left: parent.left
                    anchors.right: parent.right
                    height: 3
                    color: win.accent
                    opacity: win.activeWindow ? 1 : 0.45
                }

                RowLayout {
                    anchors.fill: parent
                    anchors.leftMargin: 16
                    anchors.rightMargin: 10
                    spacing: 10

                    Rectangle {
                        Layout.preferredWidth: 28
                        Layout.preferredHeight: 28
                        radius: 9
                        color: win.accent

                        GlyphIcon {
                            anchors.centerIn: parent
                            name: win.glyph
                            width: 17
                            height: 17
                            color: "white"
                        }
                    }

                    Text {
                        Layout.fillWidth: true
                        text: win.title
                        font.pixelSize: 15
                        font.weight: Font.DemiBold
                        color: "#3D4A57"
                        elide: Text.ElideRight
                    }

                    // 最大化 / 还原
                    Rectangle {
                        Layout.preferredWidth: 30
                        Layout.preferredHeight: 30
                        radius: 9
                        color: maxArea.active ? "#F1F5F9" : "transparent"

                        GlyphIcon {
                            anchors.centerIn: parent
                            name: win.maximized ? "restore" : "maximize"
                            width: 14
                            height: 14
                            color: "#7C8A99"
                        }

                        HoverHandler {
                            id: maxArea
                        }

                        TapHandler {
                            onTapped: {
                                win.requestActivate();
                                win.toggleMaximize();
                            }
                        }
                    }

                    // 关闭
                    Rectangle {
                        Layout.preferredWidth: 30
                        Layout.preferredHeight: 30
                        radius: 9
                        color: closeArea.active ? "#FFE4E1" : "transparent"

                        GlyphIcon {
                            anchors.centerIn: parent
                            name: "close"
                            width: 13
                            height: 13
                            color: closeArea.active ? "#E05252" : "#7C8A99"
                        }

                        HoverHandler {
                            id: closeArea
                        }

                        TapHandler {
                            onTapped: win.requestClose()
                        }
                    }
                }

                // 拖动标题栏。target: null 是必须的 —— 默认会把窗口的 x/y
                // 绑定直接改写掉，最大化还原时位置就回不去了。
                DragHandler {
                    id: drag

                    target: null
                    enabled: !win.maximized

                    property real startX: 0
                    property real startY: 0

                    onActiveChanged: {
                        if (active) {
                            win.requestActivate();
                            startX = win.x;
                            startY = win.y;
                        }
                    }

                    onTranslationChanged: {
                        if (!active) {
                            return;
                        }
                        win.x = startX + translation.x;
                        win.y = startY + translation.y;
                    }
                }

                TapHandler {
                    onTapped: win.requestActivate()
                }
            }

            // ---- 内容 ----
            Loader {
                id: body

                Layout.fillWidth: true
                Layout.fillHeight: true
                sourceComponent: win.page
            }
        }

        // 非活动窗口盖一层极淡的白，模仿「失焦」。不做的话并排三个窗口
        // 分不清哪个能接收键盘。
        Rectangle {
            anchors.fill: parent
            anchors.topMargin: 55
            color: "#FFFFFF"
            opacity: win.activeWindow ? 0 : 0.45
            visible: opacity > 0

            TapHandler {
                onTapped: win.requestActivate()
            }
        }
    }

    function toggleMaximize() {
        if (maximized) {
            maximized = false;
            x = normX;
            y = normY;
            width = normW;
            height = normH;
        } else {
            normX = x;
            normY = y;
            normW = width;
            normH = height;
            maximized = true;
            x = 0;
            y = 0;
            width = parent.width;
            height = parent.height;
        }
    }
}
