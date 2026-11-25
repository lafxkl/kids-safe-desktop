import QtQuick
import QtQuick.Layouts
import QtQuick.Window
import KidsSafe 1.0

/*
 * 故事剧场（吉里吉里 / KiriKiri 2 游戏）。
 *
 * 与红白机最大的不同：模拟器核心是**外部的独立进程**，不是链进来的库。
 *   1. krkr2 是 Win32 代码（GDI 画图、DirectSound 出声），编不进 Qt；
 *   2. 它是 GPLv2，链进来会让整个程序被迫 GPL，独立进程调用就没这问题。
 * 所以这里的职责只有三件：列游戏、起进程、游戏退出后把窗口收回来。
 *
 * 窗口怎么让位（kiosk 会话里没有窗口管理器，这点很关键）：
 *   · 窗口模式（--windowed）：直接 hide()，游戏退出后 showNormal()，方便看终端；
 *   · kiosk 模式：只 lower() 自己，让游戏的窗口压在上面。
 *     用 lower() 而不是 hide() 是故意的 —— 万一引擎根本没画出窗口
 *     （缺插件、显卡不支持），我们的界面还在下面，孩子不会面对一片黑屏
 *     无路可走；上面那层「故事正在播放」也能点「结束游戏」退出来。
 */
GameShell {
    id: page

    readonly property bool playing: Vn.running

    title: "故事剧场"
    glyph: "moon"
    accent: "#7E57C2"
    prompt: Vn.available
            ? "点一个故事开始，游戏会把整块屏幕接过去"
            : "需要一个吉里吉里运行时，家长按下面的提示装一下"

    focus: true
    Component.onCompleted: forceActiveFocus()

    // 页面被弹掉时游戏还在跑，会留下一个占屏的孤儿窗口 —— 顺手收掉。
    Component.onDestruction: {
        if (Vn.running) {
            Vn.stop();
            restoreWindow();
        }
    }

    function restoreWindow() {
        const win = page.navWin;
        if (!win) {
            return;
        }
        if (Kiosk.windowed) {
            if (!win.visible) {
                win.showNormal();
            }
        }
        win.raise();
        win.requestActivate();
        page.forceActiveFocus();
    }

    Connections {
        target: Vn

        function onRunningChanged() {
            const win = page.navWin;
            if (!win) {
                return;
            }
            if (Vn.running) {
                if (Kiosk.windowed) {
                    win.hide();
                } else {
                    win.lower();
                }
            } else {
                page.restoreWindow();
            }
        }
    }

    // Esc：玩的时候先结束游戏（而不是把整页弹掉，那样会把游戏窗口落下）。
    Keys.onPressed: function (event) {
        if (event.key !== Qt.Key_Escape) {
            return;
        }
        event.accepted = true;
        if (Vn.running) {
            Vn.stop();
        } else {
            page.navWin.popPage();
        }
    }

    // 时长用完时自动结束游戏：主窗口让开之后，ScreenTime 的计时仍在跑，
    // 但这个页面是唯一能踩刹车的地方。
    Connections {
        target: ScreenTime

        function onLockedOutChanged() {
            if (ScreenTime.lockedOut && Vn.running) {
                Vn.stop();
            }
        }
    }

    Item {
        anchors.fill: page.contentArea

        // ================= 一、选故事 =================
        ColumnLayout {
            anchors.fill: parent
            visible: !page.playing
            spacing: 14

            // ---- 没有引擎：只说怎么装，不摆一堆装不了的按钮 ----
            Rectangle {
                Layout.fillWidth: true
                Layout.preferredHeight: 132
                radius: 22
                color: "#FFFFFF"
                border.color: "#F2E1CD"
                border.width: 1
                visible: !Vn.available

                RowLayout {
                    anchors.fill: parent
                    anchors.margins: 18
                    spacing: 16

                    Rectangle {
                        Layout.preferredWidth: 56
                        Layout.preferredHeight: 56
                        radius: 18
                        color: "#EDE7F6"

                        GlyphIcon {
                            anchors.centerIn: parent
                            width: 30
                            height: 30
                            name: "plug"
                            color: "#5E35B1"
                        }
                    }

                    ColumnLayout {
                        Layout.fillWidth: true
                        spacing: 6

                        Text {
                            text: "还没有吉里吉里运行时"
                            font.pixelSize: 19
                            font.weight: Font.Bold
                            color: "#4A3B32"
                        }

                        Text {
                            Layout.fillWidth: true
                            text: Vn.detail
                            font.pixelSize: 13
                            color: "#9B8574"
                            wrapMode: Text.Wrap
                        }
                    }
                }
            }

            // ---- 有引擎但没游戏 ----
            Rectangle {
                Layout.fillWidth: true
                Layout.preferredHeight: 108
                radius: 22
                color: "#FFFFFF"
                border.color: "#F2E1CD"
                border.width: 1
                visible: Vn.available && Vn.games.length === 0

                RowLayout {
                    anchors.fill: parent
                    anchors.margins: 18
                    spacing: 16

                    Rectangle {
                        Layout.preferredWidth: 56
                        Layout.preferredHeight: 56
                        radius: 18
                        color: "#EDE7F6"

                        GlyphIcon {
                            anchors.centerIn: parent
                            width: 30
                            height: 30
                            name: "book"
                            color: "#5E35B1"
                        }
                    }

                    ColumnLayout {
                        Layout.fillWidth: true
                        spacing: 4

                        Text {
                            text: "还没有故事"
                            font.pixelSize: 19
                            font.weight: Font.Bold
                            color: "#4A3B32"
                        }

                        Text {
                            Layout.fillWidth: true
                            text: "一个游戏一个文件夹，整个放进：" + Vn.gamesDir
                            font.pixelSize: 13
                            color: "#9B8574"
                            wrapMode: Text.Wrap
                        }
                    }
                }
            }

            Flickable {
                Layout.fillWidth: true
                Layout.fillHeight: true
                contentWidth: width
                contentHeight: gameGrid.height
                clip: true
                boundsBehavior: Flickable.StopAtBounds
                visible: Vn.available && Vn.games.length > 0

                Grid {
                    id: gameGrid

                    width: parent.width
                    columns: Math.max(2, Math.min(4, Math.floor(width / 250)))
                    spacing: 14
                    topPadding: 2

                    Repeater {
                        model: Vn.games

                        Rectangle {
                            required property var modelData
                            required property int index

                            width: Math.floor((gameGrid.width - (gameGrid.columns - 1) * gameGrid.spacing)
                                              / gameGrid.columns)
                            height: 118
                            radius: 24
                            color: "#FFFFFF"
                            border.color: cardTap.pressed ? "#7E57C2" : "#F2E1CD"
                            border.width: cardTap.pressed ? 2 : 1

                            scale: cardHover.hovered ? 1.02 : 1.0
                            Behavior on scale {
                                NumberAnimation { duration: 110; easing.type: Easing.OutCubic }
                            }

                            ColumnLayout {
                                anchors.fill: parent
                                anchors.margins: 16
                                spacing: 6

                                RowLayout {
                                    Layout.fillWidth: true
                                    spacing: 10

                                    Rectangle {
                                        Layout.preferredWidth: 40
                                        Layout.preferredHeight: 40
                                        radius: 13
                                        color: "#D1C4E9"

                                        GlyphIcon {
                                            anchors.centerIn: parent
                                            width: 22
                                            height: 22
                                            name: "moon"
                                            color: "#4527A0"
                                        }
                                    }

                                    Text {
                                        Layout.fillWidth: true
                                        text: modelData.title
                                        font.pixelSize: 17
                                        font.weight: Font.DemiBold
                                        color: "#4A3B32"
                                        elide: Text.ElideRight
                                        maximumLineCount: 1
                                    }
                                }

                                Item { Layout.fillHeight: true }

                                Text {
                                    text: modelData.note
                                    font.pixelSize: 12
                                    color: "#B8A492"
                                }
                            }

                            HoverHandler { id: cardHover }

                            TapHandler {
                                id: cardTap
                                onTapped: {
                                    Vn.start(index);
                                    page.forceActiveFocus();
                                }
                            }
                        }
                    }
                }
            }

            RowLayout {
                Layout.fillWidth: true
                spacing: 12

                Text {
                    Layout.fillWidth: true
                    text: Vn.games.length > 0
                          ? ("共 " + Vn.games.length + " 个故事 · " + Vn.backend)
                          : Vn.gamesDir
                    font.pixelSize: 13
                    color: "#9B8574"
                    elide: Text.ElideMiddle
                }

                LargeButton {
                    Layout.preferredWidth: 130
                    Layout.preferredHeight: 52
                    text: "刷新"
                    accentColor: "#EFE1D0"
                    labelColor: "#7A6759"
                    onClicked: {
                        Vn.refresh();
                        page.forceActiveFocus();
                    }
                }
            }
        }

        // ================= 二、故事正在播放 =================
        // 只有「游戏窗口没盖住全屏」时才看得见（小窗口、或引擎没画出窗口）。
        // 平时它是藏着的：主窗口已经 lower() 到下面，本来也不会被点到。
        Rectangle {
            anchors.fill: page
            z: 60
            visible: page.playing
            color: "#F2FFF8EE"

            // 盖住整页（包括顶部返回按钮）时得**吃掉点击**：没有输入处理的
            // Rectangle 在 Qt Quick 里不挡事件，事件会穿到下面的返回按钮上，
            // 于是孩子一点「正在播放」这层就把整页弹掉了。MouseArea 放在
            // 第一个子项，后面的按钮仍然盖在它上面，能正常点击。
            MouseArea {
                anchors.fill: parent
                acceptedButtons: Qt.AllButtons
            }

            ColumnLayout {
                anchors.centerIn: parent
                width: Math.min(parent.width - 120, 520)
                spacing: 18

                GlyphIcon {
                    Layout.alignment: Qt.AlignHCenter
                    Layout.preferredWidth: 64
                    Layout.preferredHeight: 64
                    name: "moon"
                    color: "#7E57C2"
                }

                Text {
                    Layout.fillWidth: true
                    text: Vn.runningTitle
                    font.pixelSize: 26
                    font.weight: Font.Bold
                    color: "#4A3B32"
                    horizontalAlignment: Text.AlignHCenter
                    elide: Text.ElideRight
                }

                Text {
                    Layout.fillWidth: true
                    text: "故事正在播放。游戏窗口应该盖住了屏幕；如果只看到这一页，"
                        + "说明引擎没画出窗口，点下面的按钮退出来。"
                    font.pixelSize: 14
                    color: "#9B8574"
                    wrapMode: Text.Wrap
                    horizontalAlignment: Text.AlignHCenter
                }

                LargeButton {
                    Layout.fillWidth: true
                    Layout.preferredHeight: 58
                    text: "结束故事"
                    accentColor: "#7E57C2"
                    onClicked: Vn.stop()
                }
            }
        }

        // ---- 一句话提示（启动了 / 结束了 / 出错）----
        Rectangle {
            anchors.horizontalCenter: parent.horizontalCenter
            anchors.top: parent.top
            anchors.topMargin: 6
            z: 70
            width: Math.min(parent.width - 40, hintText.implicitWidth + 44)
            height: 44
            radius: 22
            color: "#5E35B1"
            visible: Vn.message.length > 0

            Text {
                id: hintText

                anchors.centerIn: parent
                text: Vn.message
                font.pixelSize: 15
                font.weight: Font.DemiBold
                color: "white"
            }
        }
    }
}
