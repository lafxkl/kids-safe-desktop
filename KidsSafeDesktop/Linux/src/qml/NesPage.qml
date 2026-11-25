import QtQuick
import QtQuick.Layouts
import QtQuick.Window
import KidsSafe 1.0

/*
 * 红白机（NES / Famicom）。
 *
 * 模拟器核心是 SimpleNes（MIT），编成静态库链进来；这里只负责两件事：
 *   1. 选游戏 —— 列出 ROM 目录里的 .nes 文件；
 *   2. 玩 —— NesView 把 NesEmulator 的帧画出来，键盘事件转成手柄按键。
 *
 * ROM 不随包分发（版权原因），家长自己把 .nes 放进
 * ~/.local/share/kidssafe/roms，页面里的路径是现算的，不用改这里。
 */
GameShell {
    id: page

    readonly property bool playing: Nes.loaded

    title: playing ? Nes.title : "红白机"
    glyph: "gamepad"
    accent: "#E53935"
    prompt: playing
            ? "方向键移动 · Z 跳/确认 · X 跑/取消 · Enter 开始 · Esc 退出"
            : "家长把 .nes 游戏放进下面的文件夹，点「刷新」就能玩"

    focus: true
    Component.onCompleted: {
        forceActiveFocus();
        // 上次是被「离开页面」暂停的（不是孩子自己按的暂停），进来就接着跑。
        Nes.resumeFromLeave();
    }
    // 进入/退出游戏时把焦点抢回来：点过按钮之后焦点可能跑到按钮上，
    // 键盘事件就再也进不到模拟器了。
    onPlayingChanged: forceActiveFocus()
    // 离开页面时把按住的键全松开、并且暂停：
    //   · 不松键 → 下次进来人物会自己往右走；
    //   · 不暂停 → 在首页挂着也在跑 60fps，笔记本风扇会一直响。
    // pauseForLeave 只暂停「本来在跑的」，并记住这次是自动暂停，
    // 回到页面时 resumeFromLeave 会悄悄恢复 —— 孩子不会莫名看到「已暂停」。
    Component.onDestruction: {
        Nes.releaseButtons();
        Nes.pauseForLeave();
    }

    // ---- 键盘 ----
    // 只处理模拟器认识的键，其它键（Tab 之类）继续交给 Qt 的默认焦点切换。
    Keys.onPressed: function (event) {
        if (event.key === Qt.Key_Escape) {
            page.navWin.popPage();
            event.accepted = true;
            return;
        }
        if (!page.playing) {
            return;
        }
        if (Nes.keyEvent(event.key, true)) {
            event.accepted = true;
        }
    }

    Keys.onReleased: function (event) {
        if (!page.playing) {
            return;
        }
        if (Nes.keyEvent(event.key, false)) {
            event.accepted = true;
        }
    }

    Item {
        anchors.fill: page.contentArea

        // ================= 一、选游戏 =================
        ColumnLayout {
            anchors.fill: parent
            visible: !page.playing
            spacing: 14

            // 空文件夹时给一段明确的操作说明，而不是只显示「没有游戏」。
            Rectangle {
                Layout.fillWidth: true
                Layout.preferredHeight: 96
                radius: 22
                color: "#FFFFFF"
                border.color: "#F2E1CD"
                border.width: 1
                visible: Nes.roms.length === 0

                RowLayout {
                    anchors.fill: parent
                    anchors.margins: 18
                    spacing: 16

                    Rectangle {
                        Layout.preferredWidth: 56
                        Layout.preferredHeight: 56
                        radius: 18
                        color: "#FFEBEE"

                        GlyphIcon {
                            anchors.centerIn: parent
                            width: 30
                            height: 30
                            name: "gamepad"
                            color: "#C62828"
                        }
                    }

                    ColumnLayout {
                        Layout.fillWidth: true
                        spacing: 4

                        Text {
                            text: "还没有游戏"
                            font.pixelSize: 19
                            font.weight: Font.Bold
                            color: "#4A3B32"
                        }

                        Text {
                            Layout.fillWidth: true
                            text: "把 .nes 文件（红白机 ROM）复制到这个文件夹：" + Nes.romDir
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
                contentHeight: romGrid.height
                clip: true
                boundsBehavior: Flickable.StopAtBounds

                Grid {
                    id: romGrid

                    width: parent.width
                    columns: Math.max(2, Math.min(4, Math.floor(width / 250)))
                    spacing: 14
                    topPadding: 2

                    Repeater {
                        model: Nes.roms

                        Rectangle {
                            required property var modelData

                            width: Math.floor((romGrid.width - (romGrid.columns - 1) * romGrid.spacing)
                                              / romGrid.columns)
                            height: 118
                            radius: 24
                            color: "#FFFFFF"
                            border.color: cardTap.pressed ? "#E53935" : "#F2E1CD"
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
                                        color: "#FFCDD2"

                                        GlyphIcon {
                                            anchors.centerIn: parent
                                            width: 22
                                            height: 22
                                            name: "gamepad"
                                            color: "#C62828"
                                        }
                                    }

                                    Text {
                                        Layout.fillWidth: true
                                        text: modelData.name
                                        font.pixelSize: 17
                                        font.weight: Font.DemiBold
                                        color: "#4A3B32"
                                        elide: Text.ElideRight
                                        maximumLineCount: 1
                                    }
                                }

                                Item { Layout.fillHeight: true }

                                Text {
                                    text: modelData.size
                                    font.pixelSize: 12
                                    color: "#B8A492"
                                }
                            }

                            HoverHandler { id: cardHover }

                            TapHandler {
                                id: cardTap
                                onTapped: Nes.openRom(modelData.file)
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
                    text: Nes.roms.length > 0
                          ? ("共 " + Nes.roms.length + " 个游戏，点一下就开始")
                          : Nes.romDir
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
                    onClicked: Nes.scanRoms()
                }
            }
        }

        // ================= 二、玩 =================
        ColumnLayout {
            anchors.fill: parent
            visible: page.playing
            spacing: 12

            Rectangle {
                Layout.fillWidth: true
                Layout.fillHeight: true
                radius: 20
                color: "#101010"

                NesView {
                    anchors.fill: parent
                    anchors.margins: 8
                    emulator: Nes
                }

                // 暂停时盖一层，孩子一眼看出「停住了」，而不是以为卡死。
                Rectangle {
                    anchors.fill: parent
                    radius: 20
                    color: "#B3000000"
                    visible: Nes.paused

                    Column {
                        anchors.centerIn: parent
                        spacing: 10

                        Text {
                            anchors.horizontalCenter: parent.horizontalCenter
                            text: "已暂停"
                            font.pixelSize: 34
                            font.weight: Font.Bold
                            color: "white"
                        }

                        Text {
                            anchors.horizontalCenter: parent.horizontalCenter
                            text: "点下面的「继续」接着玩"
                            font.pixelSize: 15
                            color: "#DDDDDD"
                        }
                    }
                }

                // 提示条不放在这里：选游戏和玩游戏两种状态都要能看见，
                // 所以提到最外层（见文件末尾）。
            }

            RowLayout {
                Layout.fillWidth: true
                spacing: 10

                LargeButton {
                    Layout.fillWidth: true
                    Layout.preferredHeight: 56
                    text: Nes.paused ? "继续" : "暂停"
                    accentColor: Nes.paused ? "#43A047" : "#FFB74D"
                    onClicked: {
                        Nes.togglePause();
                        page.forceActiveFocus();
                    }
                }

                LargeButton {
                    Layout.fillWidth: true
                    Layout.preferredHeight: 56
                    text: "重来"
                    accentColor: "#FFFFFF"
                    labelColor: "#7A6759"
                    onClicked: {
                        Nes.resetGame();
                        page.forceActiveFocus();
                    }
                }

                LargeButton {
                    Layout.fillWidth: true
                    Layout.preferredHeight: 56
                    text: "存档"
                    accentColor: "#FFFFFF"
                    labelColor: "#7A6759"
                    onClicked: {
                        Nes.saveState();
                        page.forceActiveFocus();
                    }
                }

                LargeButton {
                    Layout.fillWidth: true
                    Layout.preferredHeight: 56
                    text: "读档"
                    accentColor: "#FFFFFF"
                    labelColor: "#7A6759"
                    onClicked: {
                        Nes.loadState();
                        page.forceActiveFocus();
                    }
                }

                LargeButton {
                    Layout.preferredWidth: 150
                    Layout.preferredHeight: 56
                    text: "换游戏"
                    accentColor: "#EFE1D0"
                    labelColor: "#7A6759"
                    onClicked: {
                        Nes.closeRom();
                        Nes.scanRoms();
                        page.forceActiveFocus();
                    }
                }
            }
        }

        // ---- 一句话提示（存档 / 读档 / 错误）----
        // 两种状态共用，所以挂在最外层；3 秒后由 C++ 侧自动清空。
        Rectangle {
            anchors.horizontalCenter: parent.horizontalCenter
            anchors.top: parent.top
            anchors.topMargin: 6
            z: 20
            width: Math.min(parent.width - 40, hintText.implicitWidth + 44)
            height: 44
            radius: 22
            color: "#E64A19"
            visible: Nes.message.length > 0

            Text {
                id: hintText

                anchors.centerIn: parent
                text: Nes.message
                font.pixelSize: 15
                font.weight: Font.DemiBold
                color: "white"
            }
        }
    }
}
