import QtQuick
import QtQuick.Controls

/*
 * 大按钮：儿童界面的主要交互元素，最小高度 72px（远大于桌面常规 32px），
 * 因为小朋友的手指精度和鼠标点击习惯都不一样，宁可大一点。
 */
AbstractButton {
    id: control

    property color accentColor: "#FF8A5C"
    property color labelColor: "white"

    implicitWidth: 220
    implicitHeight: 72
    padding: 0

    scale: control.pressed ? 0.97 : 1.0
    Behavior on scale {
        NumberAnimation { duration: 90; easing.type: Easing.OutQuad }
    }

    background: Rectangle {
        radius: 22
        color: control.accentColor
        opacity: control.enabled ? (control.hovered ? 0.9 : 1.0) : 0.4
        Behavior on opacity { NumberAnimation { duration: 120 } }
    }

    contentItem: Text {
        text: control.text
        color: control.labelColor
        font.pixelSize: 22
        font.weight: Font.DemiBold
        horizontalAlignment: Text.AlignHCenter
        verticalAlignment: Text.AlignVCenter
        elide: Text.ElideRight
    }
}
