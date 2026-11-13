import QtQuick
import QtQuick.Controls
import QtQuick.Layouts
import KidsSafe 1.0

Item {
    id: page

    Backdrop {
        anchors.fill: parent
    }

    ColumnLayout {
        anchors.centerIn: parent
        spacing: 22

        Text {
            Layout.alignment: Qt.AlignHCenter
            text: "🌙"
            font.pixelSize: 96
        }

        Text {
            Layout.alignment: Qt.AlignHCenter
            text: "今天的时间用完啦"
            font.pixelSize: 38
            font.weight: Font.Bold
            color: "#4A3B32"
        }

        Text {
            Layout.alignment: Qt.AlignHCenter
            text: "眼睛也该休息一下～明天再来玩吧"
            font.pixelSize: 19
            color: "#9B8574"
        }

        Item {
            Layout.preferredHeight: 12
        }

        // 家长延长 15 分钟。这条路必须过密码，否则锁屏形同虚设。
        LargeButton {
            Layout.alignment: Qt.AlignHCenter
            Layout.preferredWidth: 280
            text: "家长解锁 15 分钟"
            accentColor: "#B388FF"
            onClicked: StackView.view.push(Qt.resolvedUrl("ParentGatePage.qml"), {
                "purpose": "extend"
            })
        }
    }
}
