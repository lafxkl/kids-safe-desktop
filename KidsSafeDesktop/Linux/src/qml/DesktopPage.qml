import QtQuick
import QtQuick.Layouts
import QtQuick.Window
import KidsSafe 1.0

/*
 * 儿童桌面。
 *
 * 这是「儿童桌面环境」的第一版：一张壁纸、几个图标、一个程序坞，以及
 * 三扇能同时开着、能拖、能最大化的窗口（文件管理器 / 终端 / 任务管理器）。
 *
 * 为什么不做成「点图标全屏切页面」——那本质上还是一个应用，不是桌面。
 * 孩子第一次用电脑，最需要建立的概念是「同时可以有好几件事开着」：
 * 一边看文件一边敲命令，窗口能重叠、能挪开。全屏栈做不出这个。
 *
 * kiosk 会话里没有窗口管理器（不启 plasmashell / kwin），所以这里不开
 * 真的顶层窗口 —— 开了没人管，盖满屏幕还拖不动。所有「窗口」都是画在
 * 这个页面里的卡片，z 序、拖动、最大化由本页统一管（详见 DesktopWindow.qml）。
 *
 * 三层结构，从下往上：
 *   1. 壁纸（渐变 + 太阳 + 山丘 + 飘动的云），纯装饰，不接收鼠标；
 *   2. 桌面图标（左列）与窗口工作区；
 *   3. 顶部状态条与底部程序坞 —— z 最高，窗口最大化后也够得着，
 *      否则最大化一开，孩子就再也点不到「回主页」了。
 */
