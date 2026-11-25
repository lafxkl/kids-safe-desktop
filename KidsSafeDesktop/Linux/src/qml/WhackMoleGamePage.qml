import QtQuick
import QtQuick.Layouts

/*
 * 打地鼠：限时内点冒头的小熊。
 * 对应 Windows 端的 WhackMoleGamePage。
 *
 * 规则刻意宽松：打空不扣分、地鼠自己缩回去也不算错，只统计打中几只。
 * 难度只改两件事——出洞间隔和总时长，不改命中判定：判断「点得准不准」
 * 在小屏 / 触控板上的差异太大，不该由难度背这个锅。
 */
GameShell {
    id: page

    title: "打地鼠"
    glyph: "hammer"
    accent: "#6D4C41"
    prompt: "小熊冒头就点它！"
    scoreText: "打中 " + score + " 个 · 还剩 " + remaining + " 秒"

    property int score: 0
    property int remaining: 20
    property int activeHole: -1
    property bool moleUp: false
    property bool finished: false

    readonly property int popInterval: difficulty === 1 ? 1000 : (difficulty === 2 ? 780 : 600)
    readonly property int totalSeconds: difficulty === 1 ? 25 : (difficulty === 2 ? 20 : 15)

    function startGame() {
        score = 0;
        remaining = totalSeconds;
        activeHole = -1;
        moleUp = false;
        finished = false;
        moleTimer.interval = popInterval;
        moleTimer.start();
        tickTimer.start();
        popMole();
    }

    function popMole() {
        if (finished) {
            return;
        }
        // 换一个和上次不同的洞，否则孩子会以为「没动」
        let next = Math.floor(Math.random() * 9);
        if (next === activeHole) {
            next = (next + 1 + Math.floor(Math.random() * 8)) % 9;
        }
        activeHole = next;
        moleUp = true;
        hideTimer.restart();
    }

    function whack(index) {
        if (finished || !moleUp || index !== activeHole) {
            return;
        }
        score++;
        moleUp = false;
        hideTimer.stop();
    }

    function endGame() {
        finished = true;
        moleUp = false;
        moleTimer.stop();
        tickTimer.stop();
        hideTimer.stop();
    }

    Component.onCompleted: startGame()

    Item {
        anchors.fill: page.contentArea

        Grid {
            id: board

            anchors.centerIn: parent
            columns: 3
            spacing: 22

            // 九宫格整块要塞进内容区：先按宽度分三份，再用高度收一次，
            // 窗口拉矮时最后一排不会被顶出可视区。
            // ⚠️ 这里必须给 Grid 一个 id，下面的格子靠 board.cellSize 取尺寸；
            // 漏掉 id 时 board 是未定义引用，宽高全成 0，整块空白（不报致命错误，
            // 只在控制台留一行 ReferenceError）。
            readonly property real cellSize: {
                const byWidth = (page.contentArea.width - spacing * 2) / 3;
                const byHeight = (page.contentArea.height - spacing * 2) / 3;
                return Math.max(48, Math.min(132, byWidth, byHeight));
            }

            Repeater {
                model: 9

                Item {
                    width: board.cellSize
                    height: board.cellSize

                    // 这只洞当前是否该露出小熊。抽成一个属性，opacity 与 scale
                    // 都从这里取，避免「scale 读 opacity」那种自引用式的绑定。
                    readonly property bool moleShown: page.moleUp && page.activeHole === index

                    // 洞口
                    Rectangle {
                        anchors.fill: parent
                        anchors.margins: 4
                        radius: width / 2
                        color: "#8D6E63"

                        Rectangle {
                            anchors.centerIn: parent
                            width: parent.width * 0.74
                            height: width
                            radius: width / 2
                            color: "#4E342E"
                        }
                    }

                    // 冒出来的小熊
                    // 显隐用 opacity 归零，不用 scale 归零：后者在 0.1 缩放时
                    // 会在洞口留下一个米粒大小的点，看着像脏东西。
                    GlyphIcon {
                        anchors.horizontalCenter: parent.horizontalCenter
                        anchors.bottom: parent.bottom
                        anchors.bottomMargin: parent.height * 0.12
                        width: parent.width * 0.72
                        height: width
                        name: "bear"
                        opacity: moleShown ? 1.0 : 0.0
                        scale: moleShown ? 1.0 : 0.35

                        Behavior on opacity {
                            NumberAnimation { duration: 120; easing.type: Easing.OutQuad }
                        }
                        Behavior on scale {
                            NumberAnimation { duration: 140; easing.type: Easing.OutBack }
                        }
                    }

                    TapHandler {
                        onTapped: page.whack(index)
                    }
                }
            }
        }

        GameResultOverlay {
            anchors.fill: parent
            visible: page.finished
            score: page.score
            total: 0
            summary: "这一局打中 " + page.score + " 只小熊"
            onReplay: page.startGame()
            onExit: page.navWin.popPage()
        }
    }

    Timer {
        id: moleTimer
        interval: 780
        repeat: true
        onTriggered: page.popMole()
    }

    Timer {
        id: hideTimer
        interval: 720
        onTriggered: page.moleUp = false
    }

    Timer {
        id: tickTimer
        interval: 1000
        repeat: true
        onTriggered: {
            page.remaining--;
            if (page.remaining <= 0) {
                page.remaining = 0;
                page.endGame();
            }
        }
    }
}
