import QtQuick
import QtQuick.Controls
import QtQuick.Layouts
import QtQuick.Window
import QtWebEngine
import KidsSafe 1.0

/*
 * 网页小游戏宿主页。
 *
 * Windows / Android 端把 15 款 HTML5 游戏塞进 WebView，Linux 端用
 * QtWebEngine 做同一件事：资源、玩法、美术与三端完全一致，不用逐款重写。
 * 大厅就是 Playland 自带的 index.html（15 张卡片的那一屏），点进去的每一款
 * 页面自己带着「返回 / 大厅 / 刷新」条，所以这里只需要一层外壳。
 *
 * 三条必须守住的设置：
 *   1. localContentCanAccessFileUrls —— 页面用相对路径引用 `pages/assets/mobile.css`
 *      和图片，默认虽然是开的，但显式写死，免得以后有人改了全局默认值就白屏；
 *   2. playbackRequiresUserGesture = false —— 好几款游戏进场就有音效/BGM，
 *      桌面端没有「用户手势」这一说，不关掉会被 Chromium 静音；
 *   3. 右键盘菜单直接吃掉 —— 全屏童锁里弹出「查看源代码 / 另存为」不合适。
 *
 * 另外两件事交给 QML 做：
 *   - 加载中盖一层进度；加载失败给「重试」，而不是留一片白；
 *   - alert/confirm/prompt 弹窗（游戏里偶尔用）转成自绘对话框，
 *     否则 Chromium 会默认拒绝，孩子点了没反应。
 */
