import QtQuick
import QtQuick.Layouts
import "ChessEngine.js" as Chess

/*
 * 国际象棋（孩子执白，电脑执黑）。
 *
 * 规则全部在 ChessEngine.js 里，这里只负责「点哪里、画什么」：
 *   点自己的棋子 → 绿点标出能走的格子 → 点绿点落子 → 电脑应一手。
 *
 * 给孩子做的几处让步：
 *   - 不显示代数记谱，只说「轮到你走」「将军」这种人话；
 *   - 兵到底线自动升变成皇后（不弹选择框，孩子选不明白）；
 *   - 每局都可以悔棋，退一步就是连电脑那步一起退；
 *   - 三档难度只改电脑看几步，棋盘和规则完全一样。
 *
 * 棋盘方向：白棋在下面（y 越大越靠下），和 ChessEngine 的坐标一致，
 * 所以不需要翻转，界面上第 0 行就是黑方底线。
 */
GameShell {
    id: page

    title: "国际象棋"
    glyph: "chess"
    accent: "#7B5E48"
    prompt: page.hintText
    scoreText: "第 " + page.moveNo + " 步"

    // 当前局面（不可变对象，走一步换一个新的）
    property var game: Chess.newGame()
    // 走过的棋：[{ "state": 走完之后的局面, "move": 着法 }]，悔棋用
    property var history: []
    property var lastMove: null
    property int selected: -1
    property bool thinking: false
    property bool finished: false
    property string outcome: ""      // win / lose / draw
    property string endText: ""
    property int moveNo: 1

    readonly property int level: Math.max(1, Math.min(3, page.difficulty))
    readonly property bool inCheckNow: !page.finished && Chess.inCheck(page.game, page.game.turn)

    // 选中的棋子能走哪儿；点完就清空
    readonly property var targets: (page.selected < 0 || page.thinking || page.finished)
                                   ? [] : Chess.legalMovesFrom(page.game, page.selected)
    readonly property var targetMap: {
        const map = {};
        for (let i = 0; i < page.targets.length; i++) {
            map[page.targets[i].to] = true;
        }
        return map;
    }

    readonly property string hintText: {
        if (page.finished) {
            return "这一局结束了，可以点「再来一局」";
        }
        if (page.thinking) {
            return "电脑在想下一步…";
        }
        if (page.inCheckNow) {
            return "将军！先把王走到安全的地方";
        }
        if (page.selected >= 0) {
            return "绿色圆点是可以走的位置，圈住的是可以吃的棋子";
        }
        return "点一下自己的棋子，看看它能走到哪里";
    }

    readonly property string headline: {
        if (page.finished) {
            if (page.outcome === "win") {
                return "将死电脑，你赢啦！";
            }
            if (page.outcome === "lose") {
                return "这一局电脑赢了";
            }
            return "和棋，也很厉害";
        }
        if (page.thinking) {
            return "电脑在想…";
        }
        if (page.inCheckNow) {
            return "将军！";
        }
        return "轮到你走";
    }

    // 棋盘一格的边长：先按高度算，再按宽度留出右边的面板，最后卡个上下限。
    // 宽度这边扣掉的是「面板 300 + 间距 32 + 左侧坐标 30 + 一点余量」。
    readonly property real cell: {
        const byHeight = (page.contentArea.height - 46) / 8;
        const byWidth = (page.contentArea.width - 372) / 8;
        return Math.max(38, Math.min(76, Math.floor(Math.min(byHeight, byWidth))));
    }

    // ---- 走棋 ----

    function tapSquare(sq) {
        if (page.finished || page.thinking) {
            return;
        }
        const p = page.game.board[sq];
        if (p !== "" && p.charAt(0) === "w") {
            page.selected = (page.selected === sq) ? -1 : sq;
            return;
        }
        if (page.selected < 0) {
            return;
        }
        for (let i = 0; i < page.targets.length; i++) {
            if (page.targets[i].to === sq) {
                page.playMove(page.targets[i], false);
                return;
            }
        }
        page.selected = -1;
    }

    function playMove(mv, byAI) {
        const next = Chess.applyMove(page.game, mv);
        page.history = page.history.concat([{ "state": next, "move": mv }]);
        page.game = next;
        page.lastMove = mv;
        page.selected = -1;
        page.moveNo = Math.floor(page.history.length / 2) + 1;

        if (page.checkEnd()) {
            return;
        }
        if (!byAI) {
            // 立刻进入「电脑在想」：这 0.4 秒里如果不锁住，孩子还能再点一个棋子，
            // 虽然引擎会拦住（轮不到白棋走），但界面上「点了没反应」更像坏了。
            page.thinking = true;
            aiTimer.restart();
        }
    }

    function checkEnd() {
        const st = Chess.status(page.game);
        if (st === "checkmate") {
            // 被将死的是「该走棋的那一方」：轮到黑棋走却又没棋走，说明白棋赢了
            page.outcome = (page.game.turn === "b") ? "win" : "lose";
            page.finishGame();
            return true;
        }
        if (st === "stalemate" || st === "draw50" || st === "material") {
            page.outcome = "draw";
            page.finishGame();
            return true;
        }
        return false;
    }

    function finishGame() {
        page.thinking = false;
        page.finished = true;
        if (page.outcome === "win") {
            page.endText = "你将死了电脑，用了 " + page.moveNo + " 步";
        } else if (page.outcome === "lose") {
            page.endText = "电脑赢了这一局，再来一次吧";
        } else {
            page.endText = "和棋，双方都很难取胜";
        }
    }

    function undo() {
        aiTimer.stop();
        aiWork.stop();
        page.thinking = false;
        page.finished = false;

        let h = page.history.slice();
        if (h.length === 0) {
            return;
        }
        h.pop();                                        // 先撤掉最后一步
        if (h.length > 0 && h[h.length - 1].state.turn === "b") {
            h.pop();                                    // 如果撤的是电脑的应手，再撤自己那步
        }
        page.game = h.length > 0 ? h[h.length - 1].state : Chess.newGame();
        page.history = h;
        page.lastMove = h.length > 0 ? h[h.length - 1].move : null;
        page.selected = -1;
        page.moveNo = Math.floor(h.length / 2) + 1;
        page.outcome = "";
        page.endText = "";
    }

    function restart() {
        aiTimer.stop();
        aiWork.stop();
        page.game = Chess.newGame();
        page.history = [];
        page.lastMove = null;
        page.selected = -1;
        page.thinking = false;
        page.finished = false;
        page.outcome = "";
        page.endText = "";
        page.moveNo = 1;
    }

    // ---- 电脑 ----

    Timer {
        id: aiTimer

        interval: 420      // 留一点「思考」的时间，孩子能看清自己刚走的那一步
        onTriggered: {
            if (page.finished) {
                page.thinking = false;
                return;
            }
            aiWork.restart();
        }
    }

    Timer {
        id: aiWork

        interval: 40       // 先让「电脑在想」这行字画出来，再跑同步搜索
        onTriggered: {
            if (page.finished) {
                page.thinking = false;
                return;
            }
            const mv = Chess.bestMove(page.game, page.level);
            page.thinking = false;
            if (!mv) {
                page.finishGame();
                return;
            }
            page.playMove(mv, true);
        }
    }

    // ---- 界面 ----

    Item {
        anchors.fill: page.contentArea

        RowLayout {
            anchors.centerIn: parent
            spacing: 32

            // 棋盘（外框 + 坐标 + 64 格）
            Item {
                Layout.alignment: Qt.AlignVCenter
                width: page.cell * 8 + 30
                height: page.cell * 8 + 30

                Rectangle {
                    x: 22
                    y: -4
                    width: page.cell * 8 + 8
                    height: page.cell * 8 + 8
                    radius: 10
                    color: "#7B5E48"
                }

                Grid {
                    x: 26
                    y: 0
                    columns: 8
                    rows: 8

                    Repeater {
                        model: 64

                        Item {
                            required property int index

                            readonly property int sx: index % 8
                            readonly property int sy: Math.floor(index / 8)
                            readonly property bool lightSquare: (sx + sy) % 2 === 0
                            readonly property string piece: page.game.board[index]
                            readonly property bool hasPiece: piece !== ""
                            readonly property bool isTarget: page.targetMap[index] === true
                            readonly property bool isSelected: page.selected === index
                            readonly property bool isLastFrom: page.lastMove !== null
                                                             && page.lastMove.from === index
                            readonly property bool isLastTo: page.lastMove !== null
                                                           && page.lastMove.to === index
                            readonly property bool isCheckedKing: page.inCheckNow
                                                                  && piece === (page.game.turn + "K")

                            width: page.cell
                            height: page.cell

                            Rectangle {
                                anchors.fill: parent
                                color: parent.lightSquare ? "#F8EAD6" : "#CFA478"
                            }

                            // 上一步 / 选中的淡黄罩
                            Rectangle {
                                anchors.fill: parent
                                visible: parent.isLastFrom || parent.isLastTo || parent.isSelected
                                color: parent.isSelected ? "#4D7E57C2" : "#40F2C14E"
                            }

                            // 被将军的王：整格泛红
                            Rectangle {
                                anchors.fill: parent
                                visible: parent.isCheckedKing
                                color: "#55E53935"
                            }

                            ChessPiece {
                                anchors.centerIn: parent
                                width: Math.round(parent.width * 0.88)
                                height: Math.round(parent.height * 0.88)
                                visible: parent.hasPiece
                                type: parent.hasPiece ? parent.piece.charAt(1) : "P"
                                white: parent.hasPiece && parent.piece.charAt(0) === "w"
                            }

                            // 能走的位置：空格画小圆点，能吃子画一圈
                            Rectangle {
                                anchors.centerIn: parent
                                visible: parent.isTarget
                                width: parent.hasPiece ? parent.width - 4 : Math.round(parent.width * 0.3)
                                height: width
                                radius: width / 2
                                color: parent.hasPiece ? "transparent" : "#66000000"
                                border.width: parent.hasPiece ? 4 : 0
                                border.color: "#66000000"
                            }

                            TapHandler {
                                onTapped: page.tapSquare(index)
                            }
                        }
                    }
                }

                // 坐标：左边 8~1，下面 a~h
                Repeater {
                    model: 8

                    Text {
                        required property int index

                        x: 6
                        y: index * page.cell + (page.cell - height) / 2
                        text: 8 - index
                        font.pixelSize: Math.max(10, Math.round(page.cell * 0.24))
                        color: "#B29A82"
                    }
                }

                Repeater {
                    model: 8

                    Text {
                        required property int index

                        x: 26 + index * page.cell + (page.cell - width) / 2
                        y: page.cell * 8 + 6
                        text: "abcdefgh".charAt(index)
                        font.pixelSize: Math.max(10, Math.round(page.cell * 0.24))
                        color: "#B29A82"
                    }
                }
            }

            // 右侧：状态 + 操作
            ColumnLayout {
                Layout.alignment: Qt.AlignVCenter
                Layout.preferredWidth: 300
                spacing: 16

                Rectangle {
                    Layout.fillWidth: true
                    Layout.preferredHeight: 128
                    radius: 24
                    color: "#FFFFFF"
                    border.color: "#F2E1CD"
                    border.width: 1

                    ColumnLayout {
                        anchors.fill: parent
                        anchors.margins: 18
                        spacing: 8

                        Text {
                            Layout.fillWidth: true
                            text: page.headline
                            font.pixelSize: 23
                            font.weight: Font.Bold
                            color: page.outcome === "lose" ? "#8D6E63"
                                 : (page.inCheckNow ? "#D84315" : "#4A3B32")
                            wrapMode: Text.Wrap
                        }

                        Text {
                            Layout.fillWidth: true
                            text: page.hintText
                            font.pixelSize: 14
                            color: "#9B8574"
                            wrapMode: Text.Wrap
                        }
                    }
                }

                Text {
                    Layout.fillWidth: true
                    text: "你执白棋（在下面），先走。电脑执黑棋。"
                    font.pixelSize: 14
                    color: "#9B8574"
                    wrapMode: Text.Wrap
                }

                RowLayout {
                    Layout.fillWidth: true
                    spacing: 12

                    Rectangle {
                        Layout.fillWidth: true
                        Layout.preferredHeight: 58
                        radius: 20
                        color: undoArea.active ? "#EFE7DE" : "#FFFFFF"
                        border.color: "#EADBC8"
                        border.width: 1
                        opacity: page.history.length === 0 ? 0.45 : 1

                        Text {
                            anchors.centerIn: parent
                            text: "悔棋"
                            font.pixelSize: 18
                            font.weight: Font.DemiBold
                            color: "#7A6759"
                        }

                        TapHandler {
                            id: undoArea
                            onTapped: page.undo()
                        }
                    }

                    Rectangle {
                        Layout.fillWidth: true
                        Layout.preferredHeight: 58
                        radius: 20
                        color: "#7B5E48"

                        Text {
                            anchors.centerIn: parent
                            text: "重新开始"
                            font.pixelSize: 18
                            font.weight: Font.Bold
                            color: "white"
                        }

                        TapHandler {
                            onTapped: page.restart()
                        }
                    }
                }

                Text {
                    Layout.fillWidth: true
                    text: "小提示：兵一直往前走，走到最对面就能变成皇后。"
                    font.pixelSize: 13
                    color: "#B8A492"
                    wrapMode: Text.Wrap
                }
            }
        }

        GameResultOverlay {
            anchors.fill: parent
            visible: page.finished
            summary: page.endText
            onReplay: page.restart()
            onExit: page.navWin.popPage()
        }
    }
}
