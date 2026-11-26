import QtQuick

/*
 * 程序坞上的一个按钮。
 *
 * 和桌面图标不同，这里只放图不放字：坞很矮，写四个字会挤成两行。
 * 名字靠悬停时浮出的气泡补上（hoverTip）。
 *
 * running 那个小圆点表示「这个程序已经开着」。有它，孩子才能理解
 * 「再点一次是把窗口找回来」，而不是「又开一个」。
 */
Item {
    id: root

    property string glyph: "folder"
    property color accent: "#3B82F6"
    property string tip: ""
    property bool running: false
    property bool hovered: false

    signal activated()

    implicitWidth: 56
    implicitHeight: 56

    // 悬停上浮 3px：这是最常见也最不打扰的反馈
    y: hovered ? -3 : 0
    Behavior on y {
        NumberAnimation { duration: 110; easing.type: Easing.OutCubic }
    }

    Rectangle {
        anchors.fill: parent
        radius: 16
        color: root.hovered ? "#FFFFFF" : "transparent"

        GlyphIcon {
            anchors.centerIn: parent
            name: root.glyph
            width: 28
            height: 28
            color: root.accent
        }
    }

    // 运行指示点
    Rectangle {
        anchors.horizontalCenter: parent.horizontalCenter
        anchors.bottom: parent.bottom
        anchors.bottomMargin: -7
        width: running ? 5 : 0
        height: 5
        radius: 3
        color: "#8A7565"
        opacity: 0.85
    }

    // ---- 悬停气泡 ----
    Rectangle {
        anchors.horizontalCenter: parent.horizontalCenter
        anchors.bottom: parent.top
        anchors.bottomMargin: 10
        width: tipText.implicitWidth + 20
        height: 26
        radius: 9
        color: "#E6403A31"
        visible: root.hovered && root.tip.length > 0

        Text {
            id: tipText
            anchors.centerIn: parent
            text: root.tip
            font.pixelSize: 12
            color: "white"
        }
    }

    HoverHandler {
        onHoveredChanged: root.hovered = hovered
    }

    TapHandler {
        acceptedButtons: Qt.LeftButton
        onTapped: root.activated()
    }
}
