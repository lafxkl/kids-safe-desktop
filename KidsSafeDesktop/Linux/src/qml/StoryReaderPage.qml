import QtQuick
import QtQuick.Controls
import QtQuick.Layouts
import KidsSafe 1.0

Item {
    id: page

    property var storyId: 0
    property string storyTitle: ""

    Backdrop {
        anchors.fill: parent
    }

    ColumnLayout {
        anchors.fill: parent
        anchors.margins: 28
        spacing: 14

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
                    onTapped: {
                        Speech.stop();
                        StackView.view.pop();
                    }
                }
            }

            Text {
                Layout.fillWidth: true
                text: Stories.detail.title || page.storyTitle
                font.pixelSize: 24
                font.weight: Font.Bold
                color: "#4A3B32"
                elide: Text.ElideRight
            }

            LargeButton {
                Layout.preferredWidth: 150
                Layout.preferredHeight: 54
                text: "🔊 朗读"
                accentColor: "#FF8A5C"
                enabled: (Stories.detail.content || "").length > 0
                onClicked: Speech.say(Stories.detail.content)
            }

            LargeButton {
                Layout.preferredWidth: 120
                Layout.preferredHeight: 54
                text: "■ 停止"
                accentColor: "#EFE1D0"
                labelColor: "#7A6759"
                onClicked: Speech.stop()
            }
        }

        // ---- 正文 ----
        Flickable {
            id: flick

            Layout.fillWidth: true
            Layout.fillHeight: true
            clip: true
            contentWidth: width
            contentHeight: reader.implicitHeight + 60
            boundsBehavior: Flickable.StopAtBounds

            ScrollBar.vertical: ScrollBar {}

            ColumnLayout {
                id: reader

                width: Math.min(flick.width - 24, 820)
                x: Math.max(0, (flick.width - width) / 2)
                spacing: 16

                Text {
                    Layout.fillWidth: true
                    text: Stories.detail.title || page.storyTitle
                    font.pixelSize: 32
                    font.weight: Font.Bold
                    color: "#4A3B32"
                    wrapMode: Text.Wrap
                }

                Text {
                    Layout.fillWidth: true
                    text: [Stories.detail.type, Stories.detail.readTime].filter(function (s) {
                        return s && s.length > 0;
                    }).join(" · ")
                    font.pixelSize: 14
                    color: "#9B8574"
                    visible: text.length > 0
                }

                Rectangle {
                    Layout.fillWidth: true
                    Layout.preferredHeight: 1
                    color: "#F0DFCB"
                }

                Text {
                    Layout.fillWidth: true
                    // 正文按原样显示（服务端已带换行与段首缩进），
                    // 这里只负责行高与自动折行，别做任何 trim 或重排。
                    text: Stories.detail.content || ""
                    textFormat: Text.PlainText
                    wrapMode: Text.Wrap
                    font.pixelSize: 21
                    lineHeight: 1.75
                    lineHeightMode: Text.ProportionalHeight
                    color: "#3E332C"
                }

                Item {
                    Layout.preferredHeight: 40
                }
            }
        }
    }

    // 加载中
    BusyIndicator {
        anchors.centerIn: flick
        width: 54
        height: 54
        running: Stories.detailLoading
        visible: running
    }

    // 空 / 出错
    ColumnLayout {
        anchors.centerIn: flick
        spacing: 12
        visible: !Stories.detailLoading && (Stories.detail.content || "").length === 0

        Text {
            Layout.alignment: Qt.AlignHCenter
            text: "😴"
            font.pixelSize: 52
        }

        Text {
            Layout.alignment: Qt.AlignHCenter
            text: Stories.errorText.length > 0 ? Stories.errorText : "这个故事暂时打不开"
            font.pixelSize: 18
            color: "#B8A492"
        }
    }

    Component.onCompleted: {
        if (page.storyId) {
            Stories.loadDetail(page.storyId);
        }
    }
}
