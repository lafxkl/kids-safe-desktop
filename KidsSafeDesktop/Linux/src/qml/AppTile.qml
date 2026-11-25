import QtQuick

/*
 * 应用磁贴——尺寸与 Windows 端 LauncherGridView 的 TileTemplate 逐项对齐：
 *
 *      ┌──────┐
 *      │ 色块 │  84×84，圆角 28（≈33%，squircle），纯色无边框无投影
 *      └──────┘  ↕ 6
 *      ╭────╮   名称胶囊：圆角 11、左右内边距 8、字号 12.5 加粗
 *      │名称│
 *      ╰────╯
 *
 * Windows 端外框宽 92 + 外边距 6/8 = 每格 104；这里把它折算成「格子宽度的比例」，
 * 因为 QML 的 GridView 只认 cellWidth，不像 FlexLayout 能给每个磁贴固定宽度。
 * 换算下来 84/104 ≈ 0.81。
 *
 * 图标优先用系统图标主题里的真实应用图标（image://theme/<Icon 字段>，
 * 由 C++ 的 ThemeIconProvider 渲染）。查不到就退回「名称首字」——
 * 儿童桌面跑在各种精简发行版上，图标主题经常残缺，空白图标比没有图标更难看。
 */
Item {
    id: tile

    property string appName: ""
    property string iconName: ""
    property color accent: "#4C8BF5"

    signal clicked()

    // 与 Windows 的比例换算：色块占格宽约 81%，上限 84px
    readonly property real badgeSize: Math.min(84, width * 0.81)

    scale: tap.pressed ? 0.94 : 1.0
    Behavior on scale {
        NumberAnimation { duration: 110; easing.type: Easing.OutCubic }
    }

    Column {
        anchors.centerIn: parent
        spacing: 6

        Rectangle {
            id: badge

            anchors.horizontalCenter: parent.horizontalCenter
            width: tile.badgeSize
            height: width
            radius: Math.round(width * 0.33)
            color: tile.accent

            Image {
                id: appIcon

                anchors.centerIn: parent
                // Windows 端 84 的色块里放 52 的图标，同一个比例
                width: Math.round(parent.width * 0.62)
                height: width
                sourceSize.width: Math.round(width * 2)
                sourceSize.height: Math.round(height * 2)
                fillMode: Image.PreserveAspectFit
                smooth: true
                asynchronous: true
                // 图标名可能含空格 / 中文，必须编码后再拼进 image:// URL
                source: tile.iconName.length > 0
                        ? "image://theme/" + encodeURIComponent(tile.iconName)
                        : ""
                visible: tile.iconName.length > 0 && status === Image.Ready
            }

            // 兜底：主题里没有这个图标，或者 .desktop 根本没写 Icon
            Text {
                anchors.centerIn: parent
                visible: !appIcon.visible
                text: tile.appName.length > 0 ? tile.appName.charAt(0) : "★"
                color: "white"
                font.pixelSize: badge.width * 0.44
                font.weight: Font.Bold
            }
        }

        Rectangle {
            anchors.horizontalCenter: parent.horizontalCenter
            height: 24
            // 左右各 8 的内边距，与 Windows 的 Border Padding="8,3" 一致
            width: Math.max(24, Math.min(Math.max(nameLabel.implicitWidth + 16, 34), tile.width - 8))
            radius: 11
            color: "#D9FFFFFF"

            Text {
                id: nameLabel

                anchors.centerIn: parent
                width: Math.min(implicitWidth, parent.width - 12)
                text: tile.appName
                font.pixelSize: 13
                font.weight: Font.Bold
                color: "#37474F"
                elide: Text.ElideRight
                horizontalAlignment: Text.AlignHCenter
            }
        }
    }

    TapHandler {
        id: tap
        onTapped: tile.clicked()
    }
}
