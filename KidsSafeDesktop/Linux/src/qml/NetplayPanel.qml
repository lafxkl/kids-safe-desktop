import QtQuick
import QtQuick.Layouts
import KidsSafe 1.0

/*
 * 联机面板：开房间 / 输房间号 / 等小伙伴 / 掉线。
 *
 * 面板只负责「把两个人凑进同一个房间」。一旦双方到齐（房间状态变成
 * playing），面板自己关掉并发 started()，之后的对局同步由各游戏页读
 * Netplay.room 完成 —— 象棋和华容道共用同一套入口，玩法逻辑互不干扰。
 *
 * 房间号做成 4 个空格 + 自绘数字键盘，而不是 TextField，原因有三个：
 *   1. 孩子点鼠标比敲键盘快，也不会被输入法弄成全角数字；
 *   2. TextField 会带进控件主题，和这几个游戏页的米色风格对不上；
 *   3. 输满 4 位直接提交，少点一次「确定」。
 */
Item {
    id: panel

    property string game: "chess"      // "chess" / "klotski"
    property int level: 2              // 华容道用哪一档布局

    /// 双方到齐，可以开局了
    signal started()
    /// 房间结束（或对面退出）
    signal finished()

    // 用户在「选择」页点了「加入房间」，正在敲号码
    property bool enteringCode: false
    property string digits: ""

    readonly property string gameName: game === "chess" ? "国际象棋" : "华容道"

    /*
     * 面板自己的一屏状态，和 Netplay.state 不是一一对应：
     *   choose   两个按钮（开场）
     *   code     敲房间号
     *   busy     请求在路上
     *   waiting  房主等人进来
     *   error    出错/被踢
     *   disabled 家长没开
     */
    readonly property string view: {
        if (Netplay.state === "waiting") {
            return "waiting";
        }
        if (Netplay.state === "playing") {
            return "waiting";
        }
        if (Netplay.state === "creating" || Netplay.state === "joining") {
            return "busy";
        }
        if (Netplay.state === "error" || Netplay.state === "closed") {
            return "error";
        }
        if (Netplay.state === "disabled") {
            return "disabled";
        }
        return enteringCode ? "code" : "choose";
    }

    visible: false
    z: 50

    // 双方到齐就收起面板，把屏幕交给对局
    Connections {
        target: Netplay

        function onRoomChanged() {
            if (!panel.visible) {
                return;
            }
            const status = Netplay.room.status;
            if (status === "playing") {
                panel.closedByServer();
                panel.started();
            } else if (status === "done") {
                panel.closedByServer();
                panel.finished();
            }
        }
    }

    onVisibleChanged: {
        if (visible) {
            enteringCode = false;
            digits = "";
        }
    }

    function press(key) {
        if (key === "删") {
            digits = digits.slice(0, Math.max(0, digits.length - 1));
            return;
        }
        if (key === "好") {
            submit();
            return;
        }
        if (digits.length >= 4) {
            return;
        }
        digits += key;
        if (digits.length === 4) {
            submit();
        }
    }

    function submit() {
        if (digits.length !== 4) {
            return;
        }
        Netplay.joinRoom(digits);
    }

    // 面板是「被服务端叫停」的（对方进来 / 房间结束），不用再退房
    function closedByServer() {
        enteringCode = false;
        digits = "";
        visible = false;
    }

    // 用户主动关：还在房间里就得真的退掉
    function close() {
        if (Netplay.state !== "idle" && Netplay.state !== "disabled") {
            Netplay.leave();
        }
        closedByServer();
    }

    // ---- 遮罩 ----
    Rectangle {
        anchors.fill: parent
        color: "#8C4A3B32"

        TapHandler {
            // 点遮罩不关闭：孩子手一滑点偏了会把房间弄丢
        }
    }

    // ---- 卡片 ----
    Rectangle {
        id: card

        anchors.centerIn: parent
        width: Math.min(540, panel.width - 60)
        height: content.implicitHeight + 64
        radius: 28
        color: "#FFFDF8"
        border.color: "#F2E1CD"
        border.width: 1

        Behavior on height {
            NumberAnimation { duration: 150; easing.type: Easing.OutCubic }
        }

        ColumnLayout {
            id: content

            x: 32
            y: 32
            width: card.width - 64
            spacing: 14

            // 标题 + 关闭
            RowLayout {
                Layout.fillWidth: true
                spacing: 10

                ColumnLayout {
                    Layout.fillWidth: true
                    spacing: 4

                    Text {
                        text: {
                            if (panel.view === "code") {
                                return "输入房间号";
                            }
                            if (panel.view === "waiting") {
                                return "等小伙伴进来";
                            }
                            if (panel.view === "busy") {
                                return "稍等一下";
                            }
                            if (panel.view === "error") {
                                return "出了点小问题";
                            }
                            if (panel.view === "disabled") {
                                return "现在还不能联机";
                            }
                            return panel.game === "chess" ? "和小伙伴下棋" : "和华容道比快";
                        }
                        font.pixelSize: 26
                        font.weight: Font.Bold
                        color: "#4A3B32"
                    }

                    Text {
                        Layout.fillWidth: true
                        text: {
                            if (panel.view === "waiting") {
                                return "把这个号码念给小伙伴，他在自己电脑上输进去";
                            }
                            if (panel.view === "code") {
                                return "问问小伙伴，他开的房间号是多少";
                            }
                            if (panel.view === "disabled") {
                                return Netplay.detail;
                            }
                            return "两个小朋友一人开一个房间，或者一个人开、另一个人进";
                        }
                        font.pixelSize: 15
                        color: "#8A7568"
                        wrapMode: Text.WordWrap
                        visible: text !== ""
                    }
                }

                Rectangle {
                    Layout.alignment: Qt.AlignTop
                    width: 40
                    height: 40
                    radius: 20
                    color: closeArea.active ? "#EFE3D4" : "#F7F1E9"

                    Text {
                        anchors.centerIn: parent
                        text: "×"
                        font.pixelSize: 24
                        color: "#8A7568"
                    }

                    TapHandler {
                        id: closeArea
                        onTapped: panel.close()
                    }                }
            }

            // ---- 选择：开房 / 加入 ----
            ColumnLayout {
                Layout.fillWidth: true
                spacing: 12
                visible: panel.view === "choose"

                Rectangle {
                    Layout.fillWidth: true
                    Layout.preferredHeight: 72
                    radius: 20
                    color: createArea.active ? "#FFE3B8" : "#FFF0DA"
                    border.color: "#F0BE7A"
                    border.width: 1

                    ColumnLayout {
                        anchors.centerIn: parent
                        spacing: 2

                        Text {
                            Layout.alignment: Qt.AlignHCenter
                            text: "开一个房间"
                            font.pixelSize: 22
                            font.weight: Font.DemiBold
                            color: "#8A5B18"
                        }

                        Text {
                            Layout.alignment: Qt.AlignHCenter
                            text: "开好了会给你一个 4 位号码"
                            font.pixelSize: 13
                            color: "#B08A55"
                        }
                    }

                    TapHandler {
                        id: createArea
                        onTapped: Netplay.createRoom(panel.game, panel.level)
                    }
                }

                Rectangle {
                    Layout.fillWidth: true
                    Layout.preferredHeight: 72
                    radius: 20
                    color: joinArea.active ? "#E3F0E6" : "#EEF7EF"
                    border.color: "#B7D8BE"
                    border.width: 1

                    ColumnLayout {
                        anchors.centerIn: parent
                        spacing: 2

                        Text {
                            Layout.alignment: Qt.AlignHCenter
                            text: "加入小伙伴的房间"
                            font.pixelSize: 22
                            font.weight: Font.DemiBold
                            color: "#2E6B3A"
                        }

                        Text {
                            Layout.alignment: Qt.AlignHCenter
                            text: "要输他给你的 4 位号码"
                            font.pixelSize: 13
                            color: "#5F8F6B"
                        }
                    }

                    TapHandler {
                        id: joinArea
                        onTapped: {
                            panel.enteringCode = true;
                            panel.digits = "";
                        }
                    }
                }
            }

            // ---- 输房间号 ----
            ColumnLayout {
                Layout.fillWidth: true
                spacing: 16
                visible: panel.view === "code"

                Row {
                    Layout.alignment: Qt.AlignHCenter
                    spacing: 14

                    Repeater {
                        model: 4

                        Rectangle {
                            required property int index

                            width: 68
                            height: 80
                            radius: 16
                            color: index < panel.digits.length ? "#FFFFFF" : "#F7F1E9"
                            border.width: index === panel.digits.length ? 2 : 1
                            border.color: index === panel.digits.length ? "#E8A33D" : "#EADBC8"

                            Text {
                                anchors.centerIn: parent
                                text: panel.digits.charAt(index)
                                font.pixelSize: 34
                                font.weight: Font.Bold
                                color: "#4A3B32"
                            }
                        }
                    }
                }

                Grid {
                    Layout.alignment: Qt.AlignHCenter
                    columns: 3
                    spacing: 10

                    Repeater {
                        model: ["1", "2", "3", "4", "5", "6", "7", "8", "9", "删", "0", "好"]

                        Rectangle {
                            required property string modelData

                            width: 92
                            height: 62
                            radius: 16
                            color: keyArea.active ? "#EFE3D4" : "#FFFFFF"
                            border.color: "#EADBC8"
                            border.width: 1

                            Text {
                                anchors.centerIn: parent
                                text: modelData
                                font.pixelSize: 24
                                font.weight: Font.DemiBold
                                color: modelData === "好" ? "#2E7D32" : "#4A3B32"
                            }

                            TapHandler {
                                id: keyArea
                                onTapped: panel.press(modelData)
                            }
                        }
                    }
                }
            }

            // ---- 等人 ----
            ColumnLayout {
                Layout.fillWidth: true
                spacing: 14
                visible: panel.view === "waiting"

                Rectangle {
                    Layout.alignment: Qt.AlignHCenter
                    width: 220
                    height: 110
                    radius: 22
                    color: "#FFF6E8"
                    border.color: "#F0BE7A"
                    border.width: 1

                    Text {
                        anchors.centerIn: parent
                        text: Netplay.code
                        font.pixelSize: 52
                        font.weight: Font.Bold
                        font.letterSpacing: 10
                        color: "#8A5B18"
                    }
                }

                Text {
                    Layout.alignment: Qt.AlignHCenter
                    text: "号码一直有效，等你 30 分钟"
                    font.pixelSize: 14
                    color: "#A08A7A"
                }
            }

            // ---- 请求中 ----
            ColumnLayout {
                Layout.fillWidth: true
                Layout.preferredHeight: 140
                spacing: 10
                visible: panel.view === "busy"

                Item { Layout.fillHeight: true }

                Text {
                    Layout.alignment: Qt.AlignHCenter
                    text: Netplay.detail
                    font.pixelSize: 18
                    color: "#8A7568"
                }

                Item { Layout.fillHeight: true }
            }

            // ---- 出错 ----
            ColumnLayout {
                Layout.fillWidth: true
                spacing: 14
                visible: panel.view === "error" || panel.view === "disabled"

                Text {
                    Layout.fillWidth: true
                    text: Netplay.detail
                    font.pixelSize: 17
                    color: "#A24C3C"
                    wrapMode: Text.WordWrap
                    visible: text !== ""
                }

                Text {
                    Layout.fillWidth: true
                    text: "联机要家长先把服务器地址填好，再打开「联机对战」开关。"
                    font.pixelSize: 14
                    color: "#A08A7A"
                    wrapMode: Text.WordWrap
                    visible: panel.view === "disabled"
                }

                Rectangle {
                    Layout.fillWidth: true
                    Layout.preferredHeight: 60
                    radius: 18
                    color: backArea.active ? "#EFE3D4" : "#F7F1E9"

                    Text {
                        anchors.centerIn: parent
                        text: "回到选择"
                        font.pixelSize: 19
                        font.weight: Font.DemiBold
                        color: "#6B5A4E"
                    }

                    TapHandler {
                        id: backArea
                        onTapped: {
                            panel.enteringCode = false;
                            panel.digits = "";
                            if (Netplay.state === "error" || Netplay.state === "closed") {
                                Netplay.leave();
                            }
                        }
                    }
                }
            }

            // 底部小字：只在开局那一屏显示
            Text {
                Layout.fillWidth: true
                Layout.topMargin: 4
                text: "第一次用要先让家长在后台把服务器地址填好。只有知道号码的人能进来，不会遇到陌生人。"
                font.pixelSize: 12
                color: "#B3A296"
                wrapMode: Text.WordWrap
                visible: panel.view === "choose"
            }
        }
    }
}
