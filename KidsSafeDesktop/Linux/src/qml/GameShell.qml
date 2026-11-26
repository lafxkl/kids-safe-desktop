import QtQuick
import QtQuick.Layouts
import QtQuick.Window
import KidsSafe 1.0

/*
 * 小游戏的公共外壳：背景 + 顶部栏（返回 / 图标 / 标题 / 计分）+ 提示条 + 内容区。
 *
 * 抽出来是因为几个游戏页的头部完全一样，而 QML 里复制好几遍返回按钮的点击区，
 * 迟早有一个漏掉 —— 孩子点了没反应，家长只会觉得「这软件坏了」。
 *
 * 内容区怎么用（重要）：
 *   GameShell 把中间那块留成 `contentArea`，它和子页面声明的内容是**兄弟**，
 *   所以子页面里写：
 *       Item { anchors.fill: page.contentArea }
 *   即可精确铺满中间，不会盖住顶部栏。
 *
 *   刻意没有用 `default property alias content: contentArea.data`：
 *   那样连 GameShell 自己声明的 Backdrop / 顶部栏都会被塞进内容区，
 *   而内容区的尺寸又取决于这些子项 —— 直接构成布局循环，界面会整块空白。
 */
Item {
    id: shell

    // TapHandler 里拿不到 Window.window（附加属性靠 Item 反查，handler 不是 Item），
    // 统一在根 Item 上取一次，页面里都用 shell.navWin。
    readonly property var navWin: Window.window

    property string title: ""
    property string glyph: "star"
    property color accent: "#8E7CFF"
    property string prompt: ""
    property string scoreText: ""

    // 由游戏大厅通过 StackView.push(url, { "difficulty": n }) 注入。
    // 1 = 简单，2 = 中等，3 = 困难；各游戏自己解释（数量 / 牌数 / 出洞速度）。
    property int difficulty: 2

    // 子页面用它定位内容
    readonly property Item contentArea: contentAreaItem

    Backdrop {
        anchors.fill: parent
    }

    // ---- 顶部栏 ----
    RowLayout {
        id: header

        anchors.left: parent.left
        anchors.right: parent.right
        anchors.top: parent.top
        anchors.margins: 26
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
                onTapped: shell.navWin.popPage()
            }
        }

        Rectangle {
            Layout.preferredWidth: 46
            Layout.preferredHeight: 46
            radius: 15
            color: shell.accent

            GlyphIcon {
                anchors.centerIn: parent
                width: 26
                height: 26
                name: shell.glyph
                color: "white"
            }
        }

        Text {
            text: shell.title
            font.pixelSize: 26
            font.weight: Font.Bold
            color: "#4A3B32"
        }

        Item {
            Layout.fillWidth: true
        }

        Rectangle {
            visible: shell.scoreText !== ""
            Layout.preferredHeight: 44
            Layout.preferredWidth: scoreLabel.implicitWidth + 40
            radius: 22
            color: "#FFE0B2"

            Text {
                id: scoreLabel

                anchors.centerIn: parent
                text: shell.scoreText
                font.pixelSize: 17
                font.weight: Font.DemiBold
                color: "#E65100"
            }
        }
    }

    // ---- 提示条 ----
    Rectangle {
        id: promptBar

        anchors.left: parent.left
        anchors.right: parent.right
        anchors.top: header.bottom
        anchors.leftMargin: 26
        anchors.rightMargin: 26
        anchors.topMargin: 12
        height: 46
        radius: 23
        color: "#D9FFFFFF"

        Text {
            anchors.centerIn: parent
            width: parent.width - 44
            text: shell.prompt
            font.pixelSize: 19
            font.weight: Font.DemiBold
            color: "#5D4037"
            elide: Text.ElideRight
            horizontalAlignment: Text.AlignHCenter
        }
    }

    // ---- 内容区（子页面 anchors.fill 到这里）----
    Item {
        id: contentAreaItem

        anchors.left: parent.left
        anchors.right: parent.right
        anchors.top: promptBar.bottom
        anchors.bottom: parent.bottom
        anchors.leftMargin: 26
        anchors.rightMargin: 26
        anchors.topMargin: 12
        anchors.bottomMargin: 22
    }
}
