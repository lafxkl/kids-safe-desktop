import QtQuick
import QtQuick.Controls
import QtQuick.Layouts
import KidsSafe 1.0

/*
 * 家长验证页。所有会突破儿童限制的操作都必须先过这一关。
 *
 * purpose 由调用方通过 push(url, {purpose: "..."}) 传入：
 *   "dashboard" — 进家长后台
 *   "extend"    — 只解锁 15 分钟，解完直接回桌面
 */
Item {
    id: page

    property string purpose: "dashboard"
    property string errorText: ""

    function backToHome() {
        StackView.view.pop(null);
    }

    Backdrop {
        anchors.fill: parent
    }

    ColumnLayout {
        id: box

        anchors.centerIn: parent
        width: 380
        spacing: 18

        transform: Translate {
            id: shakeOffset
        }

        Text {
            Layout.alignment: Qt.AlignHCenter
            text: "家长验证"
            font.pixelSize: 32
            font.weight: Font.Bold
            color: "#4A3B32"
        }

        Text {
            Layout.alignment: Qt.AlignHCenter
            text: page.errorText.length > 0 ? page.errorText : (Settings.hasPin ? "请输入 4 位家长密码" : "还没有设置密码，点击下方直接进入")
            font.pixelSize: 16
            color: page.errorText.length > 0 ? "#C0392B" : "#9B8574"
        }

        PinPad {
            id: pad

            Layout.alignment: Qt.AlignHCenter

            onEdited: page.errorText = ""

            onCompleted: function (pin) {
                if (!Settings.hasPin || Settings.verifyPin(pin)) {
                    page.grant();
                    return;
                }
                page.errorText = "密码不对，再试一次";
                shakeAnim.restart();
                pad.clear();
            }
        }

        LargeButton {
            Layout.alignment: Qt.AlignHCenter
            Layout.preferredWidth: 180
            Layout.preferredHeight: 58
            text: "返回"
            accentColor: "#EFE1D0"
            labelColor: "#7A6759"
            onClicked: StackView.view.pop()
        }
    }

    SequentialAnimation {
        id: shakeAnim

        NumberAnimation { target: shakeOffset; property: "x"; to: -14; duration: 55 }
        NumberAnimation { target: shakeOffset; property: "x"; to: 12; duration: 55 }
        NumberAnimation { target: shakeOffset; property: "x"; to: -8; duration: 55 }
        NumberAnimation { target: shakeOffset; property: "x"; to: 0; duration: 55 }
    }

    function grant() {
        if (page.purpose === "extend") {
            ScreenTime.extend(15);
            page.backToHome();
            return;
        }

        pad.clear();
        StackView.view.push(Qt.resolvedUrl("ParentDashboardPage.qml"));
    }
}
