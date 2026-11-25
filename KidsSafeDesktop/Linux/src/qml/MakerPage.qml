import QtQuick
import QtQuick.Controls
import QtQuick.Layouts
import QtQuick.Window
import QtWebEngine
import KidsSafe 1.0

/*
 * 小游戏工坊：内嵌一份 H5 编辑器，家长不写代码就能做出能玩的小游戏。
 *
 * 编辑器本体是四端共用的 Resources/Raw/Maker/index.html（Linux 装到
 * share/kidssafe/maker），Linux 端只加三样宿主才能做的事：
 *   1. 调系统文件选择器选图片 → Maker.jsForImage() 拼出调用串 → 执行；
 *   2. 读一个 .json 作品 → Maker.jsForProject() → 执行；
 *   3. 取回当前作品的 base64 → Maker.saveProjectBase64() 落盘。
 * 其余（模板、素材库、试玩、存档）全在网页里，改动不必四端同步。
 *
 * 文件选择器放在单独的 MakerFilePicker.qml，经 Loader 加载：
 * QtQuick.Dialogs 在部分精简发行版上是单独一个包（qml6-module-qtquick-dialogs），
 * 缺了它不该让整页白屏 —— 载入失败时退回「请自己把图片拖进来」的提示。
 */
Item {
    id: page

    readonly property var navWin: Window.window

    // ---- 提示条（导出/导入的结果） ----
    property string toastText: ""
    property bool toastBad: false

    function toast(text, bad) {
        page.toastText = text;
        page.toastBad = bad === true;
        toastTimer.restart();
    }

    Timer {
        id: toastTimer
        interval: 3600
        onTriggered: page.toastText = ""
    }

    Backdrop {
        anchors.fill: parent
    }

    // ---- 顶部工具条 ----
    RowLayout {
        id: header

        anchors.left: parent.left
        anchors.right: parent.right
        anchors.top: parent.top
        anchors.margins: 22
        spacing: 12

        Rectangle {
            Layout.preferredWidth: backLabel.implicitWidth + 34
            Layout.preferredHeight: 46
            radius: 23
            color: "#FFFFFF"
            border.color: "#F2E1CD"
            border.width: 1

            Text {
                id: backLabel

                anchors.centerIn: parent
                text: "‹ 返回"
                font.pixelSize: 17
                font.weight: Font.DemiBold
                color: "#7A6759"
            }

            TapHandler {
                onTapped: page.navWin.popPage()
            }
        }

        Rectangle {
            Layout.preferredWidth: 44
            Layout.preferredHeight: 44
            radius: 14
            color: "#7E57C2"

            GlyphIcon {
                anchors.centerIn: parent
                width: 24
                height: 24
                name: "laptop"
                color: "white"
            }
        }

        ColumnLayout {
            spacing: 0

            Text {
                text: "小游戏工坊"
                font.pixelSize: 24
                font.weight: Font.Bold
                color: "#4A3B32"
            }

            Text {
                text: Maker.available ? "选玩法、换素材，就能做出能玩的小游戏"
                                      : "当前版本没有网页组件，工坊打不开"
                font.pixelSize: 13
                color: "#B8A492"
            }
        }

        Item {
            Layout.fillWidth: true
        }

        Rectangle {
            Layout.preferredWidth: 44
            Layout.preferredHeight: 44
            radius: 22
            color: "#FFFFFF"
            border.color: "#F2E1CD"
            border.width: 1

            Canvas {
                anchors.centerIn: parent
                width: 22
                height: 22

                onPaint: {
                    const ctx = getContext("2d");
                    ctx.reset();
                    ctx.strokeStyle = "#7A6759";
                    ctx.lineWidth = 3;
                    ctx.lineCap = "round";
                    ctx.beginPath();
                    ctx.arc(11, 11, 7, -0.55 * Math.PI, 1.35 * Math.PI);
                    ctx.stroke();
                    ctx.beginPath();
                    ctx.moveTo(4.5, 3.5);
                    ctx.lineTo(4.0, 9.2);
                    ctx.lineTo(9.6, 8.4);
                    ctx.closePath();
                    ctx.fillStyle = "#7A6759";
                    ctx.fill();
                }
            }

            TapHandler {
                onTapped: makerView.reload()
            }
        }
    }

    // ---- 编辑器 ----
    Rectangle {
        id: webCard

        anchors.left: parent.left
        anchors.right: parent.right
        anchors.top: header.bottom
        anchors.bottom: actionBar.top
        anchors.leftMargin: 22
        anchors.rightMargin: 22
        anchors.topMargin: 14
        anchors.bottomMargin: 12
        radius: 20
        color: "#FFFFFF"
        border.color: "#F2E1CD"
        border.width: 1
        clip: true

        WebEngineView {
            id: makerView

            anchors.fill: parent
            anchors.margins: 2
            url: Maker.editorUrl
            focus: true

            // localContentCanAccessFileUrls：页面不跨目录取文件，但显式开着更稳；
            // localStorageEnabled：家长的作品就存在 localStorage 里，关了等于每次重做。
            settings.localContentCanAccessFileUrls: true
            settings.javascriptEnabled: true
            settings.localStorageEnabled: true
            settings.playbackRequiresUserGesture: false
            settings.fullScreenSupportEnabled: false
            // 右键菜单不额外关：kiosk 模式下窗口本来就没有菜单键，
            // 而 contextMenuRequested 这个信号在 6.6 以前的 Qt 上不存在，
            // 写上去会让整页 QML 加载失败 —— 不值得为它冒这个险。
        }

        // 组件齐全但资源没找到（打包漏了 assets/maker）时兜底
        Rectangle {
            anchors.fill: parent
            visible: !Maker.available
            color: "#FFF8EE"

            ColumnLayout {
                anchors.centerIn: parent
                width: Math.min(parent.width - 80, 560)
                spacing: 14

                Text {
                    Layout.alignment: Qt.AlignHCenter
                    text: Maker.webEngineBuilt ? "没找到工坊的资源文件"
                                               : "当前构建没有包含网页组件"
                    font.pixelSize: 22
                    font.weight: Font.Bold
                    color: "#4A3B32"
                }

                Text {
                    Layout.alignment: Qt.AlignHCenter
                    Layout.fillWidth: true
                    text: Maker.webEngineBuilt
                          ? "请确认安装包里带了 share/kidssafe/maker/index.html。"
                          : "需要 qt6-webengine 与 qml6-module-qtwebengine，装好后重开本程序。"
                    font.pixelSize: 14
                    color: "#9B8574"
                    wrapMode: Text.Wrap
                    horizontalAlignment: Text.AlignHCenter
                }
            }
        }

        // 加载进度
        Rectangle {
            anchors.fill: parent
            visible: Maker.available && makerView.loading
            color: "#FFF8EE"

            Column {
                anchors.centerIn: parent
                spacing: 14

                BusyIndicator {
                    anchors.horizontalCenter: parent.horizontalCenter
                    width: 54
                    height: 54
                    running: parent.visible
                }

                Text {
                    anchors.horizontalCenter: parent.horizontalCenter
                    text: "正在打开工坊…"
                    font.pixelSize: 17
                    color: "#7A6759"
                }
            }
        }
    }

    // ---- 底部动作条 ----
    RowLayout {
        id: actionBar

        anchors.left: parent.left
        anchors.right: parent.right
        anchors.bottom: parent.bottom
        anchors.margins: 22
        anchors.topMargin: 0
        spacing: 12

        LargeButton {
            Layout.preferredWidth: 190
            Layout.preferredHeight: 56
            text: "🖼 导入素材"
            accentColor: "#7E57C2"
            enabled: Maker.available
            onClicked: {
                if (pickerLoader.status === Loader.Ready) {
                    pickerLoader.item.requestImage();
                } else {
                    page.toast("系统文件选择器不可用，可以把图片直接拖进工坊", true);
                }
            }
        }

        LargeButton {
            Layout.preferredWidth: 190
            Layout.preferredHeight: 56
            text: "📥 打开作品"
            accentColor: "#EFE1D0"
            labelColor: "#7A6759"
            enabled: Maker.available
            onClicked: {
                if (pickerLoader.status === Loader.Ready) {
                    pickerLoader.item.requestProject();
                } else {
                    page.toast("系统文件选择器不可用", true);
                }
            }
        }

        LargeButton {
            Layout.preferredWidth: 190
            Layout.preferredHeight: 56
            text: "📤 导出作品"
            accentColor: "#EFE1D0"
            labelColor: "#7A6759"
            enabled: Maker.available
            onClicked: page.exportProject()
        }

        Item {
            Layout.fillWidth: true
        }

        Text {
            text: Maker.available ? ("导出目录：" + Maker.exportDir) : ""
            font.pixelSize: 12
            color: "#B8A492"
            elide: Text.ElideMiddle
            Layout.maximumWidth: 320
        }
    }

    // ---- 提示条 ----
    Rectangle {
        id: toastBar

        anchors.horizontalCenter: parent.horizontalCenter
        anchors.bottom: actionBar.top
        anchors.bottomMargin: 6
        width: Math.min(toastLabel.implicitWidth + 48, parent.width - 120)
        height: 46
        radius: 23
        visible: page.toastText !== ""
        color: page.toastBad ? "#E0544B" : "#4A3B32"
        opacity: visible ? 0.95 : 0

        Behavior on opacity { NumberAnimation { duration: 140 } }

        Text {
            id: toastLabel

            anchors.centerIn: parent
            width: parent.width - 40
            text: page.toastText
            font.pixelSize: 15
            color: "white"
            elide: Text.ElideRight
            horizontalAlignment: Text.AlignHCenter
        }
    }

    // ---- 文件选择器（可选依赖，缺了不影响其他功能） ----
    Loader {
        id: pickerLoader

        source: "MakerFilePicker.qml"

        onStatusChanged: {
            if (status === Loader.Error) {
                console.warn("[maker] 文件选择器不可用（缺 qml6-module-qtquick-dialogs？）");
            }
        }

        Connections {
            target: pickerLoader.item

            function onImagePicked(url) {
                const script = Maker.jsForImage(url);
                if (script === "") {
                    page.toast(Maker.lastError || "这张图读不出来", true);
                    return;
                }
                makerView.runJavaScript(script);
                page.toast("素材已导入");
            }

            function onProjectPicked(url) {
                const script = Maker.jsForProject(url);
                if (script === "") {
                    page.toast(Maker.lastError || "这个文件读不出来", true);
                    return;
                }
                makerView.runJavaScript(script, function (result) {
                    if (result === "ok") {
                        page.toast("作品已打开");
                    } else {
                        page.toast("这个文件不是作品：" + result, true);
                    }
                });
            }
        }
    }

    // ---- 导出 ----
    function exportProject() {
        // 一次取回「内容 + 名字」：runJavaScript 的返回值会把 JS 对象转成
        // QVariantMap，比在 QML 里解 base64 再 parse 稳（Qt.atob 出来的
        // 是 Latin-1 字符串，中文名会乱码）。
        makerView.runJavaScript(
            "(function(){var b=window.KS.currentProjectBase64();" +
            "return {b64:b, name:window.KS.projectName ? window.KS.projectName() : ''};})()",
            function (res) {
                if (!res || !res.b64) {
                    page.toast("先打开或新建一个作品再导出", true);
                    return;
                }
                const path = Maker.saveProjectBase64(res.b64, res.name || "我的小游戏");
                if (path === "") {
                    page.toast(Maker.lastError || "导出失败", true);
                } else {
                    page.toast("已导出到 " + path);
                }
            });
    }
}
