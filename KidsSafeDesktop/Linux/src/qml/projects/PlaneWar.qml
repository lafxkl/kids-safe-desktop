import QtQuick

/*
 * 项目素材：飞机大战（入门）。
 *
 * 逻辑分辨率固定 720×420，画的时候按等比缩放铺到实际尺寸上 —— 这样代码里
 * 所有坐标都是「一个数」，孩子改数字就能看懂效果，不用管窗口多大。
 *
 * 刻意保留的三处「可改」：敌机速度、出机间隔、子弹速度。改一个数，画面立刻
 * 不一样，这是最好的「参数 → 行为」教学。
 */
Item {
    id: game

    readonly property int W: 720
    readonly property int H: 420

    property real enemySpeed: 90
    property real spawnEvery: 1.1
    property real bulletSpeed: 520

    property var knobs: [
        { key: "enemySpeed", label: "敌机速度", min: 50, max: 220, step: 10, value: 90 },
        { key: "spawnEvery", label: "出机间隔(秒)", min: 0.4, max: 2.0, step: 0.1, value: 1.1 },
        { key: "bulletSpeed", label: "子弹速度", min: 260, max: 900, step: 20, value: 520 }
    ]

    function setKnob(key, value) {
        if (key === "enemySpeed") {
            game.enemySpeed = value
        } else if (key === "spawnEvery") {
            game.spawnEvery = Math.max(0.3, value)
        } else if (key === "bulletSpeed") {
            game.bulletSpeed = value
        }
    }

    property string howTo: "按 ← → 或 A / D 左右飞，也可以直接用鼠标拖住飞机。"
                         + "子弹自己会打出去，打中红色敌机得 1 分；被撞一次掉一颗心，"
                         + "三颗心用完就结束。"
    property string teaches: "每一帧都把子弹的 y 减一点，它看起来就在往上飞；"
                           + "两个矩形有没有叠在一起，就是「打中」的判断；"
                           + "「一直重复」让这件事每秒发生 60 次。"
    property string codeSample: [
        "// 每 1/60 秒跑一次，这就是游戏的「心跳」",
        "function step(dt) {",
        "    // 1. 让飞机跟着按键动（dt 是这一帧过了多少秒）",
        "    if (keyLeft)  planeX -= 320 * dt",
        "    if (keyRight) planeX += 320 * dt",
        "",
        "    // 2. 每 0.28 秒造一颗子弹，从机头飞出去",
        "    if (fireClock >= 0.28) {",
        "        bullets.push({ x: planeX, y: planeY - 20 })",
        "    }",
        "",
        "    // 3. 所有子弹一起往上飞",
        "    for (let i = 0; i < bullets.length; ++i)",
        "        bullets[i].y -= bulletSpeed * dt",
        "",
        "    // 4. 碰上了吗？两个矩形重叠就是打中",
        "    if (Math.abs(b.x - e.x) < 16 &&",
        "        Math.abs(b.y - e.y) < 18) {",
        "        score += 1",
        "    }",
        "}"
    ].join("\n")

    // ---- 运行时状态 ----
    property real planeX: W / 2
    readonly property real planeY: H - 54
    property var bullets: []
    property var enemies: []
    property int score: 0
    property int lives: 3
    property bool over: false
    property bool keyLeft: false
    property bool keyRight: false
    property real dragX: -1
    property real clock: 0
    property real spawnClock: 0
    property real fireClock: 0
    property real lastTick: 0
    property real flash: 0

    function restart() {
        game.bullets = []
        game.enemies = []
        game.score = 0
        game.lives = 3
        game.over = false
        game.planeX = game.W / 2
        game.clock = 0
        game.spawnClock = 0
        game.fireClock = 0
        game.flash = 0
        game.dragX = -1
        canvas.requestPaint()
    }

    function spawnEnemy() {
        game.enemies.push({
            x: 30 + Math.random() * (game.W - 60),
            y: -24,
            vy: game.enemySpeed * (0.8 + Math.random() * 0.6)
        })
    }

    function step(dt) {
        if (game.over) {
            return
        }
        game.clock += dt

        if (game.keyLeft) {
            game.planeX -= 320 * dt
        }
        if (game.keyRight) {
            game.planeX += 320 * dt
        }
        if (game.dragX >= 0) {
            game.planeX += (game.dragX - game.planeX) * Math.min(1, dt * 14)
        }
        game.planeX = Math.max(26, Math.min(game.W - 26, game.planeX))

        game.fireClock += dt
        if (game.fireClock >= 0.28) {
            game.fireClock = 0
            game.bullets.push({ x: game.planeX, y: game.planeY - 20 })
        }

        game.spawnClock += dt
        if (game.spawnClock >= game.spawnEvery) {
            game.spawnClock = 0
            game.spawnEnemy()
        }

        const aliveBullets = []
        for (let i = 0; i < game.bullets.length; ++i) {
            const b = game.bullets[i]
            b.y -= game.bulletSpeed * dt
            if (b.y > -16) {
                aliveBullets.push(b)
            }
        }
        game.bullets = aliveBullets

        const aliveEnemies = []
        for (let i = 0; i < game.enemies.length; ++i) {
            const e = game.enemies[i]
            e.y += e.vy * dt
            // 左右轻轻摇摆：sin 是一直上下摆的曲线，乘个小数字就是晃
            e.x += Math.sin((game.clock + i) * 2.2) * 14 * dt
            if (e.y < game.H + 34) {
                aliveEnemies.push(e)
            }
        }
        game.enemies = aliveEnemies

        // 子弹打敌机：先处理命中，剩下的敌机才继续存在
        const survived = []
        for (let i = 0; i < game.enemies.length; ++i) {
            const e = game.enemies[i]
            let hit = false
            for (let j = 0; j < game.bullets.length; ++j) {
                const b = game.bullets[j]
                if (Math.abs(b.x - e.x) < 16 && Math.abs(b.y - e.y) < 18) {
                    game.bullets.splice(j, 1)
                    hit = true
                    break
                }
            }
            if (hit) {
                game.score += 1
            } else {
                survived.push(e)
            }
        }
        game.enemies = survived

        // 敌机撞飞机
        for (let i = 0; i < game.enemies.length; ++i) {
            const e = game.enemies[i]
            if (Math.abs(e.x - game.planeX) < 24 && Math.abs(e.y - game.planeY) < 22) {
                game.enemies.splice(i, 1)
                game.lives -= 1
                game.flash = 1
                if (game.lives <= 0) {
                    game.lives = 0
                    game.over = true
                }
                break
            }
        }

        if (game.flash > 0) {
            game.flash = Math.max(0, game.flash - dt * 2.2)
        }
    }

    // ---- 画面 ----
    Rectangle {
        id: frame

        anchors.fill: parent
        radius: 16
        color: "#070B16"
        clip: true

        Canvas {
            id: canvas

            anchors.fill: parent

            // 等比缩放：留黑边，不把飞机拉扁
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

                // 夜空
                const sky = ctx.createLinearGradient(0, 0, 0, game.H)
                sky.addColorStop(0, "#12203F")
                sky.addColorStop(1, "#05070F")
                ctx.fillStyle = sky
                ctx.fillRect(0, 0, game.W, game.H)

                // 星星：位置是算出来的，不存数组 —— 它们只是背景
                for (let i = 0; i < 70; ++i) {
                    const sx = (i * 97) % game.W
                    const sy = ((i * 53) + game.clock * (12 + (i % 5) * 9)) % game.H
                    ctx.fillStyle = i % 3 === 0 ? "#4A5C8C" : "#212A47"
                    ctx.fillRect(sx, sy, 2, 2)
                }

                // 子弹
                ctx.fillStyle = "#FFE082"
                for (let i = 0; i < game.bullets.length; ++i) {
                    const b = game.bullets[i]
                    ctx.fillRect(b.x - 2, b.y - 9, 4, 14)
                }

                // 敌机：倒三角
                for (let i = 0; i < game.enemies.length; ++i) {
                    const e = game.enemies[i]
                    ctx.fillStyle = "#EF5350"
                    ctx.beginPath()
                    ctx.moveTo(e.x, e.y + 15)
                    ctx.lineTo(e.x + 15, e.y - 12)
                    ctx.lineTo(e.x - 15, e.y - 12)
                    ctx.closePath()
                    ctx.fill()
                    ctx.fillStyle = "#FFCDD2"
                    ctx.fillRect(e.x - 4, e.y - 6, 8, 5)
                }

                // 我方飞机
                ctx.save()
                ctx.translate(game.planeX, game.planeY)
                ctx.fillStyle = game.flash > 0 ? "#FFFFFF" : "#4FC3F7"
                ctx.beginPath()
                ctx.moveTo(0, -20)
                ctx.lineTo(16, 14)
                ctx.lineTo(0, 6)
                ctx.lineTo(-16, 14)
                ctx.closePath()
                ctx.fill()
                ctx.fillStyle = "#E1F5FE"
                ctx.beginPath()
                ctx.arc(0, -2, 5, 0, Math.PI * 2)
                ctx.fill()
                ctx.restore()
            }
        }

        // HUD 用 QML 文本画：中文字体渲染比自己算 Canvas 字体稳
        Text {
            anchors.left: parent.left
            anchors.top: parent.top
            anchors.margins: 14
            text: "得分 " + game.score
            font.pixelSize: 16
            font.bold: true
            color: "#FFFFFF"
        }

        Row {
            anchors.right: parent.right
            anchors.top: parent.top
            anchors.margins: 14
            spacing: 4

            Repeater {
                model: 3

                Text {
                    text: index < game.lives ? "♥" : "♡"
                    font.pixelSize: 18
                    color: index < game.lives ? "#EF5350" : "#5C6B8A"
                }
            }
        }

        Text {
            anchors.left: parent.left
            anchors.bottom: parent.bottom
            anchors.margins: 14
            visible: !game.over && game.score === 0
            text: "← → 移动 · 鼠标可以拖着飞"
            font.pixelSize: 12
            color: "#7E8DB0"
        }

        // 结束遮罩
        Rectangle {
            anchors.fill: parent
            visible: game.over
            // z 要比下面那块拖拽 MouseArea 高，否则「再来一局」的点击
            // 会被拖拽层吞掉（同 z 时按声明顺序，后声明的在上）
            z: 5
            radius: 16
            color: "#C0000000"

            Column {
                anchors.centerIn: parent
                spacing: 16

                Text {
                    anchors.horizontalCenter: parent.horizontalCenter
                    text: "打完了！得分 " + game.score
                    font.pixelSize: 24
                    font.bold: true
                    color: "#FFFFFF"
                }

                Rectangle {
                    anchors.horizontalCenter: parent.horizontalCenter
                    width: 158
                    height: 48
                    radius: 24
                    color: again.pressed ? "#F57C00" : "#FFB74D"

                    Text {
                        anchors.centerIn: parent
                        text: "再来一局"
                        font.pixelSize: 16
                        font.bold: true
                        color: "#4E342E"
                    }

                    MouseArea {
                        id: again
                        anchors.fill: parent
                        onClicked: game.restart()
                    }
                }
            }
        }

        // 鼠标拖动
        MouseArea {
            anchors.fill: parent
            hoverEnabled: false
            onPressed: {
                game.forceActiveFocus()
                game.dragX = (mouse.x - canvas.ox) / canvas.s
            }
            onPositionChanged: {
                if (pressed) {
                    game.dragX = (mouse.x - canvas.ox) / canvas.s
                }
            }
            onReleased: game.dragX = -1
            onCanceled: game.dragX = -1
        }
    }

    focus: true

    Component.onCompleted: forceActiveFocus()

    Keys.onPressed: function (event) {
        if (event.key === Qt.Key_Left || event.key === Qt.Key_A) {
            game.keyLeft = true
            event.accepted = true
        } else if (event.key === Qt.Key_Right || event.key === Qt.Key_D) {
            game.keyRight = true
            event.accepted = true
        } else if (event.key === Qt.Key_Space && game.over) {
            game.restart()
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
