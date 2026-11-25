import QtQuick

/*
 * 项目素材：横板冒险（进阶）。
 *
 * 比前两款多出来的东西：重力、跳跃、摄像机跟随、和地形一块一块地做碰撞。
 * 它不靠「格子」，而是靠每帧算速度 —— 这正是多数动作游戏的底层写法。
 *
 * 三个可改参数（重力 / 跳跃力 / 速度）是能明显改变手感的，孩子调完立刻能感到
 * 「同一个程序，数字不同，玩起来完全是两个游戏」。
 */
Item {
    id: game

    readonly property int W: 720          // 视野宽（逻辑像素）
    readonly property int H: 420          // 世界高
    readonly property int worldW: 2480    // 世界总宽

    property real gravity: 1400
    property real jumpPower: 560
    property real runSpeed: 240

    property var knobs: [
        { key: "gravity", label: "重力", min: 700, max: 2200, step: 50, value: 1400 },
        { key: "jumpPower", label: "跳跃力", min: 380, max: 780, step: 20, value: 560 },
        { key: "runSpeed", label: "跑动速度", min: 150, max: 380, step: 10, value: 240 }
    ]

    function setKnob(key, value) {
        if (key === "gravity") {
            game.gravity = value
        } else if (key === "jumpPower") {
            game.jumpPower = value
        } else if (key === "runSpeed") {
            game.runSpeed = value
        }
    }

    property string howTo: "← → 跑动，↑ 或空格跳。跳过缺口，把金色小圆币都捡了，"
                         + "跑到最右边的旗子就通关。掉下去会回到刚才站过的地面，不会从头开始。"
    property string teaches: "重力就是每帧给竖直速度加一点；跳是「一瞬间给一个向上的速度」；"
                           + "碰撞是判断两个矩形有没有叠在一起；摄像机跟着人走，画面才像在前进。"
    property string codeSample: [
        "function step(dt) {",
        "  // 1. 水平：按键决定速度，速度乘时间就是位移",
        "  if (keyLeft)  player.vx = -runSpeed",
        "  else if (keyRight) player.vx = runSpeed",
        "  else player.vx = 0",
        "  player.x += player.vx * dt",
        "",
        "  // 2. 重力：竖直速度每帧都变大一点",
        "  player.vy += gravity * dt",
        "  player.y += player.vy * dt",
        "",
        "  // 3. 落地：脚碰到平台顶面就停住",
        "  for (let i = 0; i < platforms.length; ++i) {",
        "    if (overlap(player, platforms[i]) && player.vy > 0) {",
        "      player.y = platforms[i].y - player.h",
        "      player.vy = 0",
        "      onGround = true",
        "    }",
        "  }",
        "",
        "  // 4. 摄像机：让人一直在屏幕偏左的位置",
        "  camX = clamp(player.x - 300, 0, worldW - W)",
        "}"
    ].join("\n")

    readonly property var platforms: [
        { x: 0, y: 380, w: 520, h: 40 },
        { x: 600, y: 380, w: 380, h: 40 },
        { x: 1060, y: 380, w: 620, h: 40 },
        { x: 1760, y: 380, w: 720, h: 40 },
        { x: 300, y: 300, w: 120, h: 18 },
        { x: 700, y: 290, w: 110, h: 18 },
        { x: 900, y: 230, w: 100, h: 18 },
        { x: 1200, y: 300, w: 140, h: 18 },
        { x: 1450, y: 240, w: 120, h: 18 },
        { x: 1650, y: 320, w: 90, h: 18 }
    ]

    readonly property real goalX: 2320
    readonly property real startX: 60
    readonly property real startY: 300

    property var coins: []
    property var player: ({ x: 60, y: 300, vx: 0, vy: 0, w: 26, h: 32, onGround: false })
    property real camX: 0
    property var safe: ({ x: 60, y: 300 })
    property int got: 0
    property int falls: 0
    property bool won: false
    property bool keyLeft: false
    property bool keyRight: false
    property bool keyJump: false
    property real lastTick: 0
    property real clock: 0

    Component.onCompleted: {
        game.restart()
        game.forceActiveFocus()
    }

    function buildCoins() {
        const spots = [
            [260, 356], [380, 356], [340, 276],
            [740, 266], [940, 206],
            [1150, 356], [1250, 276], [1500, 216],
            [1690, 296], [1900, 356], [2060, 356]
        ]
        const list = []
        for (let i = 0; i < spots.length; ++i) {
            list.push({ x: spots[i][0], y: spots[i][1], taken: false })
        }
        return list
    }

    function restart() {
        game.coins = game.buildCoins()
        game.player = { x: game.startX, y: game.startY, vx: 0, vy: 0,
                        w: 26, h: 32, onGround: false }
        game.camX = 0
        game.safe = { x: game.startX, y: game.startY }
        game.got = 0
        game.falls = 0
        game.won = false
        game.clock = 0
        canvas.requestPaint()
    }

    function overlap(px, py, pw, ph, r) {
        return px < r.x + r.w && px + pw > r.x && py < r.y + r.h && py + ph > r.y
    }

    function step(dt) {
        if (game.won) {
            return
        }
        game.clock += dt
        const p = game.player

        if (game.keyLeft) {
            p.vx = -game.runSpeed
        } else if (game.keyRight) {
            p.vx = game.runSpeed
        } else {
            p.vx = 0
        }

        if (game.keyJump && p.onGround) {
            p.vy = -game.jumpPower
            p.onGround = false
            // 按住不放不会连跳：起跳就清掉标记，要松手再按才有第二下
            game.keyJump = false
        }

        // 水平移动 + 撞墙修正
        p.x += p.vx * dt
        for (let i = 0; i < game.platforms.length; ++i) {
            const r = game.platforms[i]
            if (!game.overlap(p.x, p.y, p.w, p.h, r)) {
                continue
            }
            if (p.vx > 0) {
                p.x = r.x - p.w
            } else if (p.vx < 0) {
                p.x = r.x + r.w
            }
        }
        p.x = Math.max(0, Math.min(game.worldW - p.w, p.x))

        // 竖直移动 + 落地 / 撞头
        p.vy += game.gravity * dt
        p.y += p.vy * dt
        p.onGround = false
        for (let i = 0; i < game.platforms.length; ++i) {
            const r = game.platforms[i]
            if (!game.overlap(p.x, p.y, p.w, p.h, r)) {
                continue
            }
            if (p.vy > 0) {
                p.y = r.y - p.h
                p.vy = 0
                p.onGround = true
            } else if (p.vy < 0) {
                p.y = r.y + r.h
                p.vy = 0
            }
        }

        if (p.onGround) {
            game.safe = { x: p.x, y: p.y }
        }

        // 掉进坑里：回到最后站过的地面，不清空金币
        if (p.y > game.H + 80) {
            p.x = game.safe.x
            p.y = game.safe.y - 40
            p.vx = 0
            p.vy = 0
            game.falls += 1
        }

        // 捡金币
        for (let i = 0; i < game.coins.length; ++i) {
            const c = game.coins[i]
            if (!c.taken && Math.abs(c.x - (p.x + p.w / 2)) < 20
                    && Math.abs(c.y - (p.y + p.h / 2)) < 24) {
                c.taken = true
                game.got += 1
            }
        }

        if (p.x + p.w >= game.goalX) {
            game.won = true
        }

        game.camX = Math.max(0, Math.min(game.worldW - game.W, p.x - game.W * 0.42))
    }

    // ---- 画面 ----
    Rectangle {
        id: frame

        anchors.fill: parent
        anchors.bottomMargin: 56
        radius: 16
        color: "#8ECFF0"
        clip: true

        Canvas {
            id: canvas

            anchors.fill: parent

            readonly property real s: Math.min(width / game.W, height / game.H)
            readonly property real ox: (width - game.W * s) / 2
            readonly property real oy: (height - game.H * s) / 2

            onWidthChanged: requestPaint()
            onHeightChanged: requestPaint()

            onPaint: {
                const ctx = getContext("2d")
                ctx.setTransform(1, 0, 0, 1, 0, 0)
                ctx.clearRect(0, 0, width, height)
                ctx.translate(ox, oy)
                ctx.scale(s, s)

                // 天空
                const sky = ctx.createLinearGradient(0, 0, 0, game.H)
                sky.addColorStop(0, "#7FC8F0")
                sky.addColorStop(1, "#DFF3FF")
                ctx.fillStyle = sky
                ctx.fillRect(0, 0, game.W, game.H)

                // 远山：随摄像机慢速移动，看起来就有了远近
                const far = (game.camX * 0.25) % 480
                ctx.fillStyle = "#A8D8B9"
                for (let i = -1; i < 3; ++i) {
                    const bx = i * 480 - far
                    ctx.beginPath()
                    ctx.moveTo(bx, game.H)
                    ctx.lineTo(bx + 140, 190)
                    ctx.lineTo(bx + 280, game.H)
                    ctx.closePath()
                    ctx.fill()
                    ctx.beginPath()
                    ctx.moveTo(bx + 200, game.H)
                    ctx.lineTo(bx + 330, 230)
                    ctx.lineTo(bx + 460, game.H)
                    ctx.closePath()
                    ctx.fill()
                }

                // 世界坐标：往左推 camX，人就一直在画面里
                ctx.save()
                ctx.translate(-game.camX, 0)

                // 云
                ctx.fillStyle = "#FFFFFF"
                for (let i = 0; i < 6; ++i) {
                    const cx = i * 420 + 120
                    const cy = 60 + (i % 3) * 34
                    ctx.beginPath()
                    ctx.arc(cx, cy, 22, 0, Math.PI * 2)
                    ctx.arc(cx + 26, cy + 6, 17, 0, Math.PI * 2)
                    ctx.arc(cx - 24, cy + 8, 15, 0, Math.PI * 2)
                    ctx.fill()
                }

                // 平台：上面一层草，下面一块土
                for (let i = 0; i < game.platforms.length; ++i) {
                    const r = game.platforms[i]
                    ctx.fillStyle = "#8D6E63"
                    ctx.fillRect(r.x, r.y, r.w, r.h)
                    ctx.fillStyle = "#66BB6A"
                    ctx.fillRect(r.x, r.y, r.w, Math.min(8, r.h))
                }

                // 金币
                for (let i = 0; i < game.coins.length; ++i) {
                    const c = game.coins[i]
                    if (c.taken) {
                        continue
                    }
                    ctx.fillStyle = "#FFC107"
                    ctx.beginPath()
                    ctx.arc(c.x, c.y, 9, 0, Math.PI * 2)
                    ctx.fill()
                    ctx.fillStyle = "#FFE082"
                    ctx.beginPath()
                    ctx.arc(c.x - 2, c.y - 2, 3.5, 0, Math.PI * 2)
                    ctx.fill()
                }

                // 终点旗
                const fx = game.goalX
                ctx.fillStyle = "#8D6E63"
                ctx.fillRect(fx, 300, 6, 80)
                ctx.fillStyle = "#E53935"
                ctx.beginPath()
                ctx.moveTo(fx + 6, 302)
                ctx.lineTo(fx + 58, 318)
                ctx.lineTo(fx + 6, 334)
                ctx.closePath()
                ctx.fill()

                // 小朋友
                const p = game.player
                ctx.fillStyle = "#1E88E5"
                ctx.fillRect(p.x + 3, p.y + 10, p.w - 6, p.h - 10)
                ctx.fillStyle = "#FFCC80"
                ctx.beginPath()
                ctx.arc(p.x + p.w / 2, p.y + 9, 9, 0, Math.PI * 2)
                ctx.fill()
                ctx.fillStyle = "#3E2723"
                ctx.fillRect(p.x + 8, p.y + 6, 3, 3)
                ctx.fillRect(p.x + 15, p.y + 6, 3, 3)

                ctx.restore()
            }
        }

        Text {
            anchors.left: parent.left
            anchors.top: parent.top
            anchors.margins: 14
            text: "金币 " + game.got + " / " + game.coins.length
            font.pixelSize: 15
            font.bold: true
            color: "#1B5E20"
        }

        Text {
            anchors.right: parent.right
            anchors.top: parent.top
            anchors.margins: 14
            visible: game.falls > 0
            text: "掉下去 " + game.falls + " 次"
            font.pixelSize: 13
            color: "#4E6E55"
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
                    text: "到终点啦！金币 " + game.got + " / " + game.coins.length
                    font.pixelSize: 22
                    font.bold: true
                    color: "#1B5E20"
                }

                Rectangle {
                    anchors.horizontalCenter: parent.horizontalCenter
                    width: 150
                    height: 48
                    radius: 24
                    color: again.pressed ? "#2E7D32" : "#43A047"

                    Text {
                        anchors.centerIn: parent
                        text: "再玩一次"
                        font.pixelSize: 16
                        font.bold: true
                        color: "#FFFFFF"
                    }

                    MouseArea {
                        id: again
                        anchors.fill: parent
                        onClicked: game.restart()
                    }
                }
            }
        }

        MouseArea {
            anchors.fill: parent
            onClicked: game.forceActiveFocus()
        }
    }

    // 触屏 / 鼠标也能玩
    Row {
        anchors.horizontalCenter: parent.horizontalCenter
        anchors.bottom: parent.bottom
        anchors.bottomMargin: 4
        spacing: 10

        Rectangle {
            width: 62
            height: 44
            radius: 12
            color: leftArea.pressed ? "#D6E4EC" : "#FFFFFF"
            border.width: 1
            border.color: "#DDE7EE"

            Text {
                anchors.centerIn: parent
                text: "←"
                font.pixelSize: 20
                font.bold: true
                color: "#37474F"
            }

            MouseArea {
                id: leftArea
                anchors.fill: parent
                onPressed: game.keyLeft = true
                onReleased: game.keyLeft = false
                onCanceled: game.keyLeft = false
            }
        }

        Rectangle {
            width: 74
            height: 44
            radius: 12
            color: jumpArea.pressed ? "#FFE0B2" : "#FFFFFF"
            border.width: 1
            border.color: "#F0DCC4"

            Text {
                anchors.centerIn: parent
                text: "跳"
                font.pixelSize: 16
                font.bold: true
                color: "#E65100"
            }

            MouseArea {
                id: jumpArea
                anchors.fill: parent
                onPressed: game.keyJump = true
                onReleased: game.keyJump = false
                onCanceled: game.keyJump = false
            }
        }

        Rectangle {
            width: 62
            height: 44
            radius: 12
            color: rightArea.pressed ? "#D6E4EC" : "#FFFFFF"
            border.width: 1
            border.color: "#DDE7EE"

            Text {
                anchors.centerIn: parent
                text: "→"
                font.pixelSize: 20
                font.bold: true
                color: "#37474F"
            }

            MouseArea {
                id: rightArea
                anchors.fill: parent
                onPressed: game.keyRight = true
                onReleased: game.keyRight = false
                onCanceled: game.keyRight = false
            }
        }
    }

    focus: true

    Keys.onPressed: function (event) {
        if (event.key === Qt.Key_Left || event.key === Qt.Key_A) {
            game.keyLeft = true
            event.accepted = true
        } else if (event.key === Qt.Key_Right || event.key === Qt.Key_D) {
            game.keyRight = true
            event.accepted = true
        } else if (event.key === Qt.Key_Up || event.key === Qt.Key_W
                   || event.key === Qt.Key_Space) {
            game.keyJump = true
            event.accepted = true
        }
    }

    Keys.onReleased: function (event) {
        if (event.key === Qt.Key_Left || event.key === Qt.Key_A) {
            game.keyLeft = false
            event.accepted = true
        } else if (event.key === Qt.Key_Right || event.key === Qt.Key_D) {
            game.keyRight = false
            event.accepted = true
        } else if (event.key === Qt.Key_Up || event.key === Qt.Key_W
                   || event.key === Qt.Key_Space) {
            game.keyJump = false
            event.accepted = true
        }
    }

    Timer {
        interval: 16
        running: true
        repeat: true
        onTriggered: {
            const now = Date.now()
            if (game.lastTick === 0) {
                game.lastTick = now
            }
            const dt = Math.min(0.05, (now - game.lastTick) / 1000)
            game.lastTick = now
            game.step(dt)
            canvas.requestPaint()
        }
    }
}
