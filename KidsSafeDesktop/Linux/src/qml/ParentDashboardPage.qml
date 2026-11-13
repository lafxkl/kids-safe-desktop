import QtQuick
import QtQuick.Controls
import QtQuick.Layouts
import KidsSafe 1.0

/*
 * 家长后台。对应 C# 的 ParentDashboardPage，但只保留了 Linux MVP 需要的部分：
 * 时长、白名单、重置密码、退出。内容审核、排行榜、服务器同步等页面待后续移植。
 */
Item {
    id: page

    property bool confirmingExit: false
    property string serverHint: ""

    function minutesText(seconds) {
        const m = Math.floor(seconds / 60);
        return m > 0 ? (m + " 分钟") : (seconds + " 秒");
    }

    Backdrop {
        anchors.fill: parent
    }

    ColumnLayout {
        anchors.fill: parent
        anchors.margins: 32
        spacing: 18

        RowLayout {
            Layout.fillWidth: true
            spacing: 14

            Text {
                text: "家长后台"
                font.pixelSize: 30
                font.weight: Font.Bold
                color: "#4A3B32"
            }

            Item {
                Layout.fillWidth: true
            }

            LargeButton {
                Layout.preferredWidth: 140
                Layout.preferredHeight: 54
                text: "回到桌面"
                accentColor: "#FFFFFF"
                labelColor: "#7A6759"
                onClicked: StackView.view.pop(null)
            }
        }

        Flickable {
            Layout.fillWidth: true
            Layout.fillHeight: true
            clip: true
            contentHeight: body.implicitHeight + 8
            boundsBehavior: Flickable.StopAtBounds

            ColumnLayout {
                id: body

                width: parent.width
                spacing: 18

                // ---- 今日概览 ----
                Rectangle {
                    Layout.fillWidth: true
                    Layout.preferredHeight: 118
                    radius: 24
                    color: "#FFFFFF"
                    border.color: "#F2E1CD"
                    border.width: 1

                    RowLayout {
                        anchors.fill: parent
                        anchors.margins: 24
                        spacing: 40

                        ColumnLayout {
                            spacing: 4
                            Text { text: "今日已用"; font.pixelSize: 15; color: "#9B8574" }
                            Text {
                                text: page.minutesText(ScreenTime.usedSecondsToday)
                                font.pixelSize: 26
                                font.weight: Font.Bold
                                color: "#4A3B32"
                            }
                        }

                        ColumnLayout {
                            spacing: 4
                            Text { text: "每日上限"; font.pixelSize: 15; color: "#9B8574" }
                            Text {
                                text: Settings.dailyLimitMinutes + " 分钟"
                                font.pixelSize: 26
                                font.weight: Font.Bold
                                color: "#4A3B32"
                            }
                        }

                        ColumnLayout {
                            spacing: 4
                            Text { text: "剩余"; font.pixelSize: 15; color: "#9B8574" }
                            Text {
                                text: ScreenTime.remainingText
                                font.pixelSize: 26
                                font.weight: Font.Bold
                                color: ScreenTime.lockedOut ? "#C0392B" : "#1D9E75"
                            }
                        }

                        Item { Layout.fillWidth: true }
                    }
                }

                // ---- 快捷操作 ----
                Rectangle {
                    Layout.fillWidth: true
                    Layout.preferredHeight: 132
                    radius: 24
                    color: "#FFFFFF"
                    border.color: "#F2E1CD"
                    border.width: 1

                    ColumnLayout {
                        anchors.fill: parent
                        anchors.margins: 22
                        spacing: 12

                        Text {
                            text: "临时加时间"
                            font.pixelSize: 17
                            font.weight: Font.DemiBold
                            color: "#4A3B32"
                        }

                        RowLayout {
                            spacing: 12

                            LargeButton {
                                Layout.preferredWidth: 150
                                Layout.preferredHeight: 56
                                text: "+ 15 分钟"
                                accentColor: "#FF8A5C"
                                onClicked: ScreenTime.extend(15)
                            }

                            LargeButton {
                                Layout.preferredWidth: 150
                                Layout.preferredHeight: 56
                                text: "+ 30 分钟"
                                accentColor: "#FFB15C"
                                onClicked: ScreenTime.extend(30)
                            }

                            LargeButton {
                                Layout.preferredWidth: 150
                                Layout.preferredHeight: 56
                                text: "清零今日"
                                accentColor: "#EFE1D0"
                                labelColor: "#7A6759"
                                onClicked: ScreenTime.resetToday()
                            }

                            Item { Layout.fillWidth: true }
                        }
                    }
                }

                // ---- 每日时长 ----
                Rectangle {
                    Layout.fillWidth: true
                    Layout.preferredHeight: 132
                    radius: 24
                    color: "#FFFFFF"
                    border.color: "#F2E1CD"
                    border.width: 1

                    ColumnLayout {
                        anchors.fill: parent
                        anchors.margins: 22
                        spacing: 8

                        Text {
                            text: "每天可以玩 " + Settings.dailyLimitMinutes + " 分钟"
                            font.pixelSize: 17
                            font.weight: Font.DemiBold
                            color: "#4A3B32"
                        }

                        Slider {
                            Layout.fillWidth: true
                            from: 15
                            to: 180
                            stepSize: 15
                            snapMode: Slider.SnapAlways
                            value: Settings.dailyLimitMinutes
                            onMoved: Settings.dailyLimitMinutes = value
                        }

                        Text {
                            text: "从明天起按新时长计算（今天的余额不会被扣减）"
                            font.pixelSize: 13
                            color: "#B8A492"
                        }
                    }
                }

                // ---- 应用白名单 ----
                Rectangle {
                    Layout.fillWidth: true
                    Layout.preferredHeight: 440
                    radius: 24
                    color: "#FFFFFF"
                    border.color: "#F2E1CD"
                    border.width: 1

                    ColumnLayout {
                        anchors.fill: parent
                        anchors.margins: 22
                        spacing: 10

                        Text {
                            text: "桌面上的应用"
                            font.pixelSize: 17
                            font.weight: Font.DemiBold
                            color: "#4A3B32"
                        }

                        ListView {
                            id: allowList

                            Layout.fillWidth: true
                            Layout.fillHeight: true
                            clip: true
                            spacing: 4
                            model: AppCatalog

                            delegate: Rectangle {
                                required property string appId
                                required property string name

                                width: allowList.width
                                height: 54
                                radius: 14
                                color: Settings.isAllowed(appId) ? "#FFF3E6" : "transparent"

                                RowLayout {
                                    anchors.fill: parent
                                    anchors.leftMargin: 14
                                    anchors.rightMargin: 6
                                    spacing: 12

                                    Text {
                                        Layout.fillWidth: true
                                        text: name
                                        font.pixelSize: 17
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
                    }
                }

                // ---- 在线内容（故事屋 / 补充教材的来源）----
                Rectangle {
                    Layout.fillWidth: true
                    Layout.preferredHeight: 214
                    radius: 24
                    color: "#FFFFFF"
                    border.color: "#F2E1CD"
                    border.width: 1

                    ColumnLayout {
                        anchors.fill: parent
                        anchors.margins: 22
                        spacing: 10

                        RowLayout {
                            Layout.fillWidth: true
                            spacing: 12

                            ColumnLayout {
                                spacing: 2
                                Text {
                                    text: "在线故事与内容"
                                    font.pixelSize: 17
                                    font.weight: Font.DemiBold
                                    color: "#4A3B32"
                                }
                                Text {
                                    text: "内容来自你自己搭的 KidsSafeServer，不经任何第三方"
                                    font.pixelSize: 13
                                    color: "#B8A492"
                                }
                            }

                            Item { Layout.fillWidth: true }

                            Switch {
                                checked: Settings.onlineContentEnabled
                                onToggled: Settings.onlineContentEnabled = checked
                            }
                        }

                        RowLayout {
                            Layout.fillWidth: true
                            spacing: 12

                            TextField {
                                id: serverField

                                Layout.fillWidth: true
                                Layout.preferredHeight: 52
                                placeholderText: "http://192.168.1.10:8080"
                                text: Settings.serverUrl
                                font.pixelSize: 16
                                // 只在失焦或回车时写回，避免每敲一个字符就落一次盘
                                onEditingFinished: Settings.serverUrl = text
                            }

                            LargeButton {
                                Layout.preferredWidth: 120
                                Layout.preferredHeight: 52
                                text: "保存"
                                accentColor: "#EFE1D0"
                                labelColor: "#7A6759"
                                onClicked: {
                                    Settings.serverUrl = serverField.text;
                                    Stories.refreshOnlineState();
                                    page.serverHint = Stories.onlineEnabled
                                        ? "已启用，回桌面点「故事屋」试试"
                                        : "地址为空或在线内容未开启";
                                    hintTimer.restart();
                                }
                            }
                        }

                        Text {
                            Layout.fillWidth: true
                            text: page.serverHint.length > 0
                                ? page.serverHint
                                : "留空即完全离线，故事屋会显示「未开启」"
                            font.pixelSize: 13
                            color: page.serverHint.indexOf("已启用") === 0 ? "#1D9E75" : "#B8A492"
                            wrapMode: Text.Wrap
                        }
                    }
                }

                // ---- 退出 ----
                Rectangle {
                    Layout.fillWidth: true
                    Layout.preferredHeight: 100
                    radius: 24
                    color: "#FFFFFF"
                    border.color: "#F2E1CD"
                    border.width: 1

                    RowLayout {
                        anchors.fill: parent
                        anchors.margins: 22
                        spacing: 16

                        ColumnLayout {
                            spacing: 2
                            Text {
                                text: page.confirmingExit ? "再点一次就退出并结束本次儿童会话" : "退出儿童桌面"
                                font.pixelSize: 17
                                font.weight: Font.DemiBold
                                color: page.confirmingExit ? "#C0392B" : "#4A3B32"
                            }
                            Text {
                                text: "退出后回到登录界面，下次登录重新进入儿童模式"
                                font.pixelSize: 13
                                color: "#B8A492"
                            }
                        }

                        Item { Layout.fillWidth: true }

                        LargeButton {
                            Layout.preferredWidth: 160
                            Layout.preferredHeight: 56
                            text: page.confirmingExit ? "确认退出" : "退出"
                            accentColor: page.confirmingExit ? "#E0544B" : "#EFE1D0"
                            labelColor: page.confirmingExit ? "white" : "#7A6759"
                            onClicked: {
                                if (page.confirmingExit) {
                                    Kiosk.quit();
                                } else {
                                    page.confirmingExit = true;
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    Timer {
        id: hintTimer
        interval: 4000
        onTriggered: page.serverHint = ""
    }

    Component.onCompleted: {
        AppCatalog.refresh();
        page.confirmingExit = false;
    }
}
