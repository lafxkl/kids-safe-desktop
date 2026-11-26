import QtQuick
import QtQuick.Layouts
import KidsSafe 1.0

/*
 * 华容道（4×5 棋盘，三个布局）。
 *
 * 玩法：曹操是那个 2×2 的大块，把它挪到棋盘最下面中间的出口就算赢。
 * 棋子只能整格平移，不能跳、不能斜走 —— 和传统木制华容道一样。
 *
 * 操作做了两条路，因为孩子的手不稳：
 *   1. 按住棋子往一个方向拖，格子对得上就跟着走（拖动时按格吸附，不会卡在半格）；
 *   2. 先点一下棋子，再点右边的方向键，一格一格挪。
 *
 * 三个布局都用 A* 验过「一定能解开」，括号里是单格移动的最少步数：
 *   简单 6 个棋子（14 步）、中等 8 个（27 步）、困难 9 个（51 步）。
 * 困难这局比经典「横刀立马」少三个兵 —— 经典要 116 步，对 8 岁的孩子太长，
 * 51 步已经够他琢磨一阵了。
 *
 * ---- 联机为什么是「竞速」而不是「一人一步」----
 *
 * 华容道是单人解谜，两个人在同一副棋子上轮流挪，只会互相拆台：你挪开的
 * 空位对方立刻就堵上了，玩下来既不像合作也不像对战。所以联机做成竞速：
 * 两边拿同一道题、同一时刻开始，谁先解出来谁赢。规则公平（同一道题）、
 * 同步量极小（只需要开始时刻和完成时间）、输了也知道输在哪（慢了）。
 *
 * 对手先解出来时不打断还在解的孩子：他这一盘可以继续解完，只是顶部会
 * 多一句「小伙伴 1 分 20 秒就解出来了」。这个年纪，被强行打断比输更难受。
 */
