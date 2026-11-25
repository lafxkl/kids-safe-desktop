import QtQuick
import QtQuick.Layouts

/*
 * 拼图配对：翻开两张卡片，图案相同就消掉。
 * 对应 Windows 端的 MatchingGamePage。
 *
 * 状态存在 ListModel 里（face：0 背面 / 1 翻开 / 2 已配对），不用 JS 数组：
 * QML 不会侦听普通数组内部元素的变化，改完界面不刷新，看起来像点了没反应。
 * role 名刻意不叫 state —— Item 自带 state 属性，同名会被 required 属性覆盖并报错。
 */
GameShell {
    id: page

    title: "拼图配对"
    glyph: "grid"
    accent: "#EF6C00"
    prompt: "翻开两张，找出相同的一对"
    scoreText: "已配对 " + score + " / " + pairs

    property int score: 0
    property int pairs: 3
    property int firstIndex: -1
    property bool busy: false
    property bool finished: false

    ListModel {
        id: deck
    }

    function buildDeck() {
        deck.clear();
        pairs = difficulty === 1 ? 3 : (difficulty === 2 ? 4 : 6);

        const glyphs = ["star", "book", "music", "shield", "pencil", "palette",
                        "trophy", "gamepad"];
        const tints = ["#F5B942", "#7E57C2", "#EC407A", "#42A5F5",
                       "#66BB6A", "#FF7043", "#26A69A", "#8D6E63"];

        const slots = [];
        for (let i = 0; i < pairs; i++) {
            slots.push(i);
            slots.push(i);
        }
        // 洗牌
        for (let i = slots.length - 1; i > 0; i--) {
            const j = Math.floor(Math.random() * (i + 1));
            const tmp = slots[i];
            slots[i] = slots[j];
            slots[j] = tmp;
        }

        for (let i = 0; i < slots.length; i++) {
            deck.append({
                "slot": slots[i],
                "glyph": glyphs[slots[i]],
                "tint": tints[slots[i]],
                "face": 0
            });
        }

        score = 0;
        firstIndex = -1;
        busy = false;
        finished = false;
    }

    function flip(index) {
        if (busy || finished || deck.get(index).face !== 0) {
            return;
        }

        deck.setProperty(index, "face", 1);

        if (firstIndex === -1) {
            firstIndex = index;
            return;
        }

        const a = deck.get(firstIndex).slot;
        const b = deck.get(index).slot;

        if (a === b) {
            deck.setProperty(firstIndex, "face", 2);
            deck.setProperty(index, "face", 2);
            firstIndex = -1;
            score++;
            if (score === pairs) {
                finished = true;
            }
        } else {
            // 两张不一样：锁住输入 → 700ms 后翻回去。不锁的话孩子能连点
            // 翻开第三、第四张，第一次比对还没结束状态就乱了。
            busy = true;
            pendingA = firstIndex;
            pendingB = index;
            firstIndex = -1;
            flipBackTimer.start();
        }
    }

    property int pendingA: -1
    property int pendingB: -1

    function restart() {
        buildDeck();
    }

    Component.onCompleted: buildDeck()

    Item {
        anchors.fill: page.contentArea

        Grid {
            id: board

            anchors.centerIn: parent
            columns: 4
            spacing: 12

            readonly property int cardSize: Math.min(112,
                Math.floor((parent.width - 3 * spacing) / columns))

            Repeater {
                model: deck

                Rectangle {
                    required property int index
                    required property int slot
                    required property string glyph
                    required property string tint
                    required property int face

                    width: board.cardSize
                    height: board.cardSize
                    radius: width * 0.22
                    color: face === 0 ? "#7E57C2" : "#FFFFFF"
                    border.color: face === 0 ? "#7E57C2" : tint
                    border.width: face === 0 ? 0 : 2
                    opacity: face === 2 ? 0.45 : 1.0

                    Behavior on opacity {
                        NumberAnimation { duration: 160 }
                    }

                    Text {
                        anchors.centerIn: parent
                        visible: face === 0
                        text: "?"
                        font.pixelSize: parent.width * 0.42
                        font.weight: Font.Bold
                        color: "#E8DEFF"
                    }

                    GlyphIcon {
                        anchors.centerIn: parent
                        visible: face !== 0
                        width: parent.width * 0.55
                        height: width
                        name: glyph
                        color: tint
                    }

                    TapHandler {
                        onTapped: page.flip(index)
                    }
                }
            }
        }

        GameResultOverlay {
            anchors.fill: parent
            visible: page.finished
            score: page.score
            total: page.pairs
            onReplay: page.restart()
            onExit: page.navWin.popPage()
        }
    }

    Timer {
        id: flipBackTimer
        interval: 720
        onTriggered: {
            if (page.pendingA >= 0) {
                deck.setProperty(page.pendingA, "face", 0);
            }
            if (page.pendingB >= 0) {
                deck.setProperty(page.pendingB, "face", 0);
            }
            page.pendingA = -1;
            page.pendingB = -1;
            page.busy = false;
        }
    }
}
