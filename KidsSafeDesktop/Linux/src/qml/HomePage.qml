import QtQuick
import QtQuick.Controls
import QtQuick.Layouts
import KidsSafe 1.0

Item {
    id: page

    function openApp(app) {
        if (!app) {
            return;
        }
        // exec 来自 .desktop 的 Exec 字段，也可能是家长手填的 http 链接，
        // 两种情况都由 AppLauncher 统一处理。
        AppLauncher.launch(app.exec || "", app.name || "");
    }

    Backdrop {
        anchors.fill: parent
    }

    ColumnLayout {
        anchors.fill: parent
        anchors.margins: 34
        spacing: 18

        // ---- 顶部：头像 / 昵称 / 剩余时间 / 家长入口 ----
        RowLayout {
            Layout.fillWidth: true
            spacing: 16

            Rectangle {
                Layout.preferredWidth: 68
                Layout.preferredHeight: 68
                radius: 34
                color: "#FFE0C2"

                Text {
                    anchors.centerIn: parent
                    text: Settings.avatarEmoji
                    font.pixelSize: 34
                }
            }

            ColumnLayout {
                spacing: 2

                Text {
                    text: "你好，" + Settings.nickname
                    font.pixelSize: 26
                    font.weight: Font.Bold
                    color: "#4A3B32"
                }

                Text {
                    text: "今天也要开开心心"
                    font.pixelSize: 15
                    color: "#9B8574"
                }
            }

            Item {
                Layout.fillWidth: true
            }

            Rectangle {
                Layout.preferredHeight: 58
                Layout.preferredWidth: timeRow.implicitWidth + 44
                radius: 29
                color: ScreenTime.lockedOut ? "#FFD9D2" : "#FFFFFF"
                border.color: "#F2E1CD"
                border.width: 1

                RowLayout {
                    id: timeRow

                    anchors.centerIn: parent
                    spacing: 10

                    Text {
                        text: "⏳"
                        font.pixelSize: 22
                    }

                    Text {
                        text: ScreenTime.lockedOut ? "今天的时间用完啦" : ("还可以玩 " + ScreenTime.remainingText)
                        font.pixelSize: 19
                        font.weight: Font.DemiBold
                        color: ScreenTime.lockedOut ? "#C0392B" : "#4A3B32"
                    }
                }
            }

            LargeButton {
                Layout.preferredWidth: 116
                Layout.preferredHeight: 58
                text: "家长"
                accentColor: "#FFFFFF"
                labelColor: "#7A6759"
                onClicked: StackView.view.push(Qt.resolvedUrl("ParentGatePage.qml"), {
                    "purpose": "dashboard"
                })
            }
        }

        // ---- 内置内容入口：目前只有故事屋，后续学习世界/游戏乐园往这里加 ----
        Rectangle {
            id: storyBanner

            Layout.fillWidth: true
            Layout.preferredHeight: 124
            radius: 28

            gradient: Gradient {
                GradientStop { position: 0.0; color: "#8E7CFF" }
                GradientStop { position: 1.0; color: "#B388FF" }
            }

            RowLayout {
                anchors.fill: parent
                anchors.margins: 22
                spacing: 18

                Rectangle {
                    Layout.preferredWidth: 76
                    Layout.preferredHeight: 76
                    radius: 24
                    color: "#33FFFFFF"

                    Text {
                        anchors.centerIn: parent
                        text: "📖"
                        font.pixelSize: 38
                    }
                }

                ColumnLayout {
                    spacing: 6

                    Text {
                        text: "故事屋"
                        font.pixelSize: 26
                        font.weight: Font.Bold
                        color: "white"
                    }

                    Text {
                        text: "睡前故事、格林童话、成语典故…"
                        font.pixelSize: 15
                        color: "#EFE9FF"
                    }
                }

                Item {
                    Layout.fillWidth: true
                }

                Text {
                    text: "进去看看 ›"
                    font.pixelSize: 17
                    color: "white"
                }
            }

            TapHandler {
                onTapped: StackView.view.push(Qt.resolvedUrl("StoryHousePage.qml"))
            }
        }

        Text {
            Layout.leftMargin: 6
            text: "我的应用"
            font.pixelSize: 18
            font.weight: Font.DemiBold
            color: "#9B8574"
        }

        // ---- 应用磁贴网格 ----
        GridView {
            id: appGrid

            Layout.fillWidth: true
            Layout.fillHeight: true
            clip: true

            // 列数随窗口宽度自适应，单格不小于 190px，保证触屏也好点
            cellWidth: Math.floor(width / Math.max(2, Math.min(5, Math.floor(width / 190))))
            cellHeight: Math.floor(cellWidth * 0.84)

            model: Settings.allowedApps

            delegate: Item {
                width: appGrid.cellWidth
                height: appGrid.cellHeight

                AppTile {
                    anchors.fill: parent
                    appName: modelData.name || ""
                    accent: modelData.accent || "#4C8BF5"
                    onClicked: page.openApp(modelData)
                }
            }
        }
    }

    ColumnLayout {
        anchors.centerIn: appGrid
        spacing: 14
        visible: Settings.allowedApps.length === 0

        Text {
            Layout.alignment: Qt.AlignHCenter
            text: "这里还空空的"
            font.pixelSize: 26
            font.weight: Font.Bold
            color: "#B8A492"
        }

        Text {
            Layout.alignment: Qt.AlignHCenter
            text: "请家长点右上角「家长」，进后台挑几个应用吧"
            font.pixelSize: 17
            color: "#B8A492"
        }
    }

    // ---- 打开外部应用时的遮罩 ----
    Rectangle {
        id: launchingOverlay

        anchors.fill: parent
        z: 10
        visible: AppLauncher.running
        color: "#EEFFF8EE"

        ColumnLayout {
            anchors.centerIn: parent
            spacing: 20

            BusyIndicator {
                Layout.alignment: Qt.AlignHCenter
                Layout.preferredWidth: 58
                Layout.preferredHeight: 58
                running: launchingOverlay.visible
            }

            Text {
                Layout.alignment: Qt.AlignHCenter
                text: "正在打开 " + AppLauncher.runningName
                font.pixelSize: 24
                font.weight: Font.DemiBold
                color: "#4A3B32"
            }

            Text {
                Layout.alignment: Qt.AlignHCenter
                text: "玩完记得回来哦"
                font.pixelSize: 16
                color: "#9B8574"
            }
        }
    }

    // ---- 轻提示 ----
    Rectangle {
        id: toast

        anchors.horizontalCenter: parent.horizontalCenter
        anchors.bottom: parent.bottom
        anchors.bottomMargin: 50
        z: 20
        width: toastText.implicitWidth + 48
        height: 56
        radius: 28
        color: "#4A3B32"
        opacity: 0
        visible: opacity > 0

        Text {
            id: toastText

            anchors.centerIn: parent
            color: "white"
            font.pixelSize: 16
        }

        Behavior on opacity {
            NumberAnimation { duration: 180 }
        }
    }

    Timer {
        id: toastTimer
        interval: 2600
        onTriggered: toast.opacity = 0
    }

    function showToast(message) {
        toastText.text = message;
        toast.opacity = 1;
        toastTimer.restart();
    }

    Connections {
        target: AppLauncher

        function onRunningChanged() {
            // 外部应用占用屏幕时暂停计时——时间应该算在那个应用上，
            // 但总账是同一本，所以这里只是暂停桌面侧的计时段。
            if (AppLauncher.running) {
                ScreenTime.pause();
            } else {
                Kiosk.toFront();
                ScreenTime.start();
            }
        }

        function onLaunchFailed(reason) {
            page.showToast(reason);
        }
    }

    Connections {
        target: ScreenTime

        function onLimitReached() {
            StackView.view.push(Qt.resolvedUrl("LockoutPage.qml"));
        }
    }

    Component.onCompleted: ScreenTime.start()
}
