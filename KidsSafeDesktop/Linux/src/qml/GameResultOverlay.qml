import QtQuick
import QtQuick.Layouts

/*
 * 结束时的结算面板，几个小游戏共用。
 *
 * 刻意不做「失败」判定：这类游戏是给 3~8 岁孩子的，答错只提示重来，
 * 不扣分、不倒数、不弹红叉。分数只统计答对的数量，最后一起看。
 */
Rectangle {
    id: overlay

    property int score: 0
    property int total: 0

    // 不按「答对几 / 共几」计分的游戏（打地鼠是计时的）自己传一句文案
    property string summary: ""

    // 主按钮的文案。联机对局结束时「再来一局」是做不到的（对面可能已经走了），
    // 那时候传「离开房间」，按下去就是退房回大厅。
    property string replayText: "再来一局"

    signal replay()
    signal exit()

    radius: 28
    color: "#F7FFFDF5"
    border.color: "#F2E1CD"
    border.width: 1

    ColumnLayout {
        anchors.centerIn: parent
        spacing: 16

        GlyphIcon {
            Layout.alignment: Qt.AlignHCenter
            width: 74
            height: 74
            name: "trophy"
            color: "#F2A03D"
        }

        Text {
            Layout.alignment: Qt.AlignHCenter
            text: overlay.summary === "" && overlay.score >= overlay.total
                  ? "全部答对，太棒啦！"
                  : "这一局结束啦"
            font.pixelSize: 28
            font.weight: Font.Bold
            color: "#4A3B32"
        }

        Rectangle {
            Layout.alignment: Qt.AlignHCenter
            Layout.preferredHeight: 52
            Layout.preferredWidth: scoreText.implicitWidth + 56
            radius: 26
            color: "#FFE0B2"

            Text {
                id: scoreText

                anchors.centerIn: parent
                text: overlay.summary !== ""
                      ? overlay.summary
                      : ("答对 " + overlay.score + " / " + overlay.total)
                font.pixelSize: 20
                font.weight: Font.DemiBold
                color: "#E65100"
            }
        }

        RowLayout {
            Layout.alignment: Qt.AlignHCenter
            spacing: 14

            Rectangle {
                Layout.preferredWidth: 150
                Layout.preferredHeight: 56
                radius: 28
                color: "#7E57C2"

                Text {
                    anchors.centerIn: parent
                    text: overlay.replayText
                    font.pixelSize: 19
                    font.weight: Font.Bold
                    color: "white"
                }

                TapHandler {
                    onTapped: overlay.replay()
                }
            }

            Rectangle {
                Layout.preferredWidth: 150
                Layout.preferredHeight: 56
                radius: 28
                color: "#FFFFFF"
                border.color: "#F2E1CD"
                border.width: 1

                Text {
                    anchors.centerIn: parent
                    text: "回游戏乐园"
                    font.pixelSize: 19
                    font.weight: Font.DemiBold
                    color: "#7A6759"
                }

                TapHandler {
                    onTapped: overlay.exit()
                }
            }
        }
    }
}
