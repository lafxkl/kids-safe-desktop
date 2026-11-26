import QtQuick
import QtQuick.Layouts
import KidsSafe 1.0

/*
 * 终端。
 *
 * 底下是一个真的 PTY（C++ 侧 forkpty），所以 Tab 补全、cd、管道、python3
 * 交互都能用 —— 那些恰好是孩子最想玩的部分：敲 python3 能算 1+1，
 * 这件事本身就是奖励。换成「把命令丢给 QProcess 收输出」就全没了。
 *
 * 屏幕上显示的是 C++ 把 ANSI 转义序列翻好之后的富文本（Term.html），
 * 这里只管画和收键盘。
 *
 * 键盘处理这块有三个坑，都写在下面的 Keys 处理器里：
 *   1. 可见字符必须走 Term.type()，控制键走 Term.key() —— 分开是因为
 *      C++ 侧要在回车那一刻检查命令，检查只对「镜像出来的整行」有效。
 *   2. 方向键是转义序列，直接 raw() 发出去，不能让镜像记成普通字符，
 *      否则「按上键调历史命令」之后整行镜像就乱了，安全检查也跟着失效。
 *   3. Ctrl+字母要自己换算成控制字符（Ctrl+C = 0x03）。QML 不会替你做。
 */
Item {
    id: root

    // 自动跟随到底部。用户往上翻着看时不要抢滚动条 —— 那是最让人恼火的
    // 终端行为之一（正在看报错，每来一行新输出就被拽到底）。
    property bool follow: true

    property string blockedText: ""

    function scrollToEnd() {
        flick.contentY = Math.max(0, flick.contentHeight - flick.height);
    }

    function runQuick(cmd) {
        keyCatcher.forceActiveFocus();
        if (cmd === "clear") {
            Term.clear();
            return;
        }
        Term.type(cmd);
        Term.enter();
    }

    Component.onCompleted: {
        if (!Term.running) {
            Term.start();
        }
        keyCatcher.forceActiveFocus();
    }

    // 窗口关掉时把 shell 一起收掉。
    // 不这么做的话，关窗只是销毁了界面，子进程还在后台等着 —— 开几次就有
    // 几个 bash 挂着，任务管理器里能看到一堆。
    Component.onDestruction: {
        if (Term.running) {
            Term.stop();
        }
    }

    Connections {
        target: Term

        function onOutputChanged() {
            if (root.follow) {
                Qt.callLater(root.scrollToEnd);
            }
        }

        function onBlocked(reason) {
            // 被拦下来的命令和「真的执行了」看起来必须不一样，
            // 否则孩子会以为 rm -rf / 已经生效了。
            root.blockedText = reason;
            blockedTimer.restart();
        }
    }

    Timer {
        id: blockedTimer
        interval: 4200
        onTriggered: root.blockedText = ""
    }

    ColumnLayout {
        anchors.fill: parent
        spacing: 0

        // ============================================================
        // 顶部：状态 + 两个操作
        // ============================================================
        Rectangle {
            Layout.fillWidth: true
            Layout.preferredHeight: 44
            color: "#1A1F27"

            RowLayout {
                anchors.fill: parent
                anchors.leftMargin: 16
                anchors.rightMargin: 10
                spacing: 10

                // 运行指示点：绿=shell 在跑，红=退出了
                Rectangle {
                    Layout.preferredWidth: 8
                    Layout.preferredHeight: 8
                    radius: 4
                    color: Term.running ? "#3DDC84" : "#E05252"
                }

                Text {
                    text: Term.running ? "正在和电脑说话" : "终端停下来了"
                    font.pixelSize: 12.5
                    color: "#8B97A6"
                }

                Item { Layout.fillWidth: true }

                // 清屏
                Rectangle {
                    Layout.preferredWidth: 34
                    Layout.preferredHeight: 30
                    radius: 9
                    color: clearHover.hovered ? "#2A313B" : "transparent"

                    GlyphIcon {
                        anchors.centerIn: parent
                        name: "trash"
                        width: 14
                        height: 14
                        color: "#8B97A6"
                    }

                    HoverHandler { id: clearHover }

                    TapHandler {
                        onTapped: {
                            Term.clear();
                            keyCatcher.forceActiveFocus();
                        }
                    }
                }

                // 重开 shell
                Rectangle {
                    Layout.preferredWidth: 34
                    Layout.preferredHeight: 30
                    radius: 9
                    color: restartHover.hovered ? "#2A313B" : "transparent"

                    GlyphIcon {
                        anchors.centerIn: parent
                        name: "refresh"
                        width: 14
                        height: 14
                        color: "#8B97A6"
                    }

                    HoverHandler { id: restartHover }

                    TapHandler {
                        onTapped: {
                            Term.stop();
                            Term.start();
                            root.follow = true;
                            keyCatcher.forceActiveFocus();
                        }
                    }
                }
            }
        }

        // ============================================================
        // 屏幕
        // ============================================================
        Rectangle {
            Layout.fillWidth: true
            Layout.fillHeight: true
            color: "#12161C"
            clip: true

            Flickable {
                id: flick

                anchors.fill: parent
                anchors.margins: 14
                clip: true
                contentWidth: width
                contentHeight: screen.implicitHeight
                boundsBehavior: Flickable.StopAtBounds

                onMovementEnded: {
                    // 手动滚到底就算「继续跟随」，滚上去就不跟了
                    root.follow = (contentY + height >= contentHeight - 36);
                }

                Text {
                    id: screen

                    width: flick.width
                    text: Term.html.length > 0 ? Term.html
                                              : "<span style='color:#5C6675'>正在打开终端…</span>"
                    textFormat: Text.RichText
                    wrapMode: Text.WrapAnywhere
                    font.family: "monospace"
                    font.pixelSize: 13.5
                    lineHeight: 1.3
                }
            }

            // 键盘接收器。铺满整块屏幕但完全透明 —— 它唯一的职责是拿焦点，
            // 所以点击终端任意位置都应该把焦点抢回来（另一个窗口刚关掉的时候
            // 焦点可能落在别处，那时敲键盘会没反应）。
            Item {
                id: keyCatcher

                anchors.fill: parent
                focus: true

                TapHandler {
                    onTapped: keyCatcher.forceActiveFocus()
                }

                Keys.onPressed: function (event) {
                    if (event.key === Qt.Key_Return || event.key === Qt.Key_Enter) {
                        Term.enter();
                        root.follow = true;
                        event.accepted = true;
                        return;
                    }
                    if (event.key === Qt.Key_Backspace) {
                        Term.key(8);
                        event.accepted = true;
                        return;
                    }
                    if (event.key === Qt.Key_Tab) {
                        Term.key(9);
                        event.accepted = true;
                        return;
                    }
                    if (event.key === Qt.Key_Escape) {
                        Term.key(27);
                        event.accepted = true;
                        return;
                    }
                    if (event.key === Qt.Key_Up) {
                        Term.raw("\u001b[A");
                        event.accepted = true;
                        return;
                    }
                    if (event.key === Qt.Key_Down) {
                        Term.raw("\u001b[B");
                        event.accepted = true;
                        return;
                    }
                    if (event.key === Qt.Key_Right) {
                        Term.raw("\u001b[C");
                        event.accepted = true;
                        return;
                    }
                    if (event.key === Qt.Key_Left) {
                        Term.raw("\u001b[D");
                        event.accepted = true;
                        return;
                    }

                    // Ctrl+字母 → 控制字符。Ctrl+C 是 0x03、Ctrl+D 是 0x04……
                    // QML 只给出「按了 Ctrl」和「按了 C」，换算得自己来。
                    if (event.modifiers & Qt.ControlModifier) {
                        if (event.key >= Qt.Key_A && event.key <= Qt.Key_Z) {
                            const code = event.key - Qt.Key_A + 1;
                            Term.raw(String.fromCharCode(code));
                            event.accepted = true;
                            return;
                        }
                    }

                    // 剩下的按可见字符送进去。输入法提交中文时 text 可能是
                    // 一整串，所以是 type(text) 而不是逐字符。
                    if (event.text.length > 0) {
                        Term.type(event.text);
                        event.accepted = true;
                    }
                }
            }

            // 「shell 退出了」的提示条
            Rectangle {
                anchors.horizontalCenter: parent.horizontalCenter
                anchors.bottom: parent.bottom
                anchors.bottomMargin: 16
                width: exitRow.implicitWidth + 28
                height: 40
                radius: 12
                color: "#E63A4250"
                visible: Term.exited

                Row {
                    id: exitRow

                    anchors.centerIn: parent
                    spacing: 10

                    Text {
                        anchors.verticalCenter: parent.verticalCenter
                        text: "终端关掉了，点右边重新打开"
                        font.pixelSize: 12.5
                        color: "white"
                    }

                    Rectangle {
                        anchors.verticalCenter: parent.verticalCenter
                        width: 62
                        height: 26
                        radius: 8
                        color: "#3DDC84"

                        Text {
                            anchors.centerIn: parent
                            text: "重开"
                            font.pixelSize: 12
                            font.weight: Font.DemiBold
                            color: "#0B2E20"
                        }

                        TapHandler {
                            onTapped: {
                                Term.start();
                                root.follow = true;
                                keyCatcher.forceActiveFocus();
                            }
                        }
                    }
                }
            }
        }

        // ============================================================
        // 常用命令
        //
        // 孩子第一次面对黑屏时最常见的问题是「我该敲什么」。给几个一定能跑的
        // 命令，让他先看到东西动起来，再自己去试。
        // 放在终端下方而不是弹一个帮助页：点一下就能对比「我敲的」和「它出的」。
        // ============================================================
        Rectangle {
            Layout.fillWidth: true
            Layout.preferredHeight: 50
            color: "#161B22"

            RowLayout {
                anchors.fill: parent
                anchors.leftMargin: 14
                anchors.rightMargin: 14
                spacing: 8

                Text {
                    text: "试试："
                    font.pixelSize: 12
                    color: "#6B7A8C"
                }

                Repeater {
                    model: [
                        { "label": "ls", "cmd": "ls" },
                        { "label": "现在在哪", "cmd": "pwd" },
                        { "label": "今天几号", "cmd": "date" },
                        { "label": "算一算", "cmd": "python3 -c \"print(2+3*4)\"" },
                        { "label": "清屏", "cmd": "clear" }
                    ]

                    Rectangle {
                        required property var modelData

                        Layout.preferredHeight: 28
                        Layout.preferredWidth: chipText.implicitWidth + 22
                        radius: 9
                        color: chipHover.hovered ? "#2C3542" : "#232B36"
                        border.width: 1
                        border.color: "#2E3846"

                        Text {
                            id: chipText
                            anchors.centerIn: parent
                            text: modelData.label
                            font.pixelSize: 12
                            color: "#B9C4D2"
                        }

                        HoverHandler { id: chipHover }

                        TapHandler {
                            onTapped: root.runQuick(modelData.cmd)
                        }
                    }
                }

                Item { Layout.fillWidth: true }
            }
        }
    }

    // ============================================================
    // 被拦下来的命令
    // ============================================================
    Rectangle {
        anchors.horizontalCenter: parent.horizontalCenter
        anchors.bottom: parent.bottom
        anchors.bottomMargin: 74
        width: Math.min(parent.width - 60, blockedRow.implicitWidth + 32)
        height: 46
        radius: 12
        color: "#F0B3265E"
        visible: root.blockedText.length > 0
        opacity: visible ? 1 : 0

        Row {
            id: blockedRow

            anchors.centerIn: parent
            spacing: 9

            GlyphIcon {
                anchors.verticalCenter: parent.verticalCenter
                name: "shield"
                width: 17
                height: 17
                color: "white"
            }

            Text {
                anchors.verticalCenter: parent.verticalCenter
                width: Math.min(implicitWidth, root.width - 130)
                text: root.blockedText
                font.pixelSize: 12.5
                color: "white"
                wrapMode: Text.WordWrap
            }
        }
    }
}
