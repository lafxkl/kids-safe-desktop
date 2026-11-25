import QtQuick
import QtQuick.Controls
import QtQuick.Layouts
import QtQuick.Window
import "SafetyData.js" as SafetyData

/*
 * 单个安全主题的贴士列表。
 *
 * 通过 pushPage 的第二个参数传 topicIndex（SafetyWorldPage 的 delegate 里拿到的
 * 是 index，不是对象本身——传下标比传对象安全，QML 里 JS 对象跨页面传递时
 * 有被引擎回收的可能）。
 */
Item {
    id: page

    readonly property var navWin: Window.window

    property int topicIndex: 0

    // 兜底：topicIndex 越界（比如以后主题增删）时退回第一个，不让页面变成空白
    readonly property var topic: (topicIndex >= 0 && topicIndex < SafetyData.TOPICS.length)
                                 ? SafetyData.TOPICS[topicIndex]
                                 : SafetyData.TOPICS[0]

    Backdrop {
        anchors.fill: parent
    }

    ColumnLayout {
        anchors.fill: parent
        anchors.margins: 28
        spacing: 16

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

            GlyphIcon {
                Layout.alignment: Qt.AlignVCenter
                Layout.preferredWidth: 44
                Layout.preferredHeight: 44
                name: page.topic.glyph
            }

            ColumnLayout {
                spacing: 0

                Text {
                    text: page.topic.title
                    font.pixelSize: 28
                    font.weight: Font.Bold
                    color: "#4A3B32"
                }

                Text {
                    text: page.topic.tips.length + " 条贴士，读给孩子听，或者让他自己看"
                    font.pixelSize: 14
                    color: "#9B8574"
                }
            }

            Item {
                Layout.fillWidth: true
            }

            LargeButton {
                Layout.preferredWidth: 152
                Layout.preferredHeight: 52
                text: "去闯关"
                accentColor: "#FF8A5C"
                onClicked: page.navWin.pushPage(Qt.resolvedUrl("SafetyQuizPage.qml"))
            }
        }

        // 列表与空状态同处一个普通 Item：跨层级 anchors.centerIn 在 deepin/Qt 6.8
        // 上不生效，这是前面几个页面反复踩过的坑。
        Item {
            Layout.fillWidth: true
            Layout.fillHeight: true

            ListView {
                id: tipList

                anchors.fill: parent
                clip: true
                spacing: 10
                model: page.topic.tips

                delegate: Rectangle {
                    width: tipList.width
                    height: Math.max(76, tipText.implicitHeight + 36)
                    radius: 20
                    color: "#FFFFFF"
                    border.color: "#F2E1CD"
                    border.width: 1

                    RowLayout {
                        anchors.fill: parent
                        anchors.margins: 18
                        spacing: 14

                        // 贴士前的彩色圆点：52 条贴士有几十个不同 emoji，没法逐个
                        // 手绘，而它们在 deepin 上全是黑色线稿。改成主题色圆点，
                        // 列表反而更整齐，也保住了「每条都看得见」的层次。
                        Rectangle {
                            Layout.alignment: Qt.AlignVCenter
                            Layout.preferredWidth: 10
                            Layout.preferredHeight: 10
                            radius: 5
                            color: page.topic.color
                        }

                        Text {
                            id: tipText

                            Layout.fillWidth: true
                            text: modelData.text
                            font.pixelSize: 18
                            color: "#4A3B32"
                            wrapMode: Text.Wrap
                            lineHeight: 1.4
                        }
                    }
                }
            }

            Text {
                anchors.centerIn: parent
                visible: page.topic.tips.length === 0
                text: "这个主题还没有贴士"
                font.pixelSize: 17
                color: "#B8A492"
            }
        }
    }
}
