import QtQuick
import QtQuick.Layouts
import QtQuick.Window
import KidsSafe 1.0

/*
 * 游戏乐园大厅：难度选择 + 网页游戏入口 + 原生小游戏磁贴。
 *
 * 两层内容：
 *   1. 网页游戏 —— 15 款 HTML5，与 Windows / Android 端同一份资源，
 *      由 WebGamePage 用 QtWebEngine 打开（没编译 WebEngine 时降级提示）；
 *   2. 原生小游戏 —— 用 QML 重写的五款，好处是完全离线、启动快、
 *      画画/音效都走 Canvas，不依赖网页运行时。
 *
 * 难度是全局选择（三档），点进原生游戏时通过 StackView 的属性传过去；
 * 网页游戏自带三档难度，不需要这里传。
 */
Item {
    id: page

    readonly property var navWin: Window.window
    property int difficulty: 2

    Backdrop {
        anchors.fill: parent
    }

    ListModel {
        id: games

        ListElement {
            name: "数一数"
            glyph: "star"
            accent: "#26A69A"
            desc: "数数有几个，选对数字"
            pageUrl: "CountingGamePage.qml"
        }
        ListElement {
            name: "拼图配对"
            glyph: "grid"
            accent: "#EF6C00"
            desc: "翻开卡片，找出相同的一对"
            pageUrl: "MatchingGamePage.qml"
        }
        ListElement {
            name: "找颜色"
            glyph: "palette"
            accent: "#3949AB"
            desc: "看名字，找出对应的颜色"
            pageUrl: "FindColorGamePage.qml"
        }
        ListElement {
            name: "算术乐园"
            glyph: "pencil"
            accent: "#00897B"
            desc: "加减法我来算"
            pageUrl: "ArithmeticGamePage.qml"
        }
        ListElement {
            name: "打地鼠"
            glyph: "hammer"
            accent: "#6D4C41"
            desc: "地鼠冒头快点它"
            pageUrl: "WhackMoleGamePage.qml"
        }
        ListElement {
            name: "数独"
            glyph: "sudoku"
            accent: "#3949AB"
            desc: "4×4 儿童版，每行每列不重复"
            pageUrl: "SudokuGamePage.qml"
        }
        // 红白机与故事剧场都不在这里的 ListModel 里？红白机在下面单独一张卡
        // （它要显示「有没有 ROM」）；故事剧场走这张：游戏本体由家长放，
        // 没有时页面自己会说明去哪儿放。
        ListElement {
            name: "故事剧场"
            glyph: "moon"
            accent: "#7E57C2"
            desc: "吉里吉里互动故事，家长放游戏"
            pageUrl: "VnPage.qml"
        }
        ListElement {
            name: "小小程序员"
            glyph: "laptop"
            accent: "#5C6BC0"
            desc: "搭积木写 C 语言，编译运行"
            pageUrl: "CodeStudioPage.qml"
        }
        // 项目工坊与「小小程序员」是互补的两件事：那边是从零搭，
        // 这边是拆三个写完的作品（飞机大战 / 推箱子 / 横板冒险），
        // 每个都带关键代码和可调参数 —— 前者教「怎么写」，后者教「读」。
        ListElement {
            name: "项目工坊"
            glyph: "rocket"
            accent: "#EF6C00"
            desc: "拆三个写完的项目，改参数看变化"
            pageUrl: "ProjectLabPage.qml"
        }
    }

    function openGame(gameUrl) {
        // 双保险：列表里已经按语言删过这一项，这里再拦一次 ——
        // 将来若有别的入口直接跳 VnPage，也不该绕过语言限制。
        if (gameUrl === "VnPage.qml" && !Settings.allowVn) {
            return;
        }
        page.navWin.pushPage(Qt.resolvedUrl(gameUrl), {
            "difficulty": page.difficulty
        });
    }

    Component.onCompleted: {
        // 简体中文模式（默认）不显示「故事剧场」：吉里吉里游戏绝大多数是
        // 成人向视觉小说，中文分发版不带这个入口；切到 English 才出现。
        // 用 remove 而不是把卡片设成 invisible —— Grid 会留下一个空洞。
        if (!Settings.allowVn) {
            for (let i = 0; i < games.count; ++i) {
                if (games.get(i).pageUrl === "VnPage.qml") {
                    games.remove(i);
                    break;
                }
            }
        }
    }

    ColumnLayout {
        anchors.fill: parent
        anchors.margins: 26
        spacing: 16

        // ---- 顶部栏 ----
        RowLayout {
            Layout.fillWidth: true
            spacing: 14

            Rectangle {
                Layout.preferredWidth: backLabel.implicitWidth + 36
                Layout.preferredHeight: 48
                radius: 24
                color: "#FFFFFF"
                border.color: "#F2E1CD"
                border.width: 1

                Text {
                    id: backLabel

                    anchors.centerIn: parent
                    text: "‹ " + Settings.t("common.back")
                    font.pixelSize: 17
                    font.weight: Font.DemiBold
                    color: "#7A6759"
                }

                TapHandler {
                    onTapped: page.navWin.popPage()
                }
            }

            Rectangle {
                Layout.preferredWidth: 46
                Layout.preferredHeight: 46
                radius: 15
                color: "#5AC8E8"

                GlyphIcon {
                    anchors.centerIn: parent
                    width: 26
                    height: 26
                    name: "gamepad"
                    color: "white"
                }
            }

            Text {
                text: "游戏乐园"
                font.pixelSize: 26
                font.weight: Font.Bold
                color: "#4A3B32"
            }

            Item {
                Layout.fillWidth: true
            }

            // ---- 难度：三档胶囊 ----
            RowLayout {
                spacing: 8

                Repeater {
                    model: [
                        { "label": "简单", "level": 1 },
                        { "label": "中等", "level": 2 },
                        { "label": "困难", "level": 3 }
                    ]

                    Rectangle {
                        required property var modelData

                        Layout.preferredWidth: 74
                        Layout.preferredHeight: 44
                        radius: 22
                        color: page.difficulty === modelData.level ? "#7E57C2" : "#FFFFFF"
                        border.color: page.difficulty === modelData.level ? "#7E57C2" : "#F2E1CD"
                        border.width: 1

                        Text {
                            anchors.centerIn: parent
                            text: modelData.label
                            font.pixelSize: 16
                            font.weight: Font.DemiBold
                            color: page.difficulty === modelData.level ? "white" : "#7A6759"
                        }

                        TapHandler {
                            onTapped: page.difficulty = modelData.level
                        }
                    }
                }
            }
        }

        // ---- 两块「大内容」：网页游戏 + 红白机 ----
        // 并排而不是上下叠：竖着放两张 108 高的卡，1280×800 的屏上
        // 下面那排磁贴就只剩半行，孩子得先滚动才能看到自己画的游戏。
        RowLayout {
            Layout.fillWidth: true
            spacing: 16

            // 网页游戏（15 款 HTML5，与 Windows / Android 同一份资源）
            //
            // Windows 端的「游戏乐园」磁贴打开的就是 Playland 自带的 index.html，
            // 里面有 15 张卡片。Linux 这里不做二次开发：直接把这套页面用
            // QtWebEngine 跑起来，玩法和美术与另外两端完全一致。
            //
            // 没编译 WebEngine 时不隐藏这张卡，而是换成灰色 + 一行说明：
            // 直接消失会让家长以为「说好的 15 款游戏去哪了」。
            HomeBanner {
                Layout.fillWidth: true
                Layout.preferredHeight: 108
                glyph: "rocket"
                title: "网页游戏乐园 · 15 款"
                subtitle: Playland.available
                          ? "雷霆战机 · 五子棋 · 贪吃蛇 · 象棋…"
                          : "未安装网页组件，暂不可用"
                colorFrom: Playland.available ? "#667EEA" : "#B3AEC4"
                colorTo: Playland.available ? "#9F7AEA" : "#CCC7DA"
                onTapped: {
                    if (Playland.available) {
                        page.navWin.pushPage(Qt.resolvedUrl("WebGamePage.qml"));
                    }
                }
            }

            // 红白机（NES）。核心是 SimpleNes，ROM 由家长自己放进
            // ~/.local/share/kidssafe/roms —— 版权原因不随包分发。
            HomeBanner {
                Layout.fillWidth: true
                Layout.preferredHeight: 108
                glyph: "gamepad"
                title: Nes.roms.length > 0
                       ? ("红白机 · " + Nes.roms.length + " 个")
                       : "红白机 · NES"
                subtitle: Nes.roms.length > 0
                          ? "超级玛丽 · 魂斗罗 · 坦克大战…"
                          : "还没有游戏，家长可放入 .nes"
                colorFrom: "#E53935"
                colorTo: "#FB8C66"
                onTapped: page.navWin.pushPage(Qt.resolvedUrl("NesPage.qml"))
            }
        }

        Text {
            visible: !Playland.available
            Layout.leftMargin: 6
            text: "家长在终端执行 sudo apt install qt6-webengine-dev qml6-module-qtwebengine 后重新编译即可启用"
            font.pixelSize: 13
            color: "#B8A492"
        }

        Text {
            Layout.leftMargin: 6
            text: Playland.available ? "下面是自己画的小游戏，点开就能玩" : "挑一个喜欢的游戏吧"
            font.pixelSize: 15
            color: "#9B8574"
        }

        // ---- 游戏磁贴 ----
        Flickable {
            Layout.fillWidth: true
            Layout.fillHeight: true
            contentWidth: width
            contentHeight: gameGrid.height
            clip: true
            boundsBehavior: Flickable.StopAtBounds

            Grid {
                id: gameGrid

                width: parent.width
                columns: Math.max(2, Math.min(4, Math.floor(width / 250)))
                spacing: 16
                topPadding: 4

                Repeater {
                    model: games

                    Rectangle {
                        required property string name
                        required property string glyph
                        required property color accent
                        required property string desc
                        required property string pageUrl

                        width: Math.floor((gameGrid.width - (gameGrid.columns - 1) * gameGrid.spacing) / gameGrid.columns)
                        height: 156
                        radius: 26
                        color: "#FFFFFF"
                        border.color: "#F2E1CD"
                        border.width: 1

                        scale: tileTap.pressed ? 0.97 : (tileHover.hovered ? 1.02 : 1.0)
                        Behavior on scale {
                            NumberAnimation { duration: 110; easing.type: Easing.OutCubic }
                        }

                        RowLayout {
                            anchors.fill: parent
                            anchors.margins: 18
                            spacing: 14

                            Rectangle {
                                Layout.preferredWidth: 62
                                Layout.preferredHeight: 62
                                radius: 20
                                color: accent

                                GlyphIcon {
                                    anchors.centerIn: parent
                                    width: 34
                                    height: 34
                                    name: glyph
                                    color: "white"
                                }
                            }

                            ColumnLayout {
                                Layout.fillWidth: true
                                spacing: 5

                                Text {
                                    Layout.fillWidth: true
                                    text: name
                                    font.pixelSize: 21
                                    font.weight: Font.Bold
                                    color: "#4A3B32"
                                    elide: Text.ElideRight
                                }

                                Text {
                                    Layout.fillWidth: true
                                    text: desc
                                    font.pixelSize: 13
                                    color: "#9B8574"
                                    wrapMode: Text.Wrap
                                    maximumLineCount: 2
                                }
                            }
                        }

                        HoverHandler {
                            id: tileHover
                        }

                        TapHandler {
                            id: tileTap
                            onTapped: page.openGame(pageUrl)
                        }
                    }
                }
            }
        }
    }
}
