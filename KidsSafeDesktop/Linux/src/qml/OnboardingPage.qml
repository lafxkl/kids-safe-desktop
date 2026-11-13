import QtQuick
import QtQuick.Controls
import QtQuick.Layouts
import KidsSafe 1.0

/*
 * 首次引导：起名字 → 设家长密码 → 挑允许的应用。
 * 三步走完才进桌面，等价于 C# 版的 OnboardingPage。
 */
Item {
    id: page

    property int step: 0
    property string firstPin: ""
    property string tipText: ""

    Backdrop {
        anchors.fill: parent
    }

    ColumnLayout {
        anchors.centerIn: parent
        width: Math.min(parent.width - 80, 720)
        spacing: 24

        // 步骤指示
        RowLayout {
            Layout.alignment: Qt.AlignHCenter
            spacing: 10

            Repeater {
                model: 3

                Rectangle {
                    implicitWidth: index === page.step ? 30 : 10
                    implicitHeight: 10
                    radius: 5
                    color: index === page.step ? "#FF8A5C" : "#E7D3BF"
                    Behavior on implicitWidth { NumberAnimation { duration: 160 } }
                }
            }
        }

        Rectangle {
            Layout.fillWidth: true
            Layout.preferredHeight: content.implicitHeight + 72
            radius: 32
            color: "#FFFFFF"
            border.color: "#F2E1CD"
            border.width: 1

            ColumnLayout {
                id: content

                // 只贴左/右/上，不贴底：高度交给 implicitHeight，
                // 否则会和外层 Rectangle 的 preferredHeight 形成绑定环。
                anchors.left: parent.left
                anchors.right: parent.right
                anchors.top: parent.top
                anchors.margins: 36
                spacing: 20

                // ---- 第 1 步：起名字 ----
                ColumnLayout {
                    visible: page.step === 0
                    Layout.fillWidth: true
                    spacing: 18

                    Text {
                        text: "欢迎来到你的小天地"
                        font.pixelSize: 30
                        font.weight: Font.Bold
                        color: "#4A3B32"
                        Layout.alignment: Qt.AlignHCenter
                    }

                    Text {
                        text: "先告诉电脑，你叫什么名字？"
                        font.pixelSize: 17
                        color: "#9B8574"
                        Layout.alignment: Qt.AlignHCenter
                    }

                    TextField {
                        id: nicknameField

                        Layout.alignment: Qt.AlignHCenter
                        Layout.preferredWidth: 320
                        Layout.preferredHeight: 64
                        horizontalAlignment: TextInput.AlignHCenter
                        placeholderText: "小朋友"
                        text: Settings.nickname === "小朋友" ? "" : Settings.nickname
                        font.pixelSize: 24
                        onAccepted: page.step = 1
                    }

                    LargeButton {
                        Layout.alignment: Qt.AlignHCenter
                        Layout.preferredWidth: 240
                        text: "下一步"
                        onClicked: {
                            const name = nicknameField.text.trim();
                            if (name.length > 0) {
                                Settings.nickname = name;
                            }
                            page.step = 1;
                        }
                    }
                }

                // ---- 第 2 步：设家长密码 ----
                ColumnLayout {
                    visible: page.step === 1
                    Layout.fillWidth: true
                    spacing: 16

                    Text {
                        Layout.alignment: Qt.AlignHCenter
                        text: page.firstPin === "" ? "设置家长密码" : "再输一次确认"
                        font.pixelSize: 30
                        font.weight: Font.Bold
                        color: "#4A3B32"
                    }

                    Text {
                        Layout.alignment: Qt.AlignHCenter
                        text: page.tipText.length > 0 ? page.tipText : "这个密码用来打开家长后台，别告诉小朋友哦"
                        font.pixelSize: 16
                        color: page.tipText.length > 0 ? "#D85A30" : "#9B8574"
                    }

                    PinPad {
                        id: setupPad

                        Layout.alignment: Qt.AlignHCenter
                        onCompleted: function (pin) {
                            if (page.firstPin === "") {
                                page.firstPin = pin;
                                page.tipText = "";
                                setupPad.clear();
                                return;
                            }
                            if (page.firstPin === pin) {
                                Settings.setPin(pin);
                                page.tipText = "";
                                page.step = 2;
                            } else {
                                page.firstPin = "";
                                page.tipText = "两次输入不一样，请重新设置";
                                setupPad.clear();
                            }
                        }
                        onEdited: page.tipText = ""
                    }
                }

                // ---- 第 3 步：挑应用 ----
                ColumnLayout {
                    visible: page.step === 2
                    Layout.fillWidth: true
                    spacing: 14

                    Text {
                        Layout.alignment: Qt.AlignHCenter
                        text: "哪些应用可以出现在桌面上？"
                        font.pixelSize: 28
                        font.weight: Font.Bold
                        color: "#4A3B32"
                    }

                    Text {
                        Layout.alignment: Qt.AlignHCenter
                        text: "现在不选也没关系，之后在家长后台随时能改"
                        font.pixelSize: 16
                        color: "#9B8574"
                    }

                    ListView {
                        id: picker

                        Layout.fillWidth: true
                        Layout.preferredHeight: 340
                        clip: true
                        spacing: 6
                        model: AppCatalog

                        delegate: Rectangle {
                            required property string appId
                            required property string name

                            width: picker.width
                            height: 62
                            radius: 16
                            color: Settings.isAllowed(appId) ? "#FFF1E2" : "#FBF8F4"
                            border.color: Settings.isAllowed(appId) ? "#FFC79A" : "transparent"

                            RowLayout {
                                anchors.fill: parent
                                anchors.leftMargin: 18
                                anchors.rightMargin: 12
                                spacing: 14

                                Rectangle {
                                    implicitWidth: 36
                                    implicitHeight: 36
                                    radius: 11
                                    color: "#4C8BF5"

                                    Text {
                                        anchors.centerIn: parent
                                        text: name.length > 0 ? name.charAt(0) : "★"
                                        color: "white"
                                        font.pixelSize: 18
                                        font.weight: Font.Bold
                                    }
                                }

                                Text {
                                    Layout.fillWidth: true
                                    text: name
                                    font.pixelSize: 18
                                    color: "#4A3B32"
                                    elide: Text.ElideRight
                                }

                                Switch {
                                    checked: Settings.isAllowed(appId)
                                    onToggled: {
                                        if (checked) {
                                            Settings.addApp(AppCatalog.get(index));
                                        } else {
                                            Settings.removeApp(appId);
                                        }
                                    }
                                }
                            }
                        }
                    }

                    LargeButton {
                        Layout.alignment: Qt.AlignHCenter
                        Layout.preferredWidth: 260
                        text: "开始使用"
                        onClicked: {
                            Settings.onboardingDone = true;
                            StackView.view.replace(null, Qt.resolvedUrl("HomePage.qml"));
                        }
                    }
                }
            }
        }
    }

    Component.onCompleted: AppCatalog.refresh()
}
