import QtQuick
import QtQuick.Layouts

/*
 * 首页的「内置内容」入口卡：渐变底 + 左侧图标方块 + 标题/副标题 + 右侧引导语。
 *
 * 图标用 GlyphIcon 手绘，不用 emoji：deepin 上 📖 / 🛡️ 会被渲染成黑白线稿，
 * 和 Windows 端的彩色观感对不上（详见 GlyphIcon.qml 头注释）。
 *
 * 抽成组件是因为首页已经有三个入口（故事屋、安全学堂、游戏乐园），
 * 后面还要加媒体中心 / 学习天地。复制粘贴时最容易改漏 TapHandler 和圆角。
 */
Rectangle {
    id: banner

    property string glyph: "star"
    property string title: ""
    property string subtitle: ""
    property color colorFrom: "#8E7CFF"
    property color colorTo: "#B388FF"

    signal tapped()

    radius: 28

    gradient: Gradient {
        GradientStop { position: 0.0; color: banner.colorFrom }
        GradientStop { position: 1.0; color: banner.colorTo }
    }

    RowLayout {
        anchors.fill: parent
        anchors.margins: 20
        spacing: 16

        Rectangle {
            Layout.preferredWidth: 64
            Layout.preferredHeight: 64
            radius: 20
            color: "#33FFFFFF"

            GlyphIcon {
                anchors.centerIn: parent
                name: banner.glyph
                color: "white"
                width: 36
                height: 36
            }
        }

        ColumnLayout {
            Layout.fillWidth: true
            spacing: 5

            Text {
                Layout.fillWidth: true
                text: banner.title
                font.pixelSize: 23
                font.weight: Font.Bold
                color: "white"
                elide: Text.ElideRight
            }

            Text {
                Layout.fillWidth: true
                text: banner.subtitle
                font.pixelSize: 14
                color: "#F2ECFF"
                elide: Text.ElideRight
            }
        }

        Text {
            // 三列并排时卡片只有 200 出头，这句会把标题挤成省略号，所以窄了就收起来
            visible: banner.width > 380
            text: "进去看看 ›"
            font.pixelSize: 16
            color: "white"
        }
    }

    TapHandler {
        onTapped: banner.tapped()
    }
}
