import QtQuick
import QtQuick.Controls
import QtQuick.Layouts
import QtQuick.Window
import KidsSafe 1.0

Item {
    id: page

    // TapHandler 不是 Item，`Window.window` 附加属性对它返回 null——附加属性
    // 是靠「item → 所在窗口」反查的，handler 没有窗口归属。所以统一在根 Item
    // 上取一次窗口，页面里所有跳转都用这个属性；MouseArea、TapHandler、定时器
    // 里表现一致。（deepin / Qt 6.8 实测：MouseArea 里能用、TapHandler 里必 null）
    readonly property var navWin: Window.window

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
                        page.navWin.popPage();
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
                text: Speech.available ? "朗读" : "无语音"
                accentColor: "#FF8A5C"
                enabled: Speech.available && (Stories.detail.content || "").length > 0
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
        // 正文、加载指示、出错提示放在同一个普通 Item 里。Flickable 是
        // ColumnLayout 的子项，坐标由布局器给；跨层级 anchors.centerIn 在
        // 这台机器上不生效（StoryHousePage 的空状态踩过同一个坑）。
        Item {
            Layout.fillWidth: true
            Layout.fillHeight: true

            Flickable {
                id: flick

                // 父项是普通 Item（不是 Layout），这里必须 anchors.fill；
                // 写 Layout.fillWidth/fillHeight 在非 Layout 里是空操作，会变成 0 尺寸。
                anchors.fill: parent
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

            BusyIndicator {
                anchors.centerIn: parent
                width: 54
                height: 54
                running: Stories.detailLoading
                visible: running
            }

            // 空 / 出错
            ColumnLayout {
                anchors.centerIn: parent
                spacing: 12
                visible: !Stories.detailLoading && (Stories.detail.content || "").length === 0

                GlyphIcon {
                    Layout.alignment: Qt.AlignHCenter
                    Layout.preferredWidth: 58
                    Layout.preferredHeight: 58
                    name: "moon"
                    color: "#C0AC98"
                }

                Text {
                    Layout.alignment: Qt.AlignHCenter
                    text: Stories.errorText.length > 0 ? Stories.errorText : "这个故事暂时打不开"
                    font.pixelSize: 18
                    color: "#B8A492"
                }
            }
        }
    }

    Component.onCompleted: {
        if (page.storyId) {
            Stories.loadDetail(page.storyId);
        }
    }
}
