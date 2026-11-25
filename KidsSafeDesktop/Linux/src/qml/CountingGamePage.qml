import QtQuick
import QtQuick.Layouts

/*
 * 数一数：屏幕上出现 N 颗星星，从四个数字里选出正确答案。
 * 对应 Windows 端的 CountingGamePage。
 *
 * 星星用 GlyphIcon 画，不用 emoji —— 一是 emoji 在 deepin 上会变黑白，
 * 二是这里要按数量摆 12 个，字体渲染的 emoji 基线不齐，排出来高低不一是歪的。
 */
GameShell {
    id: page

    title: "数一数"
    glyph: "star"
    accent: "#26A69A"
    prompt: "数一数，一共有几个？"
    scoreText: "答对 " + score + " / " + totalRounds

    property int score: 0
    property int round: 1
    property int totalRounds: 5
    property int target: 0
    property var options: []
    property int picked: -1
    property bool locked: false
    property bool finished: false

    function newRound() {
        // 难度只影响数量上限：多了数不过来，孩子会开始乱点
        const maxCount = difficulty === 1 ? 5 : (difficulty === 2 ? 8 : 12);
        target = 1 + Math.floor(Math.random() * maxCount);

        const pool = [target];
        while (pool.length < 4) {
            const candidate = 1 + Math.floor(Math.random() * Math.max(maxCount, 4));
            if (pool.indexOf(candidate) === -1) {
                pool.push(candidate);
            }
        }
        // 洗牌，免得正确答案永远在第一格
        for (let i = pool.length - 1; i > 0; i--) {
            const j = Math.floor(Math.random() * (i + 1));
            const tmp = pool[i];
            pool[i] = pool[j];
            pool[j] = tmp;
        }
        options = pool;
        picked = -1;
        locked = false;
    }

    function choose(value) {
        if (locked || finished) {
            return;
        }
        picked = value;
        locked = true;
        if (value === target) {
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
            spacing: 26

            // ---- 星星 ----
            Flow {
                Layout.alignment: Qt.AlignHCenter
                Layout.preferredWidth: Math.min(parent.width, 520)
                spacing: 12

                Repeater {
                    model: page.target

                    GlyphIcon {
                        width: 54
                        height: 54
                        name: "star"
                        color: "#F5B942"
                    }
                }
            }

            // ---- 选项 ----
            RowLayout {
                Layout.alignment: Qt.AlignHCenter
                spacing: 18

                Repeater {
                    model: page.options

                    Rectangle {
                        required property int modelData

                        Layout.preferredWidth: 96
                        Layout.preferredHeight: 96
                        radius: 26
                        color: !page.locked
                               ? "#FFFFFF"
                               : (modelData === page.target
                                  ? "#C8E6C9"
                                  : (modelData === page.picked ? "#FFCDD2" : "#FFFFFF"))
                        border.color: !page.locked
                                      ? "#F2E1CD"
                                      : (modelData === page.target ? "#81C784" : "#F2E1CD")
                        border.width: 1

                        scale: optionTap.pressed ? 0.94 : 1.0
                        Behavior on scale {
                            NumberAnimation { duration: 100 }
                        }

                        Text {
                            anchors.centerIn: parent
                            text: modelData
                            font.pixelSize: 44
                            font.weight: Font.Bold
                            color: "#4A3B32"
                        }

                        TapHandler {
                            id: optionTap
                            onTapped: page.choose(modelData)
                        }
                    }
                }
            }

            Text {
                Layout.alignment: Qt.AlignHCenter
                visible: page.locked
                text: page.picked === page.target ? "答对啦！" : ("应该是 " + page.target + " 个，再数一数～")
                font.pixelSize: 20
                font.weight: Font.DemiBold
                color: page.picked === page.target ? "#2E7D32" : "#D84315"
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
        interval: 950
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
