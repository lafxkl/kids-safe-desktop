import QtQuick

/*
 * 桌面上的一个图标（图标 + 名字）。
 *
 * 单击就打开，不等双击。双击是成人习惯，七八岁的孩子经常两次点击间隔太长、
 * 或者手一抖拖动了几个像素，结果「点了没反应」。所以这里单击直接激活，
 * 代价是无法「选中」图标 —— 但桌面上的图标只有 4 个，也不需要框选。
 *
 * 底座用半透明白 + 一点阴影，因为壁纸是渐变的暖色：纯白会在浅色区域糊成一团，
 * 纯透明又会在深色区域看不见。
 */
Item {
    id: root

    property string label: ""
    property string glyph: "folder"
    property color accent: "#3B82F6"
    property bool hovered: false

    signal activated()

    implicitWidth: 96
    implicitHeight: 104

    // 悬停时整体轻微放大 + 图标底座加深，给出「可以点」的反馈。
    // 缩放锚在顶部，否则放大会把下面的文字往上顶。
    scale: hovered ? 1.06 : 1.0
    Behavior on scale {
        NumberAnimation { duration: 110; easing.type: Easing.OutCubic }
    }

    Rectangle {
        id: tile

        anchors.horizontalCenter: parent.horizontalCenter
        anchors.top: parent.top
        width: 64
        height: 64
        radius: 18
        color: root.hovered ? "#FFFFFF" : "#F2FFFFFF"
        border.width: 1
        border.color: root.hovered ? "#FFFFFF" : "#66FFFFFF"

        // 底部一点投影，让图标从壁纸上「浮」起来
        Rectangle {
            anchors.fill: parent
            anchors.margins: -1
            z: -1
            radius: parent.radius + 1
            color: "#16000000"
        }

        GlyphIcon {
            anchors.centerIn: parent
            name: root.glyph
            width: 34
            height: 34
            color: root.accent
        }
    }

    Text {
        anchors.top: tile.bottom
        anchors.topMargin: 7
        anchors.horizontalCenter: parent.horizontalCenter
        width: parent.width
        text: root.label
        horizontalAlignment: Text.AlignHCenter
        elide: Text.ElideRight
        font.pixelSize: 13
        font.weight: Font.DemiBold
        // 壁纸是暖浅色，白字读不出来；用深棕并加一层淡阴影，深浅区域都能看清
        color: "#4A3B32"
        style: Text.Raised
        styleColor: "#B3FFFFFF"
    }

    HoverHandler {
        id: hover
        onHoveredChanged: root.hovered = hovered
    }

    TapHandler {
        // 只认左键。右键在儿童机上没有约定俗成的含义，
        // 绑一个「删除」之类的操作反而危险。
        acceptedButtons: Qt.LeftButton
        onTapped: root.activated()
    }
}
