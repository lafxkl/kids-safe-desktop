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

    Backdrop {
        anchors.fill: parent
    }

    ColumnLayout {
        anchors.centerIn: parent
        spacing: 22

        GlyphIcon {
            Layout.alignment: Qt.AlignHCenter
            Layout.preferredWidth: 96
            Layout.preferredHeight: 96
            name: "moon"
            color: "#FFC46B"
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
            onClicked: page.navWin.pushPage(Qt.resolvedUrl("ParentGatePage.qml"), {
                "purpose": "extend"
            })
        }
    }
}
