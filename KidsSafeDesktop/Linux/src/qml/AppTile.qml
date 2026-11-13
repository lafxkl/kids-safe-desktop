import QtQuick

/*
 * 应用磁贴。
 *
 * 图标这里用「彩色方块 + 名称首字」的字母头像，而不是 Kirigami.Icon：
 * 应用图标的可靠性完全取决于目标机器的图标主题是否完整，儿童桌面跑在各种
 * 精简发行版上时经常出现空白图标，比没有图标更难看。字母头像永远渲染得出来。
 * 若后续要接真实图标，把 badge 里的 Text 换成 Kirigami.Icon 即可，
 * 图标名来自 AppCatalog 的 icon 角色（.desktop 的 Icon 字段）。
 */
Item {
    id: tile

    property string appName: ""
    property string accent: "#4C8BF5"

    signal clicked()

    Rectangle {
        id: card

        anchors.fill: parent
        anchors.margins: 8
        radius: 26
        color: "#FFFFFF"
        border.color: "#F2E1CD"
        border.width: 1

        scale: tap.pressed ? 0.96 : (hover.hovered ? 1.02 : 1.0)
        Behavior on scale {
            NumberAnimation { duration: 110; easing.type: Easing.OutCubic }
        }

        Rectangle {
            id: badge

            anchors.horizontalCenter: parent.horizontalCenter
            anchors.top: parent.top
            anchors.topMargin: 22
            width: Math.min(70, card.height * 0.34)
            height: width
            radius: width * 0.3
            color: tile.accent

            Text {
                anchors.centerIn: parent
                text: tile.appName.length > 0 ? tile.appName.charAt(0) : "★"
                color: "white"
                font.pixelSize: parent.width * 0.46
                font.weight: Font.Bold
            }
        }

        Text {
            anchors.top: badge.bottom
            anchors.topMargin: 14
            anchors.left: parent.left
            anchors.right: parent.right
            anchors.leftMargin: 10
            anchors.rightMargin: 10
            text: tile.appName
            horizontalAlignment: Text.AlignHCenter
            elide: Text.ElideRight
            maximumLineCount: 2
            wrapMode: Text.Wrap
            font.pixelSize: 17
            font.weight: Font.DemiBold
            color: "#4A3B32"
        }

        HoverHandler {
            id: hover
        }

        TapHandler {
            id: tap
            onTapped: tile.clicked()
        }
    }
}
