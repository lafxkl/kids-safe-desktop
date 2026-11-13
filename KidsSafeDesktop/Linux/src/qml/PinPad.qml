import QtQuick
import QtQuick.Layouts

/*
 * 数字密码键盘。家长验证页与引导页共用。
 *
 * 用法：pad.clear() 清空；监听 completed(pin) 拿到满位密码。
 */
ColumnLayout {
    id: pad

    property int digitCount: 4
    property string value: ""
    property color dotColor: "#FF8A5C"

    signal completed(string pin)
    signal edited()

    function clear() {
        pad.value = "";
        pad.edited();
    }

    spacing: 26

    RowLayout {
        Layout.alignment: Qt.AlignHCenter
        spacing: 20

        Repeater {
            model: pad.digitCount

            Rectangle {
                // 在 RowLayout 里，尺寸由 implicit 尺寸决定；直接写 width/height
                // 会被布局忽略并压成 0，所以这里用 implicitWidth/implicitHeight。
                implicitWidth: 20
                implicitHeight: 20
                radius: 10
                color: index < pad.value.length ? pad.dotColor : "#E7D3BF"

                Behavior on color {
                    ColorAnimation { duration: 120 }
                }
            }
        }
    }

    GridLayout {
        Layout.alignment: Qt.AlignHCenter
        columns: 3
        rowSpacing: 12
        columnSpacing: 12

        Repeater {
            model: ["1", "2", "3", "4", "5", "6", "7", "8", "9", "del", "0", "ok"]

            Rectangle {
                id: key

                required property string modelData

                implicitWidth: 96
                implicitHeight: 84
                radius: 20
                color: (modelData === "del" || modelData === "ok") ? "#F5E5D3" : "#FFFFFF"
                border.color: "#EFDCC6"
                border.width: 1

                scale: tap.pressed ? 0.94 : 1.0
                Behavior on scale {
                    NumberAnimation { duration: 90; easing.type: Easing.OutQuad }
                }

                Text {
                    anchors.centerIn: parent
                    text: key.modelData === "del" ? "⌫" : key.modelData === "ok" ? "✓" : key.modelData
                    font.pixelSize: 30
                    font.weight: Font.DemiBold
                    color: "#4A3B32"
                }

                TapHandler {
                    id: tap

                    onTapped: {
                        const k = key.modelData;
                        if (k === "del") {
                            pad.value = pad.value.slice(0, -1);
                        } else if (k === "ok") {
                            if (pad.value.length === pad.digitCount) {
                                pad.completed(pad.value);
                            }
                            return;
                        } else if (pad.value.length < pad.digitCount) {
                            pad.value += k;
                        }
                        pad.edited();
                        if (pad.value.length === pad.digitCount) {
                            pad.completed(pad.value);
                        }
                    }
                }
            }
        }
    }
}
