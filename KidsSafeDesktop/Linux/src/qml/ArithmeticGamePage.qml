import QtQuick
import QtQuick.Layouts

/*
 * 算术乐园：随机出一道加减法，从四个答案里选。
 * 对应 Windows 端的 ArithmeticGamePage。
 *
 * 减法永远保证「大数减小数」且结果非负：学龄前的孩子遇到负数会直接卡住，
 * 这不是难度问题，是超纲。
 */
GameShell {
    id: page

    title: "算术乐园"
    glyph: "pencil"
    accent: "#00897B"
    prompt: "算一算，答案是多少？"
    scoreText: "答对 " + score + " / " + totalRounds

    property int score: 0
    property int round: 1
    property int totalRounds: 5
    property int leftNumber: 0
    property int rightNumber: 0
    property bool minus: false
    property int answer: 0
    property var options: []
    property int picked: -1
    property bool locked: false
    property bool finished: false

    function newRound() {
        const max = difficulty === 1 ? 10 : (difficulty === 2 ? 20 : 50);

        minus = difficulty >= 2 && Math.random() < 0.5;

        if (minus) {
            leftNumber = 2 + Math.floor(Math.random() * (max - 1));
            rightNumber = 1 + Math.floor(Math.random() * (leftNumber - 1));
            answer = leftNumber - rightNumber;
        } else {
            const cap = difficulty === 1 ? Math.floor(max / 2) : max;
            leftNumber = 1 + Math.floor(Math.random() * cap);
            rightNumber = 1 + Math.floor(Math.random() * cap);
            answer = leftNumber + rightNumber;
        }

        // 干扰项围绕正确答案上下浮动，避免「一眼就看出最大的那个是答案」
        const span = Math.max(6, Math.round(answer * 0.6));
        const pool = [answer];
        let guard = 0;
        while (pool.length < 4 && guard < 200) {
            guard++;
            const candidate = answer + (Math.floor(Math.random() * (span * 2 + 1)) - span);
            if (candidate >= 0 && pool.indexOf(candidate) === -1) {
                pool.push(candidate);
            }
        }
        while (pool.length < 4) {
            pool.push(pool.length + answer);
        }

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
        if (value === answer) {
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
                Layout.preferredWidth: equation.implicitWidth + 90
                Layout.preferredHeight: 120
                radius: 34
                color: "#FFFFFF"
                border.color: "#B2DFDB"
                border.width: 3

                Text {
                    id: equation

                    anchors.centerIn: parent
                    text: page.leftNumber + (page.minus ? " − " : " + ")
                          + page.rightNumber + " = ?"
                    font.pixelSize: 56
                    font.weight: Font.Bold
                    color: "#00695C"
                }
            }

            RowLayout {
                Layout.alignment: Qt.AlignHCenter
                spacing: 18

                Repeater {
                    model: page.options

                    Rectangle {
                        required property int modelData

                        Layout.preferredWidth: 104
                        Layout.preferredHeight: 104
                        radius: 28
                        color: !page.locked
                               ? "#FFFFFF"
                               : (modelData === page.answer
                                  ? "#C8E6C9"
                                  : (modelData === page.picked ? "#FFCDD2" : "#FFFFFF"))
                        border.color: page.locked && modelData === page.answer ? "#81C784" : "#F2E1CD"
                        border.width: 1

                        scale: answerTap.pressed ? 0.94 : 1.0
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
                            id: answerTap
                            onTapped: page.choose(modelData)
                        }
                    }
                }
            }

            Text {
                Layout.alignment: Qt.AlignHCenter
                visible: page.locked
                text: page.picked === page.answer ? "答对啦！" : ("正确答案是 " + page.answer)
                font.pixelSize: 20
                font.weight: Font.DemiBold
                color: page.picked === page.answer ? "#2E7D32" : "#D84315"
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