Item {
    id: page

    // TapHandler 里 Window.window 为 null，统一在根 Item 取一次（见 Main.qml 注释）
    readonly property var navWin: Window.window

    property url startUrl: Playland.hallUrl
    property string title: "网页游戏乐园"

    // 失败态：WebEngine 起不来（缺 GPU / 进程被杀）时给出可读的原因
    property bool failed: false
    property string failureText: ""

    // JS 弹窗请求句柄与内容
    property var dialogRequest: null
    property string dialogMessage: ""
    property string dialogDefaultText: ""

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
            color: "#5AC8E8"

            GlyphIcon {
                anchors.centerIn: parent
                width: 25
                height: 25
                name: "gamepad"
                color: "white"
            }
        }

        Text {
            text: page.title
            font.pixelSize: 24
            font.weight: Font.Bold
            color: "#4A3B32"
        }

        Item {
            Layout.fillWidth: true
        }

        // 大厅：网页里点进某一款之后，靠它一键回到 15 张卡片那一屏
        Rectangle {
            Layout.preferredWidth: hallLabel.implicitWidth + 40
            Layout.preferredHeight: 44
            radius: 22
            color: "#FFFFFF"
            border.color: "#F2E1CD"
            border.width: 1
            opacity: gameView.url.toString() === Playland.hallUrl.toString() ? 0.55 : 1.0

            Text {
                id: hallLabel

                anchors.centerIn: parent
                text: "大厅"
                font.pixelSize: 15
                color: "#7A6759"
            }

            TapHandler {
                onTapped: gameView.url = Playland.hallUrl
            }
        }

        Rectangle {
            Layout.preferredWidth: 44
            Layout.preferredHeight: 44
            radius: 22
            color: "#FFFFFF"
            border.color: "#F2E1CD"
            border.width: 1

            // 刷新图标：一段圆弧加箭头，比 emoji ↻ 在各字体下都稳定
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
                    // 箭头
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
                onTapped: {
                    page.failed = false;
                    gameView.reload();
                }
            }
        }
    }

    // ---- 网页容器 ----
    Rectangle {
        id: webCard

        anchors.left: parent.left
        anchors.right: parent.right
        anchors.top: header.bottom
        anchors.bottom: parent.bottom
        anchors.leftMargin: 22
        anchors.rightMargin: 22
        anchors.topMargin: 14
        anchors.bottomMargin: 18
        radius: 20
        color: "#FFFFFF"
        border.color: "#F2E1CD"
        border.width: 1
        clip: true

        WebEngineView {
            id: gameView

            anchors.fill: parent
            anchors.margins: 2
            url: page.startUrl
            focus: true

            // 只开本地文件，不开远程：这 15 款游戏全部离线，页面里没有任何外链
            settings.localContentCanAccessFileUrls: true
            settings.javascriptEnabled: true
            settings.localStorageEnabled: true
            settings.playbackRequiresUserGesture: false
            // 注意大小写：QML 里是 fullScreenSupportEnabled（Screen 的 S 大写）。
            // 写成 fullscreenSupportEnabled 不会在编译期报错，而是整页
            // "Cannot assign to non-existent property" 编译失败 → 白屏。
            settings.fullScreenSupportEnabled: false

            onLoadingChanged: function (loadRequest) {
                if (loadRequest.status === WebEngineView.LoadSucceededStatus) {
                    page.failed = false;
                    page.failureText = "";
                } else if (loadRequest.status === WebEngineView.LoadFailedStatus) {
                    page.failed = true;
                    page.failureText = loadRequest.errorString || "页面打不开";
                    console.warn("[webgame] 加载失败：", loadRequest.url, loadRequest.errorString);
                }
            }

            onRenderProcessTerminated: function (terminationStatus, exitCode) {
                page.failed = true;
                page.failureText = "网页进程异常退出（" + exitCode + "）";
                console.warn("[webgame] 渲染进程退出：", terminationStatus, exitCode);
            }

            // 游戏里偶尔用 alert/confirm，Chromium 默认直接拒绝 —— 孩子会以为按钮坏了
            onJavaScriptDialogRequested: function (request) {
                request.accepted = true;
                page.dialogRequest = request;
                page.dialogMessage = request.message || "";
                page.dialogDefaultText = request.defaultText || "";
                dialogOverlay.visible = true;
            }

            // 童锁里不要右键菜单
            onContextMenuRequested: function (request) {
                request.accepted = true;
            }
        }

        // ---- 加载中 ----
        Rectangle {
            id: loadingOverlay

            anchors.fill: parent
            visible: gameView.loading && !page.failed
            color: "#FFF8EE"

            Column {
                anchors.centerIn: parent
                spacing: 16

                BusyIndicator {
                    anchors.horizontalCenter: parent.horizontalCenter
                    width: 56
                    height: 56
                    running: loadingOverlay.visible
                }

                Text {
                    anchors.horizontalCenter: parent.horizontalCenter
                    text: gameView.loadProgress > 0 ? ("正在打开游戏 " + gameView.loadProgress + "%")
                                                    : "正在打开游戏…"
                    font.pixelSize: 18
                    color: "#7A6759"
                }
            }
        }

        // ---- 失败 ----
        Rectangle {
            anchors.fill: parent
            visible: page.failed
            color: "#FFF8EE"

            ColumnLayout {
                anchors.centerIn: parent
                spacing: 18
                width: Math.min(parent.width - 80, 560)

                Text {
                    Layout.alignment: Qt.AlignHCenter
                    text: "这个游戏打开失败了"
                    font.pixelSize: 24
                    font.weight: Font.Bold
                    color: "#4A3B32"
                }

                Text {
                    Layout.alignment: Qt.AlignHCenter
                    Layout.fillWidth: true
                    text: page.failureText
                    font.pixelSize: 15
                    color: "#9B8574"
                    wrapMode: Text.Wrap
                    horizontalAlignment: Text.AlignHCenter
                }

                Text {
                    Layout.alignment: Qt.AlignHCenter
                    Layout.fillWidth: true
                    text: "如果一直打不开，请家长确认已安装 qt6-webengine 组件。"
                    font.pixelSize: 13
                    color: "#B8A492"
                    wrapMode: Text.Wrap
                    horizontalAlignment: Text.AlignHCenter
                }

                LargeButton {
                    Layout.alignment: Qt.AlignHCenter
                    Layout.preferredWidth: 200
                    Layout.preferredHeight: 58
                    text: "重试"
                    onClicked: {
                        page.failed = false;
                        gameView.reload();
                    }
                }
            }
        }
    }

    // ---- JS 弹窗（alert / confirm / prompt）----
    Rectangle {
        id: dialogOverlay

        anchors.fill: parent
        visible: false
        z: 50
        color: "#66000000"

        Rectangle {
            anchors.centerIn: parent
            width: Math.min(parent.width - 120, 520)
            height: dialogColumn.implicitHeight + 40
            radius: 20
            color: "#FFFFFF"

            ColumnLayout {
                id: dialogColumn

                anchors.fill: parent
                anchors.margins: 20
                spacing: 14

                Text {
                    Layout.fillWidth: true
                    text: page.dialogMessage
                    font.pixelSize: 18
                    color: "#4A3B32"
                    wrapMode: Text.Wrap
                }

                TextField {
                    id: dialogInput

                    Layout.fillWidth: true
                    Layout.preferredHeight: 46
                    visible: page.dialogDefaultText !== ""
                    text: page.dialogDefaultText
                    font.pixelSize: 17
                    placeholderText: "在这里输入"
                }

                RowLayout {
                    Layout.fillWidth: true
                    spacing: 12

                    Item {
                        Layout.fillWidth: true
                    }

                    LargeButton {
                        Layout.preferredWidth: 120
                        Layout.preferredHeight: 52
                        text: "取消"
                        accentColor: "#FFFFFF"
                        labelColor: "#7A6759"
                        onClicked: {
                            if (page.dialogRequest) {
                                page.dialogRequest.dialogReject();
                            }
                            dialogOverlay.visible = false;
                            page.dialogRequest = null;
                        }
                    }

                    LargeButton {
                        Layout.preferredWidth: 120
                        Layout.preferredHeight: 52
                        text: "确定"
                        onClicked: {
                            if (page.dialogRequest) {
                                page.dialogRequest.dialogAccept(dialogInput.text || page.dialogDefaultText);
                            }
                            dialogOverlay.visible = false;
                            page.dialogRequest = null;
                        }
                    }
                }
            }
        }
    }
}
