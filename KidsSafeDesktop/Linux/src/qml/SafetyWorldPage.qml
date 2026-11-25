import QtQuick
import QtQuick.Controls
import QtQuick.Layouts
import QtQuick.Window
import "SafetyData.js" as SafetyData

/*
 * 安全学堂：10 个主题的知识卡片。
 *
 * 内容来自 SafetyData.js（与 Windows / 鸿蒙同一份文案），完全离线，
 * 不依赖故事服务端——安全知识是「必须有」的东西，不能因为家里没搭服务器就没了。
 *
 * 布局上沿用前几页定下的两条规则：
 *   1. 窗口只在根 Item 取一次（page.navWin），handler 里不要直接用 Window.window；
 *   2. GridView 直接放在普通 Item 里，状态提示锚定这个 Item，不跨层级锚定 Layout 子项。
 */
Item {
    id: page

    readonly property var navWin: Window.window

    Backdrop {
        anchors.fill: parent
    }

    ColumnLayout {
        anchors.fill: parent
        anchors.margins: 28
        spacing: 16

        // ---- 顶栏 ----
        RowLayout {
            Layout.fillWidth: true
            spacing: 16

            Rectangle {
                implicitWidth: 56
                implicitHeight: 56
                radius: 28
                color: "#FFFFFF"
                border.color: "#F2E1CD"
                border.width: 1

                Text {
                    anchors.centerIn: parent
                    text: "‹"
                    font.pixelSize: 34
                    color: "#7A6759"
                }

                TapHandler {
                    onTapped: page.navWin.popPage()
                }
            }

            ColumnLayout {
                spacing: 0

                Text {
                    text: "安全学堂"
                    font.pixelSize: 30
                    font.weight: Font.Bold
                    color: "#4A3B32"
                }

                Text {
                    text: SafetyData.TOPICS.length + " 个主题 · " + SafetyData.tipCount() + " 条贴士"
                    font.pixelSize: 14
                    color: "#9B8574"
                }
            }

            Item {
                Layout.fillWidth: true
            }

            LargeButton {
                Layout.preferredWidth: 168
                Layout.preferredHeight: 56
                text: "安全闯关"
                accentColor: "#FF8A5C"
                onClicked: page.navWin.pushPage(Qt.resolvedUrl("SafetyQuizPage.qml"))
            }
        }

        // ---- 主题网格 ----
        Item {
            Layout.fillWidth: true
            Layout.fillHeight: true

            GridView {
                id: grid

                anchors.fill: parent
                clip: true

                // 列数随宽度自适应，单格不小于 210px；触屏上也好点
                cellWidth: Math.floor(width / Math.max(2, Math.min(4, Math.floor(width / 210))))

                // 高度按卡片的「固定内容」倒推：图标 52 + 标题 + 贴士数 + 底部
                // 「读一读」，再加上下内边距和卡片外边距，最少 182。
                // 之前按 cellWidth * 0.68 算，4 列时只有 174，内容比卡片高 ——
                // 「读一读」被挤出卡片下沿，视觉上像是掉到了卡外面。
                cellHeight: Math.max(182, Math.floor(cellWidth * 0.66))

                model: SafetyData.TOPICS

                delegate: Item {
                    width: grid.cellWidth
                    height: grid.cellHeight

                    Rectangle {
                        anchors.fill: parent
                        anchors.margins: 8
                        radius: 24
                        color: "#FFFFFF"
                        border.color: "#F2E1CD"
                        border.width: 1

                        // 左侧色条：用主题色，和角标底色、图标呼应
                        Rectangle {
                            anchors.left: parent.left
                            anchors.top: parent.top
                            anchors.bottom: parent.bottom
                            anchors.margins: 10
                            width: 6
                            radius: 3
                            color: modelData.color
                        }

                        // 图标角标：浅色圆角底 + 手绘彩色图标。
                        // 不用 emoji —— deepin 上没有彩色 emoji 字体，🚦 会变成黑线稿。
                        Rectangle {
                            id: badge

                            anchors.left: parent.left
                            anchors.top: parent.top
                            anchors.leftMargin: 28
                            anchors.topMargin: 16
                            width: 52
                            height: 52
                            radius: 17
                            color: Qt.lighter(modelData.color, 1.9)

                            GlyphIcon {
                                anchors.centerIn: parent
                                width: 40
                                height: 40
                                name: modelData.glyph
                            }
                        }

                        Text {
                            id: titleText

                            anchors.left: parent.left
                            anchors.right: parent.right
                            anchors.top: badge.bottom
                            anchors.leftMargin: 28
                            anchors.rightMargin: 18
                            anchors.topMargin: 8
                            text: modelData.title
                            font.pixelSize: 21
                            font.weight: Font.DemiBold
                            color: "#4A3B32"
                            elide: Text.ElideRight
                        }

                        Text {
                            anchors.left: parent.left
                            anchors.top: titleText.bottom
                            anchors.leftMargin: 28
                            anchors.topMargin: 2
                            text: modelData.tips.length + " 条贴士"
                            font.pixelSize: 14
                            color: "#B8A492"
                        }

                        // 「读一读」直接锚在卡片底部，不再参与弹性分配 ——
                        // 卡片矮一点时它只会离上面更近，不会被挤到卡片外面去。
                        Text {
                            anchors.left: parent.left
                            anchors.bottom: parent.bottom
                            anchors.leftMargin: 28
                            anchors.bottomMargin: 16
                            text: "读一读 ›"
                            font.pixelSize: 15
                            font.weight: Font.DemiBold
                            color: modelData.color
                        }

                        TapHandler {
                            onTapped: page.navWin.pushPage(Qt.resolvedUrl("SafetyTopicPage.qml"), {
                                "topicIndex": index
                            })
                        }
                    }
                }
            }
        }
    }
}
