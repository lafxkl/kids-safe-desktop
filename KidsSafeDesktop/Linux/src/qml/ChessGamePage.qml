import QtQuick
import QtQuick.Layouts
import KidsSafe 1.0
import "ChessEngine.js" as Chess

/*
 * 国际象棋：人机（孩子执白、电脑执黑）和联机两种玩法共用这一页。
 *
 * 规则全部在 ChessEngine.js 里，这里只负责「点哪里、画什么」。
 *
 * 联机怎么同步（关键）：服务端只存一串走法，两边各自从开局重放。
 *   - 我走一步：先本地走出（孩子立刻看到落子），再发给服务器；
 *   - 对面走一步：1.5 秒轮询拿到新走法，按 onlineApplied 只补缺的那几步；
 *   - 万一本地局面和服务器对不上（理论上不该发生），replayAll 整盘重放。
 * 于是棋盘永远是同一份走法推出来的，不需要同步「棋盘快照」这种东西。
 *
 * 给孩子做的几处让步：
 *   - 不显示代数记谱，只说「轮到你走」「将军」这种人话；
 *   - 兵到底线自动升变成皇后（不弹选择框，孩子选不明白）；
 *   - 人机时每局都能悔棋；联机时不能 —— 服务器上的棋局退不回去，
 *     留着这个按钮只会让孩子以为「我悔了但对面还看得见」；
 *   - 三档难度只改电脑看几步，棋盘和规则完全一样。
 *
 * 棋盘方向：白棋在下面（y 越大越靠下），和 ChessEngine 的坐标一致，
 * 所以不需要翻转，界面上第 0 行就是黑方底线。联机时房主执白。
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

    // ---- 联机 ----
    // 从 Netplay 直接派生，不另外存一个 bool：房间开了就是 true，退房自动变
    // false。「同一件事存两份状态」是最难查的那种 bug。
    readonly property bool online: Netplay.state === "playing" || Netplay.state === "done"
    // 房主执白。进别人房间的孩子执黑，棋盘不翻转 —— 白子永远在下面，
    // 只是自己的子在上面，这比把棋盘转 180° 好理解。
    readonly property string mySide: Netplay.seat === "a" ? "w" : "b"
    // 服务器走法序列已经重放到第几步
    property int onlineApplied: 0
    // 我已经在本地走出、但还没出现在服务器列表里的那一步。
    // 没有它，轮询回来会把这步再走一遍（本地两下，对面只看到一下）。
    property var pendingMove: null

    // 现在能不能动手。联机时**按本地局面的轮次判**，不看服务器的 turn：
    // 本地走完一步，服务器的 turn 要 1.5 秒后才轮到；这段时间里如果只信
    // 服务器，孩子还能再点自己的另一个子（legalMovesFrom 不校验轮次），
    // 等于连走两步，随后被服务端 409 拒掉，棋盘就得整盘重放。
    readonly property bool canMoveNow: page.online
                                       ? (page.game.turn === page.mySide && !page.finished)
                                       : (!page.thinking && !page.finished)

    // 选中的棋子能走哪儿；点完就清空
    readonly property var targets: (page.selected < 0 || !page.canMoveNow)
                                   ? [] : Chess.legalMovesFrom(page.game, page.selected)

    onOnlineChanged: {
        if (page.online) {
            // 双方到齐（或断线后重连）都从开局摆起，再把服务器上已有的走法补上
            page.resetBoard();
            page.applyRemoteMoves();
        } else {
            page.resetBoard();
        }
    }

    Connections {
        target: Netplay

        function onRoomChanged() {
            if (!page.online) {
                return;
            }
            page.applyRemoteMoves();

            // 房间结束了，但本机还没走完最后一步（对面先报了结果，或者对面
            // 直接退出）。按服务端记的赢家给个结论，别让孩子对着僵住的棋盘。
            if (Netplay.room.status === "done" && !page.finished) {
                page.finished = true;
                const winner = Netplay.room.winner;
                if (winner === "draw") {
                    page.outcome = "draw";
                    page.endText = "和棋，双方都很难取胜";
                } else {
                    page.outcome = (winner === Netplay.seat) ? "win" : "lose";
                    if (Netplay.room.reason === "leave") {
                        page.endText = "小伙伴离开了房间";
                    } else {
                        page.endText = page.outcome === "win"
                                        ? "这一局你赢了"
                                        : "小伙伴赢了这一局，再来一次吧";
                    }
                }
            }
        }
    }
    readonly property var targetMap: {
        const map = {};
        for (let i = 0; i < page.targets.length; i++) {
            map[page.targets[i].to] = true;
        }
        return map;
    }

    readonly property string hintText: {
        if (page.finished) {
            return page.online ? "这一局结束了，可以点「离开房间」" : "这一局结束了，可以点「再来一局」";
        }
        if (page.online) {
            if (!page.canMoveNow) {
                return "等小伙伴走一步…";
            }
            if (page.inCheckNow) {
                return "将军！先把王走到安全的地方";
            }
            if (page.selected >= 0) {
                return "绿色圆点是可以走的位置，圈住的是可以吃的棋子";
            }
            return "轮到你走，点一下自己的棋子";
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
                return page.online ? "你赢啦！" : "将死电脑，你赢啦！";
            }
            if (page.outcome === "lose") {
                return page.online ? "小伙伴赢了" : "这一局电脑赢了";
            }
            return "和棋，也很厉害";
        }
        if (page.online) {
            if (page.inCheckNow) {
                return "将军！";
            }
            return page.canMoveNow ? "轮到你走" : "等小伙伴";
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
        if (!page.canMoveNow) {
            return;
        }
        const mine = page.online ? page.mySide : "w";
        const p = page.game.board[sq];
        if (p !== "" && p.charAt(0) === mine) {
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

    // 只把一步走到棋盘上：不管是谁走的，也不管之后要通知谁。
    // 拆出这一层是因为本地走子和联机重放都要用它，但后续动作完全不同。
    function commitMove(mv) {
        const next = Chess.applyMove(page.game, mv);
        page.history = page.history.concat([{ "state": next, "move": mv }]);
        page.game = next;
        page.lastMove = mv;
        page.selected = -1;
        page.moveNo = Math.floor(page.history.length / 2) + 1;
    }

    function playMove(mv, byAI) {
        page.commitMove(mv);

        if (page.online) {
            // 联机：本地先落子（孩子立刻看到），再告诉服务器；对面那步靠轮询补。
            // pendingMove 记下这一步，轮询回来时认得「这是我走的」，不再重放。
            if (!byAI) {
                page.pendingMove = mv;
                Netplay.sendMove(mv.from, mv.to, mv.promo);
            }
            page.checkEnd();
            return;
        }

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
            const winner = (page.game.turn === "b") ? "w" : "b";
            if (page.online) {
                page.outcome = (winner === page.mySide) ? "win" : "lose";
                page.finishGame();
                // 两边重放到同一个局面，算出的赢家必然一样，所以谁先报都行：
                // 服务端记最后一个，而两个值是同一个。不用抢。
                Netplay.reportChessResult(winner === "w" ? "a" : "b");
            } else {
                page.outcome = (winner === "w") ? "win" : "lose";
                page.finishGame();
            }
            return true;
        }
        if (st === "stalemate" || st === "draw50" || st === "material") {
            page.outcome = "draw";
            page.finishGame();
            if (page.online) {
                Netplay.reportChessResult("draw");
            }
            return true;
        }
        return false;
    }

    function finishGame() {
        page.thinking = false;
        page.finished = true;
        if (page.online) {
            if (page.outcome === "win") {
                page.endText = "你将死了小伙伴，用了 " + page.moveNo + " 步";
            } else if (page.outcome === "lose") {
                page.endText = "小伙伴赢了这一局，再来一次吧";
            } else {
                page.endText = "和棋，双方都很难取胜";
            }
            return;
        }
        if (page.outcome === "win") {
            page.endText = "你将死了电脑，用了 " + page.moveNo + " 步";
        } else if (page.outcome === "lose") {
            page.endText = "电脑赢了这一局，再来一次吧";
        } else {
            page.endText = "和棋，双方都很难取胜";
        }
    }

    function undo() {
        if (page.online) {
            return;      // 联机不能悔棋：服务器上的棋局退不回去
        }
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

    // 把棋盘摆回开局。人机、联机、断线重连都用它，所以里面不能碰 online。
    function resetBoard() {
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
        page.onlineApplied = 0;
        page.pendingMove = null;
    }

    function restart() {
        if (page.online) {
            // 联机时按「重新开始」等于退房回人机：对面可能已经走开，
            // 单方面重开一盘只会在两台机器上留下两个不同的棋盘。
            Netplay.leave();
            return;
        }
        page.resetBoard();
    }

    // ---- 联机同步 ----

    // 只补服务器上「还没重放」的那几步
    function applyRemoteMoves() {
        if (!page.online) {
            return;
        }
        const moves = Netplay.room.moves || [];
        if (moves.length === page.onlineApplied) {
            return;
        }
        if (moves.length < page.onlineApplied) {
            // 服务器上的步数比本地少：说明房间被重开过，整盘重来
            page.onlineApplied = 0;
        }
        while (page.onlineApplied < moves.length) {
            const raw = moves[page.onlineApplied];
            const promo = raw.p || "";

            if (page.pendingMove !== null
                    && raw.f === page.pendingMove.from
                    && raw.t === page.pendingMove.to
                    && promo === page.pendingMove.promo) {
                // 这就是我自己刚走的那步：棋盘上早就有了，只把进度往前推
                page.pendingMove = null;
                page.onlineApplied += 1;
                continue;
            }

            const mv = Chess.findMove(page.game, raw.f, raw.t, promo);
            if (!mv) {
                // 本地局面和服务器对不上（理论上不该发生）：以服务器的走法为准，
                // 整盘重放一遍，别硬接着走。
                page.replayAll(moves);
                return;
            }
            page.onlineApplied += 1;
            page.pendingMove = null;
            page.commitMove(mv);
        }
        page.checkEnd();
    }

    function replayAll(moves) {
        page.resetBoard();
        for (let i = 0; i < moves.length; i++) {
            const raw = moves[i];
            const mv = Chess.findMove(page.game, raw.f, raw.t, raw.p || "");
            if (!mv) {
                break;      // 还是对不上就停下，停在能重放的那一步
            }
            page.onlineApplied = i + 1;
            page.commitMove(mv);
        }
        page.checkEnd();
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
                    text: page.online
                          ? (page.mySide === "w"
                             ? "你执白棋（在下面），先走。对面是小伙伴。"
                             : "你执黑棋（在上面），小伙伴先走。")
                          : "你执白棋（在下面），先走。电脑执黑棋。"
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
                        color: leftArea.active ? "#EFE7DE" : "#FFFFFF"
                        border.color: "#EADBC8"
                        border.width: 1
                        // 人机时没棋可悔就置灰；联机时这个键是「离开房间」，永远可点
                        opacity: (!page.online && page.history.length === 0) ? 0.45 : 1

                        Text {
                            anchors.centerIn: parent
                            text: page.online ? "离开房间" : "悔棋"
                            font.pixelSize: 18
                            font.weight: Font.DemiBold
                            color: "#7A6759"
                        }

                        TapHandler {
                            id: leftArea
                            onTapped: {
                                if (page.online) {
                                    Netplay.leave();
                                } else {
                                    page.undo();
                                }
                            }
                        }
                    }

                    Rectangle {
                        Layout.fillWidth: true
                        Layout.preferredHeight: 58
                        radius: 20
                        color: "#7B5E48"

                        Text {
                            anchors.centerIn: parent
                            text: page.online ? "回到大厅" : "重新开始"
                            font.pixelSize: 18
                            font.weight: Font.Bold
                            color: "white"
                        }

                        TapHandler {
                            onTapped: {
                                if (page.online) {
                                    Netplay.leave();
                                    page.navWin.popPage();
                                } else {
                                    page.restart();
                                }
                            }
                        }
                    }
                }

                // 联机入口：家长把「在线内容」和「联机对战」都打开才出现
                Rectangle {
                    Layout.fillWidth: true
                    Layout.preferredHeight: 58
                    radius: 20
                    visible: !page.online && Netplay.enabled
                    color: netArea.active ? "#DCEFE1" : "#EEF7EF"
                    border.color: "#B7D8BE"
                    border.width: 1

                    Text {
                        anchors.centerIn: parent
                        text: "和小朋友下棋"
                        font.pixelSize: 18
                        font.weight: Font.DemiBold
                        color: "#2E6B3A"
                    }

                    TapHandler {
                        id: netArea
                        onTapped: netPanel.visible = true
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
            replayText: page.online ? "离开房间" : "再来一局"
            onReplay: page.restart()
            onExit: {
                if (page.online) {
                    // 直接退出页面也得先退房，不然服务器上要等 30 分钟才回收
                    Netplay.leave();
                }
                page.navWin.popPage();
            }
        }
    }

    // 联机面板盖满整页（连顶部栏一起），z 比内容区高
    NetplayPanel {
        id: netPanel

        anchors.fill: parent
        game: "chess"
        level: page.level
        onStarted: page.applyRemoteMoves()
    }
}