Item {
    id: desk

    readonly property var navWin: Window.window

    /*
     * 三个内置工具。dock 和桌面图标都从这一份数据生成，加第四个工具只改这里。
     *
     * w / h 是默认窗口大小，实际开窗时还要按工作区尺寸收一次（大屏幕上
     * 960×620 挺合适，1280×800 上再大一档就会顶到程序坞）。
     */
    readonly property var tools: [
        {
            "id": "files",
            "label": "文件管理器",
            "glyph": "folder",
            "accent": "#3B82F6",
            "w": 980,
            "h": 620
        },
        {
            "id": "terminal",
            "label": "终端",
            "glyph": "terminal",
            "accent": "#0F766E",
            "w": 840,
            "h": 540
        },
        {
            "id": "tasks",
            "label": "任务管理器",
            "glyph": "chip",
            "accent": "#8B5CF6",
            "w": 920,
            "h": 600
        }
    ]

    // ------------------------------------------------------------------
    // 时钟：状态条右上角。10 秒刷一次就够 —— 只显示到分钟，秒针是多余的。
    // ------------------------------------------------------------------
    property string nowTime: ""
    property string nowDate: ""

    function tickClock() {
        const d = new Date();
        desk.nowTime = Qt.formatTime(d, "HH:mm");
        const week = ["周日", "周一", "周二", "周三", "周四", "周五", "周六"];
        desk.nowDate = week[d.getDay()] + " " + (d.getMonth() + 1) + "月" + d.getDate() + "日";
    }

    Component.onCompleted: tickClock()

    Timer {
        interval: 10000
        running: true
        repeat: true
        onTriggered: desk.tickClock()
    }

    // ------------------------------------------------------------------
    // 窗口管理
    // ------------------------------------------------------------------
    property int nextKey: 1
    property int activeKey: 0

    ListModel {
        id: openWindows
    }

    function metaOf(appId) {
        for (let i = 0; i < desk.tools.length; ++i) {
            if (desk.tools[i].id === appId) {
                return desk.tools[i];
            }
        }
        return null;
    }

    // appId → 页面组件。用函数查而不是把 Component 塞进 ListModel：
    // ListModel 的 role 只能是基础类型，存不了 Component / QObject。
    function compFor(appId) {
        if (appId === "files") {
            return fileManagerPage;
        }
        if (appId === "terminal") {
            return terminalPage;
        }
        if (appId === "tasks") {
            return taskManagerPage;
        }
        return null;
    }

    function isOpen(appId) {
        for (let i = 0; i < openWindows.count; ++i) {
            if (openWindows.get(i).appId === appId) {
                return true;
            }
        }
        return false;
    }

    function focusWindow(key) {
        desk.activeKey = key;
    }

    /*
     * 打开（或切到）一个工具。
     *
     * 已经开着就不再开第二个 —— 同一个程序开两份窗口对孩子没有意义，
     * 而且终端开两份会是两条独立的 shell，容易搞混「我刚才在哪敲的」。
     * 这里改成把它提到最前，和真实系统的 dock 行为一致。
     */
    function launch(appId) {
        for (let i = 0; i < openWindows.count; ++i) {
            const row = openWindows.get(i);
            if (row.appId === appId) {
                desk.focusWindow(row.key);
                return;
            }
        }

        const m = desk.metaOf(appId);
        if (!m) {
            return;
        }

        // 层叠摆放：每开一个就往右下错开一点，三个窗口的标题栏都露在外面，
        // 一眼能看出「开了三个」。错开到第四轮回到原点，避免一直往外跑。
        const shift = (openWindows.count % 4) * 34;
        const w = Math.min(m.w, workArea.width - 32);
        const h = Math.min(m.h, workArea.height - 24);
        const x = Math.max(8, Math.min(workArea.width - w - 8, 44 + shift));
        const y = Math.max(6, Math.min(workArea.height - h - 8, 18 + shift));

        const k = desk.nextKey++;

        openWindows.append({
            "appId": appId,
            "winTitle": m.label,
            "winGlyph": m.glyph,
            "winAccent": String(m.accent),
            "key": k,
            "spawnX": x,
            "spawnY": y,
            "spawnW": w,
            "spawnH": h
        });

        desk.activeKey = k;
    }

    function closeWindow(key) {
        for (let i = 0; i < openWindows.count; ++i) {
            if (openWindows.get(i).key === key) {
                openWindows.remove(i);
                break;
            }
        }
        if (desk.activeKey === key) {
            // 关掉当前窗口后，把焦点交给最后剩下的那个（通常就是刚被它盖住的）
            desk.activeKey = openWindows.count > 0
                    ? openWindows.get(openWindows.count - 1).key : 0;
        }
    }

    // ==================================================================
    // 1. 壁纸
    // ==================================================================
    Rectangle {
        anchors.fill: parent

        gradient: Gradient {
            GradientStop { position: 0.00; color: "#FFF8EC" }
            GradientStop { position: 0.45; color: "#FFEFD8" }
            GradientStop { position: 1.00; color: "#FFDDB6" }
        }
    }

    // 偏右上的太阳。两层同心圆，外层当光晕、内层当本体 ——
    // 直接画一个大圆会像一颗贴在纸上的球，两层才像「发光的」。
    Rectangle {
        x: parent.width * 0.62
        y: -parent.width * 0.09
        width: parent.width * 0.30
        height: width
        radius: width / 2
        color: "#FFCE8A"
        opacity: 0.34
    }

    Rectangle {
        x: parent.width * 0.70
        y: -parent.width * 0.04
        width: parent.width * 0.16
        height: width
        radius: width / 2
        color: "#FFC46B"
        opacity: 0.42
    }

    // 远处的山：Canvas 画三层，越靠下越深，做出空气透视
    Canvas {
        id: hills

        anchors.left: parent.left
        anchors.right: parent.right
        anchors.bottom: parent.bottom
        height: Math.max(200, parent.height * 0.34)
        renderStrategy: Canvas.Cooperative

        onPaint: {
            const ctx = hills.getContext("2d");
            if (!ctx) {
                return;
            }
            ctx.reset();
            const w = width;
            const h = height;

            // 第一层：最远、最浅
            ctx.beginPath();
            ctx.moveTo(0, h);
            ctx.lineTo(0, h * 0.62);
            ctx.quadraticCurveTo(w * 0.16, h * 0.20, w * 0.34, h * 0.56);
            ctx.quadraticCurveTo(w * 0.50, h * 0.88, w * 0.68, h * 0.46);
            ctx.quadraticCurveTo(w * 0.84, h * 0.10, w, h * 0.50);
            ctx.lineTo(w, h);
            ctx.closePath();
            ctx.fillStyle = "#FFE7C6";
            ctx.fill();

            // 第二层：稍微靠近、稍微深一点，峰谷与第一层错开
            ctx.beginPath();
            ctx.moveTo(0, h);
            ctx.lineTo(0, h * 0.82);
            ctx.quadraticCurveTo(w * 0.22, h * 0.44, w * 0.44, h * 0.78);
            ctx.quadraticCurveTo(w * 0.66, h * 1.06, w * 0.82, h * 0.70);
            ctx.quadraticCurveTo(w * 0.92, h * 0.54, w, h * 0.66);
            ctx.lineTo(w, h);
            ctx.closePath();
            ctx.fillStyle = "#FFD9AC";
            ctx.fill();

            // 第三层：最近的一道缓坡，压住画面底部
            ctx.beginPath();
            ctx.moveTo(0, h);
            ctx.lineTo(0, h * 0.92);
            ctx.quadraticCurveTo(w * 0.40, h * 0.74, w * 0.74, h * 0.90);
            ctx.quadraticCurveTo(w * 0.90, h * 0.97, w, h * 0.90);
            ctx.lineTo(w, h);
            ctx.closePath();
            ctx.fillStyle = "#F7C58F";
            ctx.fill();
        }

        onWidthChanged: requestPaint()
        onHeightChanged: requestPaint()
    }

    // 两朵慢慢飘的云。动画周期很长（60 秒走一趟），目的是「活着」而不是
    // 吸引注意力 —— 桌面上的动态一旦快起来，孩子就会盯着看而不去用。
    Rectangle {
        width: 190
        height: 44
        radius: 22
        color: "#FFFFFF"
        opacity: 0.55
        y: hills.y * 0.34

        SequentialAnimation on x {
            loops: Animation.Infinite
            NumberAnimation { from: -200; to: desk.width + 40; duration: 72000; easing.type: Easing.Linear }
        }
    }

    Rectangle {
        width: 130
        height: 32
        radius: 16
        color: "#FFFFFF"
        opacity: 0.42
        y: hills.y * 0.12

        SequentialAnimation on x {
            loops: Animation.Infinite
            NumberAnimation { from: desk.width * 0.55; to: -160; duration: 96000; easing.type: Easing.Linear }
        }
    }

    // ==================================================================
    // 2. 桌面图标（左列）
    // ==================================================================
    Column {
        id: desktopIcons

        x: 20
        y: topBar.height + 22
        spacing: 12
        z: 2

        Repeater {
            model: desk.tools

            DesktopIcon {
                required property var modelData

                label: modelData.label
                glyph: modelData.glyph
                accent: modelData.accent

                onActivated: desk.launch(modelData.id)
            }
        }

        // 回主界面的入口。桌面图标里也放一份，因为孩子从「全部应用」进来后，
        // 第一反应往往是回到原来那个彩色主页，而不是去点右上角。
        DesktopIcon {
            label: "全部应用"
            glyph: "grid"
            accent: "#8A7565"
            onActivated: desk.navWin.popPage()
        }
    }

    // 右下角一块淡淡的提示牌，第一次进桌面时告诉孩子怎么用。
    // 没有任何交互，纯粹是「说明书」，所以做成半透明不抢注意力。
    Rectangle {
        z: 2
        anchors.right: parent.right
        anchors.bottom: dock.top
        anchors.rightMargin: 34
        anchors.bottomMargin: 18
        width: tips.implicitWidth + 36
        height: tips.implicitHeight + 26
        radius: 16
        color: "#40FFFFFF"
        border.width: 1
        border.color: "#4DFFFFFF"
        visible: openWindows.count === 0

        Text {
            id: tips

            anchors.centerIn: parent
            text: "点左边的图标打开工具\n窗口可以拖动，右上角能放大"
            font.pixelSize: 13
            lineHeight: 1.5
            color: "#8A7565"
        }
    }

    // ==================================================================
    // 3. 窗口工作区
    //
    // 单独一层而不是把窗口直接挂在根上：最大化要铺满「去掉状态条和程序坞」
    // 的那块矩形，给窗口一个尺寸确定的 parent 最省事 —— DesktopWindow 的
    // toggleMaximize() 用的就是 parent.width / parent.height。
    // ==================================================================
    Item {
        id: workArea

        anchors.top: topBar.bottom
        anchors.left: parent.left
        anchors.right: parent.right
        anchors.bottom: parent.bottom
        anchors.bottomMargin: 106
        anchors.leftMargin: 8
        anchors.rightMargin: 8
        z: 5

        Repeater {
            model: openWindows

            DesktopWindow {
                id: winItem

                required property int index
                required property int key
                required property string appId
                required property string winTitle
                required property string winGlyph
                required property string winAccent
                required property real spawnX
                required property real spawnY
                required property real spawnW
                required property real spawnH

                x: spawnX
                y: spawnY
                width: spawnW
                height: spawnH

                title: winTitle
                glyph: winGlyph
                accent: winAccent
                page: desk.compFor(appId)
                activeWindow: desk.activeKey === key
                // 当前窗口压在最上面；其余按打开顺序叠。
                // 不用「点一次 +1」的计数器：计数会溢出，而且关窗后不回收。
                z: desk.activeKey === key ? 400 : 100 + index

                onRequestActivate: desk.focusWindow(winItem.key)
                onRequestClose: winItem.playClose()

                // 关闭时先播一段 110ms 的淡出再删，直接 remove 会「啪」地消失，
                // 孩子会以为程序崩了。
                function playClose() {
                    exitAnim.start();
                }

                ParallelAnimation {
                    id: exitAnim

                    NumberAnimation {
                        target: winItem
                        property: "opacity"
                        to: 0
                        duration: 110
                    }
                    NumberAnimation {
                        target: winItem
                        property: "scale"
                        to: 0.98
                        duration: 110
                    }
                    onFinished: desk.closeWindow(winItem.key)
                }
            }
        }
    }

    // ==================================================================
    // 4. 顶部状态条
    // ==================================================================
    Rectangle {
        id: topBar

        z: 900
        anchors.top: parent.top
        anchors.left: parent.left
        anchors.right: parent.right
        height: 46

        gradient: Gradient {
            GradientStop { position: 0.0; color: "#4DFFFFFF" }
            GradientStop { position: 1.0; color: "#00FFFFFF" }
        }

        RowLayout {
            anchors.fill: parent
            anchors.leftMargin: 20
            anchors.rightMargin: 18
            spacing: 10

            Rectangle {
                Layout.preferredWidth: 26
                Layout.preferredHeight: 26
                radius: 13
                color: "#FFFFFF"

                GlyphIcon {
                    anchors.centerIn: parent
                    name: "bear"
                    width: 22
                    height: 22
                }
            }

            Text {
                text: "小朋友的电脑"
                font.pixelSize: 14
                font.weight: Font.DemiBold
                color: "#4A3B32"
            }

            Item {
                Layout.fillWidth: true
            }

            Text {
                text: desk.nowDate + "  " + desk.nowTime
                font.pixelSize: 13
                color: "#7A6759"
            }

            // 回主页。做成一个明显的胶囊按钮，而不是角落里的小图标 ——
            // 孩子要「逃离」桌面时不该去找。
            Rectangle {
                Layout.preferredWidth: homeText.implicitWidth + 30
                Layout.preferredHeight: 30
                radius: 15
                color: homeHover.hovered ? "#FFFFFF" : "#D9FFFFFF"
                border.width: 1
                border.color: "#66FFFFFF"

                Text {
                    id: homeText

                    anchors.centerIn: parent
                    text: "回主页"
                    font.pixelSize: 13
                    font.weight: Font.DemiBold
                    color: "#4A3B32"
                }

                HoverHandler {
                    id: homeHover
                }

                TapHandler {
                    onTapped: desk.navWin.popPage()
                }
            }
        }
    }

    // ==================================================================
    // 5. 底部程序坞
    // ==================================================================
    Rectangle {
        id: dock

        z: 900
        anchors.horizontalCenter: parent.horizontalCenter
        anchors.bottom: parent.bottom
        anchors.bottomMargin: 16
        width: dockRow.implicitWidth + 30
        height: 74
        radius: 24
        color: "#D9FFFFFF"
        border.width: 1
        border.color: "#73FFFFFF"

        // 投影：坞是浮在壁纸上的，没有阴影会和背景糊在一起
        Rectangle {
            anchors.fill: parent
            anchors.margins: -2
            z: -1
            radius: parent.radius + 2
            color: "#14000000"
        }

        Row {
            id: dockRow

            anchors.centerIn: parent
            spacing: 8

            Repeater {
                model: desk.tools

                DockButton {
                    required property var modelData

                    glyph: modelData.glyph
                    accent: modelData.accent
                    tip: modelData.label
                    running: desk.isOpen(modelData.id)

                    onActivated: desk.launch(modelData.id)
                }
            }

            Rectangle {
                width: 1
                height: 32
                anchors.verticalCenter: parent.verticalCenter
                color: "#26000000"
            }

            DockButton {
                glyph: "grid"
                accent: "#8A7565"
                tip: "全部应用"
                onActivated: desk.navWin.popPage()
            }
        }
    }

    // ==================================================================
    // 三个工具的页面（懒加载：真的打开窗口时才实例化）
    // ==================================================================
    Component {
        id: fileManagerPage

        FileManagerPage {}
    }

    Component {
        id: terminalPage

        TerminalPage {}
    }

    Component {
        id: taskManagerPage

        TaskManagerPage {}
    }
}
