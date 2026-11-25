import QtQuick
import QtQuick.Layouts

/*
 * 数独（4×4 儿童版），对应 Windows 端 SudokuPage。
 *
 * 规则按 4~8 岁改过：只有 1~4 四个数字、2×2 的小宫，填错不判负、不扣分，
 * 重复的数字标红提示，改掉就好。全部填满且没有重复才算完成。
 *
 * 棋盘用 16 个 {value, given} 对象放在 `cells` 里，改一格就整体替换一次
 * （copy-on-write）。QML 不会监听 JS 数组内部的改动，只有整个属性被重新赋值
 * 才会让绑定重新求值；16 个小对象的拷贝开销可以忽略，换来的是「改一格、
 * 整盘按新数据重画」这种最不容易出错的数据流。
 *
 * 出题不是随机填数，而是从一张合法的满盘出发：
 *   1. 把 1~4 随机重标号（仍然是合法盘面）；
 *   2. 随机挖空格，每挖一个都用回溯求解器验证「解唯一」，不唯一就填回去。
 * 这样保证每个孩子拿到的题一定能做出来，且只有一个答案。
 */
GameShell {
    id: page

    title: "数独"
    glyph: "sudoku"
    accent: "#3949AB"
    prompt: "把 1~4 填进空格，每行、每列、每个小宫都不能重复"
    scoreText: page.finished ? "" : (page.remaining + " 格待填")

    property var cells: []
    property var solution: []
    property int selected: -1
    property bool finished: false
    property bool started: false
    property int seconds: 0
    property int hints: 0

    // 一张合法的 4×4 满盘（每行/列/2×2 宫都是 1~4）
    readonly property var baseGrid: [1, 2, 3, 4,
                                     3, 4, 1, 2,
                                     2, 1, 4, 3,
                                     4, 3, 2, 1]

    readonly property int remaining: {
        let n = 0;
        for (let i = 0; i < page.cells.length; i++) {
            if (page.cells[i].value === 0) {
                n++;
            }
        }
        return n;
    }

    // 冲突格集合：{ 下标: true }。整盘每次改动都会重算（16 格，无所谓性能）。
    readonly property var conflictSet: page.findConflicts()

    readonly property string timeText: {
        const m = Math.floor(page.seconds / 60);
        const s = page.seconds % 60;
        return m > 0 ? (m + " 分 " + s + " 秒") : (s + " 秒");
    }

    // ---- 数据 ----

    function shuffled(list) {
        const copy = list.slice();
        for (let i = copy.length - 1; i > 0; i--) {
            const j = Math.floor(Math.random() * (i + 1));
            const tmp = copy[i];
            copy[i] = copy[j];
            copy[j] = tmp;
        }
        return copy;
    }

    function findConflicts() {
        const bad = {};
        const g = [];
        for (let i = 0; i < page.cells.length; i++) {
            g.push(page.cells[i].value);
        }

        for (let i = 0; i < 16; i++) {
            for (let j = i + 1; j < 16; j++) {
                if (g[i] === 0 || g[i] !== g[j]) {
                    continue;
                }
                const r1 = Math.floor(i / 4);
                const c1 = i % 4;
                const r2 = Math.floor(j / 4);
                const c2 = j % 4;
                const sameBox = Math.floor(r1 / 2) === Math.floor(r2 / 2)
                        && Math.floor(c1 / 2) === Math.floor(c2 / 2);
                if (r1 === r2 || c1 === c2 || sameBox) {
                    bad[i] = true;
                    bad[j] = true;
                }
            }
        }
        return bad;
    }

    // 数解的个数，最多数到 limit 就停（只关心「唯一 / 不唯一」）
    function countSolutions(grid, limit) {
        let count = 0;
        const g = grid.slice();

        function canPlace(idx, v) {
            const r = Math.floor(idx / 4);
            const c = idx % 4;
            for (let i = 0; i < 4; i++) {
                if (g[r * 4 + i] === v || g[i * 4 + c] === v) {
                    return false;
                }
            }
            const br = Math.floor(r / 2) * 2;
            const bc = Math.floor(c / 2) * 2;
            for (let dr = 0; dr < 2; dr++) {
                for (let dc = 0; dc < 2; dc++) {
                    if (g[(br + dr) * 4 + bc + dc] === v) {
                        return false;
                    }
                }
            }
            return true;
        }

        function walk() {
            if (count >= limit) {
                return;
            }
            let idx = -1;
            for (let i = 0; i < 16; i++) {
                if (g[i] === 0) {
                    idx = i;
                    break;
                }
            }
            if (idx === -1) {
                count++;
                return;
            }
            for (let v = 1; v <= 4; v++) {
                if (canPlace(idx, v)) {
                    g[idx] = v;
                    walk();
                    g[idx] = 0;
                    if (count >= limit) {
                        return;
                    }
                }
            }
        }

        walk();
        return count;
    }

    function makePuzzle() {
        // 随机重标号：盘面结构不变，数字换一身衣服，保证每局看起来不一样
        const perm = page.shuffled([1, 2, 3, 4]);
        const full = page.baseGrid.map(function (v) { return perm[v - 1]; });

        // 难度只改「挖几个空」：简单 6、中等 8、困难 10
        const want = page.difficulty === 1 ? 6 : (page.difficulty === 2 ? 8 : 10);
        const order = page.shuffled([0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15]);

        const puzzle = full.slice();
        let removed = 0;
        for (let k = 0; k < order.length && removed < want; k++) {
            const idx = order[k];
            const backup = puzzle[idx];
            puzzle[idx] = 0;
            if (page.countSolutions(puzzle, 2) === 1) {
                removed++;
            } else {
                puzzle[idx] = backup;   // 挖了会出多解，填回去
            }
        }

        page.solution = full;
        const next = [];
        for (let i = 0; i < 16; i++) {
            next.push({ "value": puzzle[i], "given": puzzle[i] !== 0 });
        }
        page.cells = next;
    }

    // ---- 操作 ----

    function tapCell(i) {
        if (page.finished) {
            return;
        }
        page.started = true;   // 第一下点棋盘才开始计时，站在页面发呆不算时间
        page.selected = i;
    }

    function nextEmptyAfter(i) {
        for (let k = 1; k <= 16; k++) {
            const j = (i + k) % 16;
            if (page.cells[j].value === 0) {
                return j;
            }
        }
        return i;
    }

    function put(v) {
        if (page.finished || page.selected < 0) {
            return;
        }
        if (page.cells[page.selected].given) {
            return;
        }

        page.started = true;
        const copy = page.cells.slice();
        copy[page.selected] = { "value": v, "given": false };
        page.cells = copy;

        // 填上数字后自动跳到下一个空格，少点几次
        if (v !== 0) {
            page.selected = page.nextEmptyAfter(page.selected);
        }
        page.checkDone();
    }

    function hint() {
        if (page.finished) {
            return;
        }
        let target = page.selected;
        if (target < 0 || page.cells[target].value !== 0) {
            target = page.nextEmptyAfter(Math.max(0, page.selected));
        }
        if (page.cells[target].value !== 0) {
            return;
        }

        page.started = true;
        const copy = page.cells.slice();
        copy[target] = { "value": page.solution[target], "given": true };
        page.cells = copy;
        page.hints++;
        page.selected = target;
        page.checkDone();
    }

    function checkDone() {
        for (let i = 0; i < 16; i++) {
            if (page.cells[i].value === 0) {
                return;
            }
        }
        // 填满了还要确认没有重复，否则「全填 1」也算赢
        if (Object.keys(page.findConflicts()).length > 0) {
            return;
        }
        page.finished = true;
    }

    function restart() {
        page.finished = false;
        page.started = false;
        page.selected = -1;
        page.seconds = 0;
        page.hints = 0;
        page.makePuzzle();
    }

    Component.onCompleted: page.makePuzzle()

    Timer {
        interval: 1000
        repeat: true
        running: !page.finished && page.started
        onTriggered: page.seconds++
    }

    Item {
        anchors.fill: page.contentArea

        RowLayout {
            anchors.centerIn: parent
            spacing: 40

            // ---- 棋盘 ----
            Grid {
                columns: 4
                rows: 4
                spacing: 7
                Layout.alignment: Qt.AlignVCenter

                Repeater {
                    model: 16

                    Rectangle {
                        required property int index

                        readonly property var cell: page.cells[index] !== undefined
                                                   ? page.cells[index]
                                                   : ({ "value": 0, "given": false })
                        readonly property bool bad: page.conflictSet[index] === true

                        width: 92
                        height: 92
                        radius: 18
                        color: bad ? "#FFE3E3" : (cell.given ? "#FBF3E8" : "#FFFFFF")
                        border.width: page.selected === index ? 3 : 1
                        border.color: page.selected === index ? "#3949AB" : "#EADBC8"

                        Text {
                            anchors.centerIn: parent
                            text: parent.cell.value === 0 ? "" : parent.cell.value
                            font.pixelSize: 46
                            font.weight: Font.Bold
                            color: parent.bad ? "#D32F2F"
                                              : (parent.cell.given ? "#4A3B32" : "#1565C0")
                        }

                        TapHandler {
                            onTapped: page.tapCell(index)
                        }
                    }
                }
            }

            // ---- 数字键盘 ----
            ColumnLayout {
                spacing: 14
                Layout.alignment: Qt.AlignVCenter

                Text {
                    text: page.selected >= 0 && page.cells[page.selected] !== undefined
                          && page.cells[page.selected].given
                          ? "这格是题目给的，换一个空格吧"
                          : "点一个空格，再点下面的数字"
                    font.pixelSize: 15
                    color: "#9B8574"
                }

                Grid {
                    columns: 2
                    spacing: 12

                    Repeater {
                        model: 4

                        Rectangle {
                            required property int index

                            readonly property int value: index + 1

                            width: 92
                            height: 92
                            radius: 22
                            color: padTap.pressed ? "#E3E8FA" : "#FFFFFF"
                            border.color: "#D7DDF2"
                            border.width: 1

                            scale: padTap.pressed ? 0.95 : 1.0
                            Behavior on scale {
                                NumberAnimation { duration: 90 }
                            }

                            Text {
                                anchors.centerIn: parent
                                text: parent.value
                                font.pixelSize: 44
                                font.weight: Font.Bold
                                color: "#3949AB"
                            }

                            TapHandler {
                                id: padTap
                                onTapped: page.put(index + 1)
                            }
                        }
                    }
                }

                RowLayout {
                    spacing: 12

                    Rectangle {
                        Layout.preferredWidth: 92
                        Layout.preferredHeight: 64
                        radius: 20
                        color: "#FFFFFF"
                        border.color: "#EADBC8"
                        border.width: 1

                        Text {
                            anchors.centerIn: parent
                            text: "擦除"
                            font.pixelSize: 19
                            font.weight: Font.DemiBold
                            color: "#7A6759"
                        }

                        TapHandler {
                            onTapped: page.put(0)
                        }
                    }

                    Rectangle {
                        Layout.preferredWidth: 92
                        Layout.preferredHeight: 64
                        radius: 20
                        color: "#FFF3E0"
                        border.color: "#FFD9A8"
                        border.width: 1

                        Text {
                            anchors.centerIn: parent
                            text: "提示"
                            font.pixelSize: 19
                            font.weight: Font.DemiBold
                            color: "#E65100"
                        }

                        TapHandler {
                            onTapped: page.hint()
                        }
                    }
                }

                Text {
                    text: "用时 " + page.timeText + (page.hints > 0 ? " · 提示 " + page.hints + " 次" : "")
                    font.pixelSize: 14
                    color: "#B8A492"
                }
            }
        }

        GameResultOverlay {
            anchors.fill: parent
            visible: page.finished
            summary: "全部填对！用了 " + page.timeText
                     + (page.hints > 0 ? "（提示 " + page.hints + " 次）" : "")
            onReplay: page.restart()
            onExit: page.navWin.popPage()
        }
    }
}