GameShell {
    id: page

    title: "华容道"
    glyph: "blocks"
    accent: "#8D6E63"
    prompt: page.hintText
    scoreText: page.online && page.raceStarted
               ? page.steps + " 步 · " + page.clockText
               : page.steps + " 步"

    readonly property int cols: 4
    readonly property int rows: 5
    readonly property int pad: 16

    property var pieces: []
    property var history: []
    property int steps: 0
    property int selected: -1
    property bool finished: false
    property int dragIndex: -1
    property var dragStart: ({ "x": 0, "y": 0 })

    // ---- 联机竞速 ----
    readonly property bool online: Netplay.state === "playing" || Netplay.state === "done"
    // 本地时间轴上的开始时刻（毫秒）。两台机器的系统时钟不可能一样准，
    // 所以只能用「服务器说还有几秒」换算成自己这边的时间点。
    property double startAtMs: 0
    property bool raceStarted: false
    property int countdownLeft: 0
    property int elapsedMs: 0
    property bool reported: false

    // 联机时难度用房主定的那一档：两个人必须解同一道题，否则「谁快」没有意义
    readonly property int level: {
        if (page.online) {
            const lv = Netplay.room.level;
            return (lv === 1 || lv === 2 || lv === 3) ? lv : 2;
        }
        return Math.max(1, Math.min(3, page.difficulty));
    }
    readonly property int bestSteps: page.level === 1 ? 14 : (page.level === 2 ? 27 : 51)

    readonly property string clockText: page.fmtMs(page.elapsedMs)

    readonly property var myResult: {
        const list = (page.online && Netplay.room.results) ? Netplay.room.results : [];
        for (let i = 0; i < list.length; i++) {
            if (list[i].seat === Netplay.seat) {
                return list[i];
            }
        }
        return null;
    }

    readonly property var rivalResult: {
        const list = (page.online && Netplay.room.results) ? Netplay.room.results : [];
        const seat = Netplay.seat === "a" ? "b" : "a";
        for (let i = 0; i < list.length; i++) {
            if (list[i].seat === seat) {
                return list[i];
            }
        }
        return null;
    }

    readonly property string hintText: {
        if (!page.online) {
            return page.finished ? "曹操出去了，厉害！"
                                 : "拖住棋子往空处挪，把「曹操」送到最下面的出口";
        }
        if (!page.raceStarted) {
            return page.countdownLeft > 0 ? "准备… " + page.countdownLeft : "马上开始";
        }
        if (page.finished) {
            return "你解出来了！";
        }
        if (page.rivalResult !== null) {
            return "小伙伴 " + page.fmtMs(page.rivalResult.ms) + " 就解出来了，加油追！";
        }
        return "同一道题，谁先把曹操送出去谁赢";
    }

    // 结算区的一句话：两边成绩都到齐才比较，先到的只说「等小伙伴」
    readonly property string onlineSummary: {
        const mine = page.myResult;
        const rival = page.rivalResult;
        if (mine === null) {
            return "正在结算…";
        }
        const mineText = page.fmtMs(mine.ms) + " · " + mine.steps + " 步";
        if (rival === null) {
            return "你用了 " + mineText + "，小伙伴还在解";
        }
        if (mine.ms <= rival.ms) {
            return "你先解出来！你 " + mineText + "，小伙伴 " + page.fmtMs(rival.ms);
        }
        return "小伙伴快一点：他 " + page.fmtMs(rival.ms) + "，你 " + mineText;
    }

    onOnlineChanged: {
        page.resetRace();
        page.restart();
        if (page.online) {
            page.syncRace();
        }
    }

    Connections {
        target: Netplay

        function onRoomChanged() {
            if (page.online) {
                page.syncRace();
            }
        }
    }

    function fmtMs(ms) {
        const total = Math.max(0, Math.round(ms / 1000));
        const m = Math.floor(total / 60);
        const s = total % 60;
        return m > 0 ? (m + " 分 " + s + " 秒") : (s + " 秒");
    }

    function resetRace() {
        page.startAtMs = 0;
        page.raceStarted = false;
        page.countdownLeft = 0;
        page.elapsedMs = 0;
        page.reported = false;
    }

    // 服务端给了「什么时候开始」，换算成本地的时间点。只算一次：
    // 每次轮询都重算会让倒计时来回抖，已经开跑之后也不该再被网络延迟拉扯。
    function syncRace() {
        if (!page.online) {
            return;
        }
        const room = Netplay.room;
        if (!room.startAt || room.startAt <= 0) {
            return;
        }
        if (page.startAtMs === 0) {
            page.startAtMs = Date.now() + (room.startAt - room.now) * 1000;
        }
        page.tickClock();
    }

    function tickClock() {
        if (!page.online || page.startAtMs <= 0) {
            return;
        }
        const left = page.startAtMs - Date.now();
        page.countdownLeft = Math.max(0, Math.ceil(left / 1000));
        page.raceStarted = left <= 0;
        if (page.raceStarted) {
            page.elapsedMs = Date.now() - page.startAtMs;
        }
    }

    // 能不能动手：人机看「解完没」，联机还要等倒计时结束
    function canPlayNow() {
        if (page.finished) {
            return false;
        }
        return page.online ? page.raceStarted : true;
    }

    Timer {
        interval: 200
        repeat: true
        running: page.online && page.startAtMs > 0 && !page.finished
        onTriggered: page.tickClock()
    }

    readonly property real cell: {
        const byHeight = (page.contentArea.height - 70) / 5;
        const byWidth = (page.contentArea.width - 400) / 4;
        return Math.max(48, Math.min(104, Math.floor(Math.min(byHeight, byWidth))));
    }
    readonly property int gap: Math.max(3, Math.round(page.cell * 0.05))

    // ---- 布局 ----

    function layoutFor(level) {
        const cao = { "name": "曹操", "x": 1, "y": 0, "w": 2, "h": 2 };
        const guan = { "name": "关羽", "x": 1, "y": 2, "w": 2, "h": 1 };

        if (level === 1) {
            return [
                cao, guan,
                { "name": "张飞", "x": 0, "y": 1, "w": 1, "h": 2 },
                { "name": "黄忠", "x": 3, "y": 1, "w": 1, "h": 2 },
                { "name": "兵", "x": 0, "y": 0, "w": 1, "h": 1 },
                { "name": "兵", "x": 3, "y": 0, "w": 1, "h": 1 }
            ];
        }

        const base = [
            cao, guan,
            { "name": "张飞", "x": 0, "y": 0, "w": 1, "h": 2 },
            { "name": "赵云", "x": 3, "y": 0, "w": 1, "h": 2 },
            { "name": "马超", "x": 0, "y": 2, "w": 1, "h": 2 },
            { "name": "黄忠", "x": 3, "y": 2, "w": 1, "h": 2 },
            { "name": "兵", "x": 1, "y": 3, "w": 1, "h": 1 },
            { "name": "兵", "x": 2, "y": 3, "w": 1, "h": 1 }
        ];
        if (level === 2) {
            return base;
        }
        return base.concat([
            { "name": "兵", "x": 0, "y": 4, "w": 1, "h": 1 }
        ]);
    }

    function pieceColor(name) {
        switch (name) {
        case "曹操": return "#C62828";
        case "关羽": return "#2E7D32";
        case "张飞": return "#5E35B1";
        case "赵云": return "#1565C0";
        case "马超": return "#00838F";
        case "黄忠": return "#EF6C00";
        case "兵": return "#A1887F";
        default: return "#8D6E63";
        }
    }

    // ---- 规则 ----

    function canPlace(i, nx, ny) {
        const p = page.pieces[i];
        if (nx < 0 || ny < 0 || nx + p.w > page.cols || ny + p.h > page.rows) {
            return false;
        }
        for (let j = 0; j < page.pieces.length; j++) {
            if (j === i) {
                continue;
            }
            const o = page.pieces[j];
            if (nx < o.x + o.w && nx + p.w > o.x && ny < o.y + o.h && ny + p.h > o.y) {
                return false;
            }
        }
        return true;
    }

    // 整体替换数组：QML 不会盯 JS 数组内部的改动，换掉引用才会重画
    function moveTo(i, nx, ny, countStep) {
        const copy = page.pieces.slice();
        const p = copy[i];
        copy[i] = { "name": p.name, "x": nx, "y": ny, "w": p.w, "h": p.h };
        page.pieces = copy;
        if (countStep) {
            page.steps++;
            page.history = page.history.concat([{ "i": i, "x": p.x, "y": p.y }]);
        }
    }

    // 从当前位置一格一格推向目标格，走不动就停在能到的地方
    function slideTo(i, tx, ty) {
        let guard = 0;
        while (guard++ < 12) {
            const p = page.pieces[i];
            if (p.x === tx && p.y === ty) {
                break;
            }
            let nx = p.x;
            let ny = p.y;
            if (nx !== tx) {
                nx += (tx > nx) ? 1 : -1;
            } else {
                ny += (ty > ny) ? 1 : -1;
            }
            if (!page.canPlace(i, nx, ny)) {
                break;
            }
            page.moveTo(i, nx, ny, true);
        }
        page.checkWin();
    }

    function beginDrag(i) {
        if (!page.canPlayNow()) {
            return;
        }
        page.dragIndex = i;
        page.selected = i;
        page.dragStart = { "x": page.pieces[i].x, "y": page.pieces[i].y };
    }

    function dragBy(dx, dy) {
        const i = page.dragIndex;
        if (i < 0 || !page.canPlayNow()) {
            return;
        }
        const cx = Math.round(dx / page.cell);
        const cy = Math.round(dy / page.cell);
        // 华容道的棋子一次只沿一个方向走：哪边拖得多就认哪边
        if (Math.abs(cx) >= Math.abs(cy)) {
            page.slideTo(i, page.dragStart.x + cx, page.dragStart.y);
        } else {
            page.slideTo(i, page.dragStart.x, page.dragStart.y + cy);
        }
    }

    function nudge(dx, dy) {
        if (!page.canPlayNow() || page.selected < 0) {
            return;
        }
        const p = page.pieces[page.selected];
        const nx = p.x + dx;
        const ny = p.y + dy;
        if (page.canPlace(page.selected, nx, ny)) {
            page.moveTo(page.selected, nx, ny, true);
            page.checkWin();
        }
    }

    function checkWin() {
        const cao = page.pieces[0];
        if (cao !== undefined && cao.x === 1 && cao.y === 3) {
            page.finished = true;
            page.dragIndex = -1;
            if (page.online && !page.reported) {
                page.reported = true;
                // 只报步数：耗时由服务端按它记的开始时刻算，不信客户端报的
                // 毫秒数（两台机器的计时器精度和起点都不一样）。
                Netplay.reportKlotskiFinish(page.steps);
            }
        }
    }

    function undo() {
        if (page.history.length === 0 || page.finished) {
            return;
        }
        const h = page.history.slice();
        const last = h.pop();
        page.history = h;
        page.moveTo(last.i, last.x, last.y, false);
        page.steps = Math.max(0, page.steps - 1);
        page.selected = last.i;
    }

    function restart() {
        page.pieces = page.layoutFor(page.level).slice();
        page.history = [];
        page.steps = 0;
        page.selected = -1;
        page.finished = false;
        page.dragIndex = -1;
    }

    Component.onCompleted: page.restart()

    // ---- 界面 ----

    Item {
        anchors.fill: page.contentArea

        /*
         * 棋盘和右侧面板各自算位置，不再用 RowLayout。
         *
         * 原来这里是 RowLayout + 一个 `width: cell*4+32` 的 Item。布局管理器
         * 会按「尺寸提示」重新给子项分配宽度，而给一个 Item 直接写 width 只
         * 是普通绑定：布局一赋值就把绑定顶掉了，Item 的实际宽度变成 0。盘面
         * 里的 Grid 用的是绝对坐标，于是从 0 宽度的父项里溢出来照画不误，
         * 右边面板则贴着「宽度为 0 的棋盘」摆过来 —— 看上去就是面板压在盘面
         * 上，而棋盘整体偏到右边去了。
         *
         * 现在宽度全部写死在这里算：棋盘在「扣掉面板」之后的空白里居中，
         * 面板贴右边。cell 本来就是按 contentArea 算出来的，两者不会打架。
         */
        Item {
            id: stage

            anchors.fill: parent

            readonly property int gap: 36
            readonly property int panelW: 320
            readonly property real boardW: page.cell * page.cols + page.pad * 2
            readonly property real boardH: page.cell * page.rows + page.pad * 2 + 26

            readonly property int boardX: Math.max(0, Math.round((width - panelW - gap - boardW) / 2))
            readonly property int boardY: Math.max(0, Math.round((height - boardH) / 2))

            // 棋盘
            Item {
                x: stage.boardX
                y: stage.boardY
                width: stage.boardW
                height: stage.boardH

                Rectangle {
                    x: 0
                    y: 0
                    width: page.cell * page.cols + page.pad * 2
                    height: page.cell * page.rows + page.pad * 2
                    radius: 22
                    color: "#6D4C41"
                    border.color: "#5D4037"
                    border.width: 2

                    // 棋盘格：浅色底 + 细缝，方便数格子
                    Grid {
                        x: page.pad
                        y: page.pad
                        columns: page.cols
                        rows: page.rows

                        Repeater {
                            model: page.cols * page.rows

                            Rectangle {
                                required property int index

                                readonly property int gx: index % page.cols
                                readonly property int gy: Math.floor(index / page.cols)
                                readonly property bool isExit: gy === 4 && (gx === 1 || gx === 2)

                                width: page.cell
                                height: page.cell
                                color: "transparent"

                                Rectangle {
                                    anchors.fill: parent
                                    anchors.margins: 2
                                    radius: 8
                                    color: parent.isExit ? "#4E342E" : "#7B584B"
                                    border.color: parent.isExit ? "#FFCC80" : "transparent"
                                    border.width: parent.isExit ? 2 : 0
                                }
                            }
                        }
                    }

                    // 出口箭头
                    Text {
                        x: page.pad + page.cell
                        y: page.pad + page.cell * 4 + (page.cell - height) / 2
                        width: page.cell * 2
                        text: "↓"
                        horizontalAlignment: Text.AlignHCenter
                        font.pixelSize: Math.round(page.cell * 0.5)
                        font.weight: Font.Bold
                        color: "#FFCC80"
                        opacity: 0.9
                    }
                }

                Text {
                    x: 0
                    y: page.cell * page.rows + page.pad * 2 + 4
                    width: page.cell * page.cols + page.pad * 2
                    text: "出口在这里"
                    horizontalAlignment: Text.AlignHCenter
                    font.pixelSize: 14
                    color: "#9B8574"
                }

                // 棋子
                //
                // model 用「个数」而不是数组本身：数组每次走子都会整体换新，
                // Repeater 会把所有格子拆了重建，Behavior 的滑动动画就没了。
                // 用下标取数据，delegate 原地更新 x/y，动画才连得上。
                Repeater {
                    model: page.pieces.length

                    Rectangle {
                        required property int index

                        readonly property var piece: page.pieces[index] !== undefined
                                                   ? page.pieces[index]
                                                   : ({ "name": "", "x": 0, "y": 0, "w": 1, "h": 1 })

                        x: page.pad + piece.x * page.cell + Math.round(page.gap / 2)
                        y: page.pad + piece.y * page.cell + Math.round(page.gap / 2)
                        width: piece.w * page.cell - page.gap
                        height: piece.h * page.cell - page.gap
                        radius: 14
                        color: page.pieceColor(piece.name)
                        border.width: page.selected === index ? 4 : 2
                        border.color: page.selected === index ? "#FFD54F" : "#00000033"

                        Behavior on x {
                            NumberAnimation { duration: 110; easing.type: Easing.OutCubic }
                        }
                        Behavior on y {
                            NumberAnimation { duration: 110; easing.type: Easing.OutCubic }
                        }

                        Text {
                            anchors.centerIn: parent
                            text: parent.piece.w * parent.piece.h >= 4
                                  ? parent.piece.name
                                  : parent.piece.name.charAt(0)
                            font.pixelSize: Math.round(Math.min(parent.width, parent.height)
                                                       * (parent.piece.name === "曹操" ? 0.34 : 0.42))
                            font.weight: Font.Bold
                            color: "#FFF8F0"
                        }

                        // 拖动用 MouseArea 而不是 DragHandler：
                        // DragHandler 默认会自己搬动 target 的 x/y，正好会顶掉这里的
                        // 绑定（位置是按格子算出来的，不能被它随便改）；而且棋子跟着
                        // 手走的时候，局部坐标会跟着变，位移就不准了 —— 所以取场景坐标。
                        MouseArea {
                            anchors.fill: parent

                            property point pressScene: Qt.point(0, 0)

                            onPressed: (mouse) => {
                                pressScene = mapToItem(null, mouse.x, mouse.y);
                                page.beginDrag(index);
                            }

                            onPositionChanged: (mouse) => {
                                if (!pressed) {
                                    return;
                                }
                                const p = mapToItem(null, mouse.x, mouse.y);
                                page.dragBy(p.x - pressScene.x, p.y - pressScene.y);
                            }

                            onReleased: page.dragIndex = -1
                            onCanceled: page.dragIndex = -1
                        }
                    }
                }

                // 联机倒计时：盖住棋盘，3 → 2 → 1，开始后自己消失
                Rectangle {
                    anchors.fill: parent
                    visible: page.online && !page.raceStarted
                    radius: 22
                    color: "#F2FBF4EA"

                    Column {
                        anchors.centerIn: parent
                        spacing: 6

                        Text {
                            anchors.horizontalCenter: parent.horizontalCenter
                            text: page.countdownLeft > 0 ? page.countdownLeft : "开始"
                            font.pixelSize: 84
                            font.weight: Font.Bold
                            color: "#C62828"
                        }

                        Text {
                            anchors.horizontalCenter: parent.horizontalCenter
                            text: "同一道题，谁先送出去谁赢"
                            font.pixelSize: 15
                            color: "#8A7568"
                        }
                    }
                }
            }

            // 右侧：步数 / 方向键 / 按钮
            ColumnLayout {
                x: Math.max(0, stage.width - stage.panelW)
                y: Math.max(0, Math.round((stage.height - height) / 2))
                width: stage.panelW
                height: implicitHeight
                spacing: 16

                Rectangle {
                    Layout.fillWidth: true
                    Layout.preferredHeight: 104
                    radius: 24
                    color: "#FFFFFF"
                    border.color: "#F2E1CD"
                    border.width: 1

                    ColumnLayout {
                        anchors.fill: parent
                        anchors.margins: 18
                        spacing: 4

                        Text {
                            text: page.steps + " 步"
                            font.pixelSize: 30
                            font.weight: Font.Bold
                            color: "#4A3B32"
                        }

                        Text {
                            text: "最少 " + page.bestSteps + " 步可以送出去"
                            font.pixelSize: 14
                            color: "#9B8574"
                        }
                    }
                }

                // 联机时才出现的对战卡：自己的用时 + 对方解完没有
                Rectangle {
                    Layout.fillWidth: true
                    Layout.preferredHeight: 88
                    radius: 22
                    visible: page.online
                    color: "#FFFFFF"
                    border.color: "#F2E1CD"
                    border.width: 1

                    ColumnLayout {
                        anchors.fill: parent
                        anchors.margins: 16
                        spacing: 4

                        Text {
                            Layout.fillWidth: true
                            text: page.raceStarted ? page.clockText : "准备开始"
                            font.pixelSize: 24
                            font.weight: Font.Bold
                            color: "#2E6B3A"
                        }

                        Text {
                            Layout.fillWidth: true
                            text: page.rivalResult !== null
                                  ? ("小伙伴已完成：" + page.rivalResult.steps + " 步 · "
                                     + page.fmtMs(page.rivalResult.ms))
                                  : "小伙伴还在解"
                            font.pixelSize: 13
                            color: "#8A7568"
                            wrapMode: Text.Wrap
                        }
                    }
                }

                Text {
                    Layout.fillWidth: true
                    text: "按住棋子拖动，或者点一下棋子，再点下面的方向键。"
                    font.pixelSize: 14
                    color: "#9B8574"
                    wrapMode: Text.Wrap
                }

                // 方向键：3×3 的十字
                Grid {
                    Layout.alignment: Qt.AlignHCenter
                    columns: 3
                    spacing: 8

                    Item {
                        width: 62
                        height: 62
                    }

                    Rectangle {
                        width: 62
                        height: 62
                        radius: 18
                        color: upArea.active ? "#EFE7DE" : "#FFFFFF"
                        border.color: "#EADBC8"
                        border.width: 1

                        Text {
                            anchors.centerIn: parent
                            text: "↑"
                            font.pixelSize: 26
                            font.weight: Font.Bold
                            color: "#7A6759"
                        }

                        TapHandler {
                            id: upArea
                            onTapped: page.nudge(0, -1)
                        }
                    }

                    Item {
                        width: 62
                        height: 62
                    }

                    Rectangle {
                        width: 62
                        height: 62
                        radius: 18
                        color: leftArea.active ? "#EFE7DE" : "#FFFFFF"
                        border.color: "#EADBC8"
                        border.width: 1

                        Text {
                            anchors.centerIn: parent
                            text: "←"
                            font.pixelSize: 26
                            font.weight: Font.Bold
                            color: "#7A6759"
                        }

                        TapHandler {
                            id: leftArea
                            onTapped: page.nudge(-1, 0)
                        }
                    }

                    Rectangle {
                        width: 62
                        height: 62
                        radius: 18
                        color: "#FBF3E8"
                        border.color: "#EADBC8"
                        border.width: 1

                        Text {
                            anchors.centerIn: parent
                            text: "挪"
                            font.pixelSize: 18
                            font.weight: Font.DemiBold
                            color: "#B8A492"
                        }
                    }

                    Rectangle {
                        width: 62
                        height: 62
                        radius: 18
                        color: rightArea.active ? "#EFE7DE" : "#FFFFFF"
                        border.color: "#EADBC8"
                        border.width: 1

                        Text {
                            anchors.centerIn: parent
                            text: "→"
                            font.pixelSize: 26
                            font.weight: Font.Bold
                            color: "#7A6759"
                        }

                        TapHandler {
                            id: rightArea
                            onTapped: page.nudge(1, 0)
                        }
                    }

                    Item {
                        width: 62
                        height: 62
                    }

                    Rectangle {
                        width: 62
                        height: 62
                        radius: 18
                        color: downArea.active ? "#EFE7DE" : "#FFFFFF"
                        border.color: "#EADBC8"
                        border.width: 1

                        Text {
                            anchors.centerIn: parent
                            text: "↓"
                            font.pixelSize: 26
                            font.weight: Font.Bold
                            color: "#7A6759"
                        }

                        TapHandler {
                            id: downArea
                            onTapped: page.nudge(0, 1)
                        }
                    }

                    Item {
                        width: 62
                        height: 62
                    }
                }

                RowLayout {
                    Layout.fillWidth: true
                    spacing: 12

                    Rectangle {
                        Layout.fillWidth: true
                        Layout.preferredHeight: 56
                        radius: 20
                        color: undoArea.active ? "#EFE7DE" : "#FFFFFF"
                        border.color: "#EADBC8"
                        border.width: 1
                        opacity: (page.history.length === 0 && !page.online) ? 0.45 : 1

                        Text {
                            anchors.centerIn: parent
                            text: "退一步"
                            font.pixelSize: 17
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
                        Layout.preferredHeight: 56
                        radius: 20
                        color: "#8D6E63"

                        Text {
                            anchors.centerIn: parent
                            // 联机时不能「重新摆」：重来一次等于把这一局作废，
                            // 对面还在解同一道题，这就不算比赛了
                            text: page.online ? "离开房间" : "重新摆"
                            font.pixelSize: 17
                            font.weight: Font.Bold
                            color: "white"
                        }

                        TapHandler {
                            onTapped: {
                                if (page.online) {
                                    Netplay.leave();
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
                    Layout.preferredHeight: 56
                    radius: 20
                    visible: !page.online && Netplay.enabled
                    color: netArea.active ? "#DCEFE1" : "#EEF7EF"
                    border.color: "#B7D8BE"
                    border.width: 1

                    Text {
                        anchors.centerIn: parent
                        text: "和小伙伴比快"
                        font.pixelSize: 17
                        font.weight: Font.DemiBold
                        color: "#2E6B3A"
                    }

                    TapHandler {
                        id: netArea
                        onTapped: netPanel.visible = true
                    }
                }
            }
        }

        GameResultOverlay {
            anchors.fill: parent
            visible: page.finished
            summary: page.online
                     ? page.onlineSummary
                     : ("曹操送出去了！用了 " + page.steps + " 步（最少 " + page.bestSteps + " 步）")
            replayText: page.online ? "离开房间" : "再来一局"
            onReplay: page.restart()
            onExit: {
                if (page.online) {
                    Netplay.leave();
                }
                page.navWin.popPage();
            }
        }
    }

    // 联机面板盖满整页（连顶部栏一起）
    NetplayPanel {
        id: netPanel

        anchors.fill: parent
        game: "klotski"
        level: page.level
    }
}
