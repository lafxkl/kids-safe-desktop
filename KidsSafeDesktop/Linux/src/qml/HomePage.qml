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

    function openApp(app) {
        if (!app) {
            return;
        }
        // exec 来自 .desktop 的 Exec 字段，也可能是家长手填的 http 链接，
        // 两种情况都由 AppLauncher 统一处理。
        AppLauncher.launch(app.exec || "", app.name || "");
    }

    /*
     * 头像 emoji → 自绘图标。
     *
     * 头像是孩子每天第一眼看到的东西，不能是黑白线稿。deepin 默认没有彩色
     * emoji 字体（🧸 会渲染成一团黑），所以在常用 emoji 上都挂了手绘头像；
     * 表里没有的（家长自己输入的任意字符）仍然按原样用 Text 画出来，
     * 不剥夺自定义能力，只是那一种情况下效果取决于系统字体。
     */
    function avatarGlyph(value) {
        switch (value) {
        case "🧸":
        case "🐻":
        case "🐨":
            return "bear";
        // 🐯 是 Windows 端的默认头像，必须映射，否则 Linux 上退回 emoji 文本
        // 就是一团黑（deepin 没有彩色 emoji 字体）——首页左上角那个「小黑熊」
        // 就是这么来的。
        case "🐯":
        case "🐅":
            return "tiger";
        case "🐶":
        case "🐕":
            return "dog";
        case "🦊":
            return "fox";
        case "🐷":
        case "🐖":
            return "pig";
        case "🐱":
        case "🐈":
            return "cat";
        case "🐰":
        case "🐇":
            return "rabbit";
        case "🐼":
            return "panda";
        default:
            return "";
        }
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

            // 与 Windows 端同一个样子：白底圆形 + 大号动物头像
            // （那边是 emoji 字体渲染成彩色贴纸，这边用 GlyphIcon 手绘，
            //  否则在 deepin 上只会得到一团黑色轮廓）。
            Rectangle {
                Layout.preferredWidth: 68
                Layout.preferredHeight: 68
                radius: 34
                color: "#FFFFFF"

                GlyphIcon {
                    anchors.centerIn: parent
                    visible: page.avatarGlyph(Settings.avatarEmoji) !== ""
                    name: page.avatarGlyph(Settings.avatarEmoji)
                    width: 56
                    height: 56
                }

                Text {
                    anchors.centerIn: parent
                    visible: page.avatarGlyph(Settings.avatarEmoji) === ""
                    text: Settings.avatarEmoji
                    font.pixelSize: 34
                }
            }

            ColumnLayout {
                spacing: 2

                Text {
                    text: Settings.t("home.hello") + Settings.nickname
                    font.pixelSize: 26
                    font.weight: Font.Bold
                    color: "#4A3B32"
                }

                Text {
                    text: Settings.t("home.subtitle")
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
                // 与 Windows 首页的剩余时间胶囊同一套配色：橙底橙字，
                // 在白底页面上是最容易被孩子认出来的那一块
                color: ScreenTime.lockedOut ? "#FFD9D2" : "#FFE0B2"
                border.color: ScreenTime.lockedOut ? "#F5B7B1" : "#FFCC94"
                border.width: 1

                RowLayout {
                    id: timeRow

                    anchors.centerIn: parent
                    spacing: 10

                    GlyphIcon {
                        Layout.preferredWidth: 24
                        Layout.preferredHeight: 24
                        name: "hourglass"
                        color: ScreenTime.lockedOut ? "#C0392B" : "#E65100"
                    }

                    Text {
                        text: ScreenTime.lockedOut ? "今天的时间用完啦" : ("还可以玩 " + ScreenTime.remainingText)
                        font.pixelSize: 19
                        font.weight: Font.DemiBold
                        color: ScreenTime.lockedOut ? "#C0392B" : "#E65100"
                    }
                }
            }

            // 护眼生效时给个明确提示，否则孩子/家长只会觉得「屏幕怎么发黄了」
            Rectangle {
                visible: EyeCare.active
                Layout.preferredHeight: 58
                Layout.preferredWidth: eyeBadgeText.implicitWidth + 66
                radius: 29
                color: "#FFF0DC"
                border.color: "#F6D8AE"
                border.width: 1

                RowLayout {
                    anchors.centerIn: parent
                    spacing: 7

                    GlyphIcon {
                        Layout.preferredWidth: 22
                        Layout.preferredHeight: 22
                        name: "eye"
                        color: "#8A6A3A"
                    }

                    Text {
                        id: eyeBadgeText

                        text: "护眼"
                        font.pixelSize: 17
                        color: "#8A6A3A"
                    }
                }
            }

            LargeButton {
                Layout.preferredWidth: 116
                Layout.preferredHeight: 58
                text: Settings.t("home.parent")
                accentColor: "#FFFFFF"
                labelColor: "#7A6759"
                onClicked: page.navWin.pushPage(Qt.resolvedUrl("ParentGatePage.qml"), {
                    "purpose": "dashboard"
                })
            }
        }

        // ---- 天气 / 电脑桌面入口 ----
        // 全宽一条，和 Windows 首页「时钟下面的天气条」位置对应。
        // 没网、没缓存、家长关掉天气时自身 visible 为 false，RowLayout 会
        // 跳过它，右边的「我的电脑」还在，不会留一条空白。
        //
        // 「我的电脑」和天气并排放而不是自己占一行：首页纵向已经很满
        // （问候 + 天气 + 五个横幅 + 应用网格），再加一条 84px 会把应用区
        // 压到只剩两行。并排之后零额外高度，位置又比藏在图标墙里显眼。
        RowLayout {
            Layout.fillWidth: true
            spacing: 16

            WeatherCard {
                Layout.fillWidth: true
                Layout.preferredHeight: 86
            }

            Rectangle {
                id: desktopEntry

                Layout.preferredWidth: 268
                Layout.preferredHeight: 86
                radius: 22

                gradient: Gradient {
                    GradientStop { position: 0.0; color: "#3D7BF7" }
                    GradientStop { position: 1.0; color: "#5CA6FF" }
                }

                RowLayout {
                    anchors.fill: parent
                    anchors.margins: 16
                    spacing: 12

                    Rectangle {
                        Layout.preferredWidth: 52
                        Layout.preferredHeight: 52
                        radius: 16
                        color: "#33FFFFFF"

                        GlyphIcon {
                            anchors.centerIn: parent
                            name: "monitor"
                            width: 28
                            height: 28
                            color: "white"
                        }
                    }

                    ColumnLayout {
                        Layout.fillWidth: true
                        spacing: 2

                        Text {
                            Layout.fillWidth: true
                            text: "我的电脑"
                            font.pixelSize: 18
                            font.weight: Font.Bold
                            color: "white"
                            elide: Text.ElideRight
                        }

                        Text {
                            Layout.fillWidth: true
                            text: "文件 · 终端 · 任务管理器"
                            font.pixelSize: 12
                            color: "#D9E8FF"
                            elide: Text.ElideRight
                        }
                    }
                }

                // 悬停提亮，给出「可以点」的反馈
                HoverHandler {
                    id: desktopHover
                }

                Rectangle {
                    anchors.fill: parent
                    radius: parent.radius
                    color: "#FFFFFF"
                    opacity: desktopHover.hovered ? 0.10 : 0.0

                    Behavior on opacity {
                        NumberAnimation { duration: 120 }
                    }
                }

                TapHandler {
                    onTapped: page.navWin.pushPage(Qt.resolvedUrl("DesktopPage.qml"))
                }
            }
        }

        // ---- 内置内容入口 ----
        // 四个都有「离线也能用」的部分：故事屋离线时提示去家长后台，
        // 安全学堂、游戏乐园、眼保健操完全内置，不依赖任何服务器。
        // 四卡一行：1280 宽时每张约 290px，副标题会自动省略，标题不会被挤。
        RowLayout {
            Layout.fillWidth: true
            spacing: 16

            HomeBanner {
                Layout.fillWidth: true
                Layout.preferredHeight: 124
                glyph: "book"
                title: Settings.t("banner.story")
                subtitle: Settings.t("banner.storySub")
                colorFrom: "#8E7CFF"
                colorTo: "#B388FF"
                onTapped: page.navWin.pushPage(Qt.resolvedUrl("StoryHousePage.qml"))
            }

            HomeBanner {
                Layout.fillWidth: true
                Layout.preferredHeight: 124
                glyph: "music"
                title: Settings.t("banner.media")
                subtitle: Settings.t("banner.mediaSub")
                colorFrom: "#F06292"
                colorTo: "#FFB0C8"
                onTapped: page.navWin.pushPage(Qt.resolvedUrl("MediaHubPage.qml"))
            }

            HomeBanner {
                Layout.fillWidth: true
                Layout.preferredHeight: 124
                glyph: "shield"
                title: Settings.t("banner.safety")
                subtitle: Settings.t("banner.safetySub")
                colorFrom: "#FF9F6B"
                colorTo: "#FFC46B"
                onTapped: page.navWin.pushPage(Qt.resolvedUrl("SafetyWorldPage.qml"))
            }

            HomeBanner {
                Layout.fillWidth: true
                Layout.preferredHeight: 124
                glyph: "gamepad"
                title: Settings.t("banner.games")
                subtitle: Settings.t("banner.gamesSub")
                colorFrom: "#5AC8E8"
                colorTo: "#7BD9A5"
                onTapped: page.navWin.pushPage(Qt.resolvedUrl("GamesHubPage.qml"))
            }

            // 眼保健操：对应 Windows 端首页的「👀 眼保健操」磁贴。
            // 排在游戏乐园后面 —— 玩之前先护眼，位置也算个暗示。
            HomeBanner {
                Layout.fillWidth: true
                Layout.preferredHeight: 124
                glyph: "eye"
                title: Settings.t("banner.eye")
                subtitle: Settings.t("banner.eyeSub")
                colorFrom: "#7E57C2"
                colorTo: "#B39DDB"
                onTapped: page.navWin.pushPage(Qt.resolvedUrl("EyeExercisePage.qml"))
            }
        }

        Text {
            Layout.leftMargin: 6
            text: Settings.t("home.myApps")
            font.pixelSize: 18
            font.weight: Font.DemiBold
            color: "#9B8574"
        }

        // ---- 应用磁贴网格 ----
        // 网格和「空状态」放在同一个普通 Item 里，空状态用 anchors.centerIn
        // 指向这个 Item：直接 centerIn 一个由 Layout 管理的子项时，实测
        // （deepin / Qt 6.8）空状态会被摆到左上角，压住顶部问候语。
        Item {
            id: gridArea

            Layout.fillWidth: true
            Layout.fillHeight: true

            GridView {
                id: appGrid

                anchors.fill: parent
                clip: true

                // 与 Windows 磁贴同一套尺寸逻辑：每格约 104px（84 色块 + 左右间距），
                // 列数按可用宽度算，同一块屏永远算出同一组尺寸。
                // 行高只留「间隙 + 名称胶囊」的高度就够了 —— 之前按 1.08 倍留白，
                // 多出来的十几像素把图标推得稀稀拉拉，和 Windows 的紧凑排布对不上。
                readonly property int columns: Math.max(3, Math.min(12, Math.floor(width / 104)))
                readonly property real cell: Math.floor(width / columns)
                readonly property real badge: Math.min(84, cell * 0.81)

                cellWidth: cell
                cellHeight: Math.round(badge + 46)

                model: Settings.allowedApps

                delegate: Item {
                    width: appGrid.cellWidth
                    height: appGrid.cellHeight

                    AppTile {
                        anchors.fill: parent
                        appName: modelData.name || ""
                        iconName: modelData.icon || ""
                        accent: modelData.accent || "#4C8BF5"
                        onClicked: page.openApp(modelData)
                    }
                }
            }

            ColumnLayout {
                anchors.centerIn: parent
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
            page.navWin.pushPage(Qt.resolvedUrl("LockoutPage.qml"));
        }
    }

    // 引导还没走完时，这一页只是垫在引导盖层下面（见 Main.qml），
    // 那时候不该开始扣时间。
    Component.onCompleted: {
        if (Settings.onboardingDone) {
            ScreenTime.start();
        }
    }

}
