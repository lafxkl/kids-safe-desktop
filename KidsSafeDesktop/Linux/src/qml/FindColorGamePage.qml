import QtQuick
import QtQuick.Layouts

/*
 * 找颜色：上面给出颜色的名字，下面从四块颜色里点出对应的那块。
 * 对应 Windows 端的 FindColorGamePage。
 *
 * 颜色名固定用中文（红 / 蓝 / 黄…），不做本地化：这个游戏的目的之一
 * 就是让孩子把「颜色」和「汉字」对上号。
 */
GameShell {
    id: page

    title: "找颜色"
    glyph: "palette"
    accent: "#3949AB"
    prompt: "点一点下面这个颜色"
    scoreText: "答对 " + score + " / " + totalRounds

    property int score: 0
    property int round: 1
    property int totalRounds: 5
    property string answerName: ""
    property string answerColor: "#FFFFFF"
    property var options: []
    property string pickedColor: ""
    property bool locked: false
    property bool finished: false

    readonly property var palette: [
        { "name": "红色", "value": "#E53935" },
        { "name": "蓝色", "value": "#1E88E5" },
        { "name": "黄色", "value": "#FDD835" },
        { "name": "绿色", "value": "#43A047" },
        { "name": "紫色", "value": "#8E24AA" },
        { "name": "橙色", "value": "#FB8C00" },
        { "name": "粉色", "value": "#EC407A" },
        { "name": "棕色", "value": "#6D4C41" }
    ]

    function newRound() {
        const pool = [];
        for (let i = 0; i < palette.length; i++) {
            pool.push(palette[i]);
        }
        // 洗牌后取前四个：第一个当答案
        for (let i = pool.length - 1; i > 0; i--) {
            const j = Math.floor(Math.random() * (i + 1));
            const tmp = pool[i];
            pool[i] = pool[j];
            pool[j] = tmp;
        }
        const chosen = pool.slice(0, 4);
        answerName = chosen[0].name;
        answerColor = chosen[0].value;
        options = chosen;
        pickedColor = "";
        locked = false;
    }

    function choose(value) {
        if (locked || finished) {
            return;
        }
        pickedColor = value;
        locked = true;
        if (value === answerColor) {
            score++;
        }
        nextTimer.start();
    }

    function restart() {
        score = 0;
        round = 1;
        finished = false;
        newRound();
    }

    Component.onCompleted: newRound()

    Item {
        anchors.fill: page.contentArea

        ColumnLayout {
            anchors.centerIn: parent
            width: Math.min(parent.width, 720)
            spacing: 30

            Rectangle {
                Layout.alignment: Qt.AlignHCenter
                Layout.preferredHeight: 96
                Layout.preferredWidth: titleLabel.implicitWidth + 80
                radius: 30
                color: "#FFFFFF"
                border.color: answerColor
                border.width: 3

                Text {
                    id: titleLabel

                    anchors.centerIn: parent
                    text: page.answerName
                    font.pixelSize: 44
                    font.weight: Font.Bold
                    color: "#4A3B32"
                }
            }

            RowLayout {
                Layout.alignment: Qt.AlignHCenter
                spacing: 20

                Repeater {
                    model: page.options

                    Rectangle {
                        required property var modelData

                        Layout.preferredWidth: 112
                        Layout.preferredHeight: 112
                        radius: 28
                        color: modelData.value
                        border.color: "#FFFFFF"
                        border.width: page.locked && modelData.value === page.answerColor ? 6 : 3

                        // 选错时打一个叉，正确时打勾：不靠颜色变化，
                        // 因为对色觉障碍的孩子来说「变浅一点」根本看不出来
                        Text {
                            anchors.centerIn: parent
                            visible: page.locked
                                     && (modelData.value === page.answerColor
                                         || modelData.value === page.pickedColor)
                            text: modelData.value === page.answerColor ? "✓" : "✕"
                            font.pixelSize: 52
                            font.weight: Font.Bold
                            color: "white"
                        }

                        scale: colorTap.pressed ? 0.94 : 1.0
                        Behavior on scale {
                            NumberAnimation { duration: 100 }
                        }

                        TapHandler {
                            id: colorTap
                            onTapped: page.choose(modelData.value)
                        }
                    }
                }
            }

            Text {
                Layout.alignment: Qt.AlignHCenter
                visible: page.locked
                text: page.pickedColor === page.answerColor
                      ? "答对啦！"
                      : ("这个颜色是「" + page.answerName + "」哦")
                font.pixelSize: 20
                font.weight: Font.DemiBold
                color: page.pickedColor === page.answerColor ? "#2E7D32" : "#D84315"
            }
        }

        GameResultOverlay {
            anchors.fill: parent
            visible: page.finished
            score: page.score
            total: page.totalRounds
            onReplay: page.restart()
            onExit: page.navWin.popPage()
        }
    }

    Timer {
        id: nextTimer
        interval: 1000
        onTriggered: {
            if (page.round >= page.totalRounds) {
                page.finished = true;
            } else {
                page.round++;
                page.newRound();
            }
        }
    }
}
