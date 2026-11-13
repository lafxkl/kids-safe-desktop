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
        spacing: 20

        Rectangle {
            id: logo

            Layout.alignment: Qt.AlignHCenter
            implicitWidth: 136
            implicitHeight: 136
            radius: 42

            gradient: Gradient {
                GradientStop { position: 0.0; color: "#B388FF" }
                GradientStop { position: 1.0; color: "#7C5CE0" }
            }

            Text {
                anchors.centerIn: parent
                text: "🚀"
                font.pixelSize: 64
            }

            SequentialAnimation on scale {
                running: true
                loops: Animation.Infinite
                NumberAnimation { to: 1.06; duration: 900; easing.type: Easing.InOutQuad }
                NumberAnimation { to: 1.00; duration: 900; easing.type: Easing.InOutQuad }
            }
        }

        Text {
            Layout.alignment: Qt.AlignHCenter
            text: "儿童安全桌面"
            font.pixelSize: 42
            font.weight: Font.Bold
            color: "#4A3B32"
        }

        Text {
            Layout.alignment: Qt.AlignHCenter
            text: "给小朋友的专属空间"
            font.pixelSize: 19
            color: "#9B8574"
        }
    }

    BusyIndicator {
        anchors.horizontalCenter: parent.horizontalCenter
        anchors.bottom: parent.bottom
        anchors.bottomMargin: 72
        width: 44
        height: 44
        running: true
    }

    Timer {
        interval: 1400
        running: true
        onTriggered: {
            if (!Settings.onboardingDone) {
                StackView.view.replace(null, Qt.resolvedUrl("OnboardingPage.qml"));
            } else if (ScreenTime.lockedOut) {
                StackView.view.replace(null, Qt.resolvedUrl("LockoutPage.qml"));
            } else {
                StackView.view.replace(null, Qt.resolvedUrl("HomePage.qml"));
            }
        }
    }
}
