import QtQuick

/*
 * 项目素材：推箱子（入门）。
 *
 * 这一款在教「数组」和「规则」：地图就是一排字符串，每个字符是一个格子；
 * 能不能走，取决于目标格子里是墙、箱子还是空地 —— 这就是最朴素的规则判断。
 *
 * 地图直接写在 levels 里，孩子改一个字符（把 # 改成空格）就能改关卡，
 * 这是「数据决定程序」最直观的一次体验。
 */
Item {
    id: game

    // 地图字符：# 墙  空格 地板  . 目标点  $ 箱子  @ 小朋友
    readonly property var levels: [
        [
            "#######",
            "#     #",
            "#  .  #",
            "#  $  #",
            "#  @  #",
            "#######"
        ],
        [
            "########",
            "#      #",
            "#  $   #",
            "#  .   #",
            "#   $  #",
            "#   .  #",
            "#  @   #",
            "########"
        ],
        [
            "#########",
            "#       #",
            "#  .$   #",
            "#   $   #",
            "#  .    #",
            "#  @    #",
            "#########"
        ]
    ]

    readonly property var levelNames: ["第 1 关 · 推一下", "第 2 关 · 两个箱子", "第 3 关 · 绕一圈"]

    // 这一款没有滑块参数，改的是地图本身
    property var knobs: []

    function setKnob(key, value) {
        // 故意留空：想要新参数时，在这里加一行就行
    }

    property string howTo: "用方向键（或下面的按钮）走，走到箱子后面就能把它推走。"
                         + "把所有箱子推到橙色圆点上就过关。推错了按「撤销」，或者按 Z。"
    property string teaches: "地图是一个「二维表」：第几行、第几列，就能定位一个格子。"
                           + "每次移动先看目标格是什么 —— 是墙就停下，是箱子就试着推，这就是规则。"
    property string codeSample: [
        "// 地图：一排字符串，一个字符一个格子",
        "const levels = [",
        "  [\"#######\",",
        "   \"#  .  #\",",
        "   \"#  $  #\",",
        "   \"#  @  #\",",
        "   \"#######\"]",
        "]",
        "",
        "function tryMove(dx, dy) {",
        "  const nx = player.x + dx",
        "  const ny = player.y + dy",
        "  if (wallAt(nx, ny)) return        // 墙：走不过去",
        "  const box = boxAt(nx, ny)",
        "  if (box >= 0) {                   // 前面是箱子：推它",
        "    if (wallAt(nx+dx, ny+dy)) return",
        "    if (boxAt(nx+dx, ny+dy) >= 0) return",
        "    boxes[box] = { x: nx+dx, y: ny+dy }",
        "  }",
        "  player = { x: nx, y: ny }        // 人走过去",
        "  checkWin()                       // 每个箱子都在点上？",
        "}"
    ].join("\n")

    property int levelIndex: 0
    property var grid: []
    property var boxes: []
    // 属性值里的对象字面量必须用括号包起来。
    // 不包的话 QML 会把 `{` 当成「一组属性赋值」来解析，直接报
    // `Expected token ','`，整个项目加载失败 —— 界面上就是右侧一片空白。
    property var player: ({ x: 1, y: 1 })
    property var history: []
    property int moves: 0
    property int pushes: 0
    property bool won: false

    readonly property int cols: grid.length > 0 ? grid[0].length : 0
    readonly property int rows: grid.length

    Component.onCompleted: {
        game.loadLevel(0)
        game.forceActiveFocus()
    }

    function loadLevel(index) {
        const i = Math.max(0, Math.min(levels.length - 1, index))
        const map = []
        const list = []
        let start = { x: 1, y: 1 }
        for (let y = 0; y < levels[i].length; ++y) {
            const row = levels[i][y]
            const cells = []
            for (let x = 0; x < row.length; ++x) {
                const ch = row.charAt(x)
                if (ch === "$") {
                    list.push({ x: x, y: y })
                    cells.push(" ")
                } else if (ch === "@") {
                    start = { x: x, y: y }
                    cells.push(" ")
                } else {
                    cells.push(ch)
                }
            }
            map.push(cells)
        }
        game.grid = map
        game.boxes = list
        game.player = start
        game.history = []
        game.moves = 0
        game.pushes = 0
        game.won = false
        game.levelIndex = i
        canvas.requestPaint()
    }

    function restart() {
        game.loadLevel(game.levelIndex)
    }

    function nextLevel() {
        if (game.levelIndex + 1 < game.levels.length) {
            game.loadLevel(game.levelIndex + 1)
        } else {
            game.loadLevel(0)
        }
    }

    function wallAt(x, y) {
        if (x < 0 || y < 0 || y >= game.grid.length) {
            return true
        }
        const row = game.grid[y]
        if (x >= row.length) {
            return true
        }
        return row[x] === "#"
    }

    function goalAt(x, y) {
        if (y < 0 || y >= game.grid.length) {
            return false
        }
        const row = game.grid[y]
        return x >= 0 && x < row.length && row[x] === "."
    }

    function boxAt(x, y) {
        for (let i = 0; i < game.boxes.length; ++i) {
            if (game.boxes[i].x === x && game.boxes[i].y === y) {
                return i
            }
        }
        return -1
    }

    function snapshot() {
        const copy = []
        for (let i = 0; i < game.boxes.length; ++i) {
            copy.push({ x: game.boxes[i].x, y: game.boxes[i].y })
        }
        game.history.push({
            player: { x: game.player.x, y: game.player.y },
            boxes: copy,
            moves: game.moves,
            pushes: game.pushes
        })
        if (game.history.length > 200) {
            game.history.shift()
        }
    }

    function tryMove(dx, dy) {
        if (game.won) {
            return
        }
        const nx = game.player.x + dx
        const ny = game.player.y + dy
        if (game.wallAt(nx, ny)) {
            return
        }
        const box = game.boxAt(nx, ny)
        if (box >= 0) {
            const bx = nx + dx
            const by = ny + dy
            if (game.wallAt(bx, by) || game.boxAt(bx, by) >= 0) {
                    return
            }
            game.snapshot()
            const list = []
            for (let i = 0; i < game.boxes.length; ++i) {
                list.push(i === box ? { x: bx, y: by } : game.boxes[i])
            }
            game.boxes = list
            game.pushes += 1
        } else {
            game.snapshot()
        }

        game.player = { x: nx, y: ny }
        game.moves += 1
        checkWin()
        canvas.requestPaint()
    }

    function undo() {
        if (game.history.length === 0 || game.won) {
            return
        }
        const last = game.history.pop()
        game.player = last.player
        game.boxes = last.boxes
        game.moves = last.moves
        game.pushes = last.pushes
        canvas.requestPaint()
    }

    function checkWin() {
        for (let i = 0; i < game.boxes.length; ++i) {
            const b = game.boxes[i]
            if (!game.goalAt(b.x, b.y)) {
                return
            }
        }
        game.won = true
    }

    // ---- 画面 ----
    Rectangle {
        id: frame

        anchors.fill: parent
        anchors.bottomMargin: 56
        radius: 16
        color: "#F6EBD8"
        clip: true

        Canvas {
            id: canvas

            anchors.fill: parent

            readonly property real tile: Math.max(18, Math.floor(Math.min(
                width / Math.max(1, game.cols), height / Math.max(1, game.rows))))
            readonly property real ox: (width - game.cols * tile) / 2
            readonly property real oy: (height - game.rows * tile) / 2

            onWidthChanged: requestPaint()
            onHeightChanged: requestPaint()

            onPaint: {
                const ctx = getContext("2d")
                ctx.setTransform(1, 0, 0, 1, 0, 0)
                ctx.clearRect(0, 0, width, height)

                const t = canvas.tile
                const ox = canvas.ox
                const oy = canvas.oy
                if (game.cols === 0) {
                    return
                }

                // 地板
                ctx.fillStyle = "#FBF4E6"
                ctx.fillRect(ox, oy, game.cols * t, game.rows * t)

                for (let y = 0; y < game.rows; ++y) {
                    for (let x = 0; x < game.cols; ++x) {
                        const px = ox + x * t
                        const py = oy + y * t
                        const cell = game.grid[y][x]

                        if (cell === "#") {
                            ctx.fillStyle = "#7B5E4B"
                            ctx.fillRect(px, py, t, t)
                            ctx.fillStyle = "#8D6E63"
                            ctx.fillRect(px + 2, py + 2, t - 4, t - 8)
                            continue
                        }

                        // 地板的小砖缝，让格子看得出来
                        ctx.strokeStyle = "#EFE3CE"
                        ctx.lineWidth = 1
                        ctx.strokeRect(px + 0.5, py + 0.5, t - 1, t - 1)

                        if (cell === ".") {
                            ctx.fillStyle = "#FFB74D"
                            ctx.beginPath()
                            ctx.arc(px + t / 2, py + t / 2, t * 0.16, 0, Math.PI * 2)
                            ctx.fill()
                        }
                    }
                }

                // 箱子
                for (let i = 0; i < game.boxes.length; ++i) {
                    const b = game.boxes[i]
                    const px = ox + b.x * t
                    const py = oy + b.y * t
                    const onGoal = game.goalAt(b.x, b.y)
                    ctx.fillStyle = onGoal ? "#66BB6A" : "#C98A4B"
                    ctx.fillRect(px + 3, py + 3, t - 6, t - 6)
                    ctx.strokeStyle = onGoal ? "#2E7D32" : "#8D5524"
                    ctx.lineWidth = 3
                    ctx.strokeRect(px + 4.5, py + 4.5, t - 9, t - 9)
                    ctx.beginPath()
                    ctx.moveTo(px + 6, py + 6)
                    ctx.lineTo(px + t - 6, py + t - 6)
                    ctx.moveTo(px + t - 6, py + 6)
                    ctx.lineTo(px + 6, py + t - 6)
                    ctx.stroke()
                }

                // 小朋友
                const cx = ox + game.player.x * t + t / 2
                const cy = oy + game.player.y * t + t / 2
                ctx.fillStyle = "#42A5F5"
                ctx.beginPath()
                ctx.arc(cx, cy, t * 0.32, 0, Math.PI * 2)
                ctx.fill()
                // 两只眼睛分开画：同一个路径里连着两个 arc，中间会被
                // 直线连起来填出一条白杠（眼睛糊成一片）。
                ctx.fillStyle = "#FFFFFF"
                ctx.beginPath()
                ctx.arc(cx - t * 0.1, cy - t * 0.06, t * 0.07, 0, Math.PI * 2)
                ctx.fill()
                ctx.beginPath()
                ctx.arc(cx + t * 0.1, cy - t * 0.06, t * 0.07, 0, Math.PI * 2)
                ctx.fill()
            }
        }

        Text {
            anchors.left: parent.left
            anchors.top: parent.top
            anchors.margins: 12
            text: game.levelNames[game.levelIndex]
            font.pixelSize: 14
            font.bold: true
            color: "#6D4C41"
        }

        Text {
            anchors.right: parent.right
            anchors.top: parent.top
            anchors.margins: 12
            text: "步数 " + game.moves + " · 推动 " + game.pushes
            font.pixelSize: 13
            color: "#8D6E63"
        }

        MouseArea {
            anchors.fill: parent
            onClicked: game.forceActiveFocus()
        }

        Rectangle {
            anchors.fill: parent
            visible: game.won
            z: 5
            radius: 16
            color: "#B3FFFFFF"

            Column {
                anchors.centerIn: parent
                spacing: 14

                Text {
                    anchors.horizontalCenter: parent.horizontalCenter
                    text: "过关！用了 " + (game.moves) + " 步"
                    font.pixelSize: 24
                    font.bold: true
                    color: "#33691E"
                }

                Row {
                    anchors.horizontalCenter: parent.horizontalCenter
                    spacing: 12

                    Rectangle {
                        width: 132
                        height: 46
                        radius: 23
                        color: nextArea.pressed ? "#2E7D32" : "#43A047"

                        Text {
                            anchors.centerIn: parent
                            text: game.levelIndex + 1 < game.levels.length ? "下一关" : "再来一遍"
                            font.pixelSize: 15
                            font.bold: true
                            color: "#FFFFFF"
                        }

                        MouseArea {
                            id: nextArea
                            anchors.fill: parent
                            onClicked: game.nextLevel()
                        }
                    }

                    Rectangle {
                        width: 110
                        height: 46
                        radius: 23
                        color: "#FFFFFF"

                        Text {
                            anchors.centerIn: parent
                            text: "重来"
                            font.pixelSize: 15
                            font.bold: true
                            color: "#6D4C41"
                        }

                        MouseArea {
                            anchors.fill: parent
                            onClicked: game.restart()
                        }
                    }
                }
            }
        }
    }

    // 方向键：鼠标也能玩，不依赖键盘焦点
    Row {
        anchors.horizontalCenter: parent.horizontalCenter
        anchors.bottom: parent.bottom
        anchors.bottomMargin: 4
        spacing: 8

        Repeater {
            model: [
                { label: "←", dx: -1, dy: 0 },
                { label: "↑", dx: 0, dy: -1 },
                { label: "↓", dx: 0, dy: 1 },
                { label: "→", dx: 1, dy: 0 }
            ]

            Rectangle {
                required property var modelData

                width: 52
                height: 44
                radius: 12
                color: area.pressed ? "#E0CFB6" : "#FFFFFF"
                border.width: 1
                border.color: "#E6D8C3"

                Text {
                    anchors.centerIn: parent
                    text: modelData.label
                    font.pixelSize: 18
                    font.bold: true
                    color: "#6D4C41"
                }

                MouseArea {
                    id: area
                    anchors.fill: parent
                    onClicked: game.tryMove(modelData.dx, modelData.dy)
                }
            }
        }

        Rectangle {
            width: 72
            height: 44
            radius: 12
            color: undoArea.pressed ? "#EFE3D6" : "#F7F1E9"

            Text {
                anchors.centerIn: parent
                text: "撤销"
                font.pixelSize: 14
                font.bold: true
                color: "#8D6E63"
            }

            MouseArea {
                id: undoArea
                anchors.fill: parent
                onClicked: game.undo()
            }
        }
    }

    focus: true

    Keys.onPressed: function (event) {
        if (event.key === Qt.Key_Left || event.key === Qt.Key_A) {
            game.tryMove(-1, 0)
            event.accepted = true
        } else if (event.key === Qt.Key_Right || event.key === Qt.Key_D) {
            game.tryMove(1, 0)
            event.accepted = true
        } else if (event.key === Qt.Key_Up || event.key === Qt.Key_W) {
            game.tryMove(0, -1)
            event.accepted = true
        } else if (event.key === Qt.Key_Down || event.key === Qt.Key_S) {
            game.tryMove(0, 1)
            event.accepted = true
        } else if (event.key === Qt.Key_Z || event.key === Qt.Key_Backspace) {
            game.undo()
            event.accepted = true
        } else if (event.key === Qt.Key_R) {
            game.restart()
            event.accepted = true
        }
    }
}
