import QtQuick
import QtQuick.Controls
import QtQuick.Layouts
import QtQuick.Window
import KidsSafe 1.0

/*
 * 家长后台。对应 C# 的 ParentDashboardPage，但只保留了 Linux MVP 需要的部分：
 * 时长、白名单、重置密码、退出。内容审核、排行榜、服务器同步等页面待后续移植。
 */
Item {
    id: page

    // TapHandler 不是 Item，`Window.window` 附加属性对它返回 null——附加属性
    // 是靠「item → 所在窗口」反查的，handler 没有窗口归属。所以统一在根 Item
    // 上取一次窗口，页面里所有跳转都用这个属性；MouseArea、TapHandler、定时器
    // 里表现一致。（deepin / Qt 6.8 实测：MouseArea 里能用、TapHandler 里必 null）
    readonly property var navWin: Window.window

    property bool confirmingExit: false
    property string serverHint: ""

    function minutesText(seconds) {
        const m = Math.floor(seconds / 60);
        return m > 0 ? (m + " 分钟") : (seconds + " 秒");
    }

    function eyeIntensityText(value) {
        if (value < 40) {
            return "柔和";
        }
        if (value < 75) {
            return "标准";
        }
        return "强力";
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
                // 必须是 popToRoot，不能是 popPage：回家路径是
                // 「桌面 → 家长验证 → 后台」，弹一层只会回到验证页，
                // 家长得再输一次密码、又进后台，看起来就是卡在家长界面出不去。
                onClicked: page.navWin.popToRoot()
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
                    Layout.preferredHeight: 236
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
                                Layout.preferredWidth: 132
                                Layout.preferredHeight: 52
                                text: Stories.testing ? "测试中…" : "测试连接"
                                accentColor: "#EFE1D0"
                                labelColor: "#7A6759"
                                enabled: !Stories.testing
                                onClicked: {
                                    // 先落盘再测，否则测的是上一次的地址
                                    Settings.serverUrl = serverField.text;
                                    Stories.refreshOnlineState();
                                    Stories.testConnection();
                                }
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
                            text: Stories.testResult.length > 0
                                ? Stories.testResult
                                : (page.serverHint.length > 0
                                    ? page.serverHint
                                    : "留空即完全离线，故事屋会显示「未开启」。Linux 服务端见 Linux/server/README.md")
                            font.pixelSize: 13
                            color: (Stories.testResult.indexOf("连接成功") === 0
                                    || page.serverHint.indexOf("已启用") === 0) ? "#1D9E75" : "#B8A492"
                            wrapMode: Text.Wrap
                        }
                    }
                }

                // ---- 首页天气 ----
                // 数据源是 Open-Meteo（公开、免密钥），和家长服务器无关，
                // 所以和「在线故事」分成两块：关掉故事不影响天气，反之亦然。
                // 高度 224：下面那行状态文案可能因为「超时（8 秒没有响应）」
                // 这类原因折成两行，写 208 会顶出卡片。
                Rectangle {
                    Layout.fillWidth: true
                    Layout.preferredHeight: 224
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
                                    text: "首页天气"
                                    font.pixelSize: 17
                                    font.weight: Font.DemiBold
                                    color: "#4A3B32"
                                }
                                Text {
                                    text: "来自 Open-Meteo，免密钥；关掉后首页不显示天气条"
                                    font.pixelSize: 13
                                    color: "#B8A492"
                                }
                            }

                            Item { Layout.fillWidth: true }

                            Switch {
                                checked: Settings.weatherEnabled
                                onToggled: Settings.weatherEnabled = checked
                            }
                        }

                        RowLayout {
                            Layout.fillWidth: true
                            spacing: 12

                            TextField {
                                id: cityField

                                Layout.fillWidth: true
                                Layout.preferredHeight: 52
                                placeholderText: "留空或填 auto = 按 IP 定位；也可填城市名，如 达州"
                                text: Settings.weatherCity
                                font.pixelSize: 16
                                // 只在失焦或回车时写回，避免每敲一个字符就联网一次
                                onEditingFinished: Settings.weatherCity = text
                            }

                            LargeButton {
                                Layout.preferredWidth: 120
                                Layout.preferredHeight: 52
                                text: "刷新"
                                accentColor: "#EFE1D0"
                                labelColor: "#7A6759"
                                onClicked: {
                                    Settings.weatherCity = cityField.text;
                                    Weather.refresh();
                                }
                            }
                        }

                        Text {
                            Layout.fillWidth: true
                            // 拿不到数据时把原因原样写出来。原来只有一句「确认网络可用」，
                            // 家长看不出到底是没网、超时、还是城市名打错了 —— 天气这块
                            // 一直空着的时候，这句提示是唯一的线索。
                            text: Weather.available
                                  ? ("当前：" + Weather.city + " " + Weather.tempText + " " + Weather.descText
                                     + "（" + Weather.statusText + "）")
                                  : (Weather.problemText.length > 0
                                     ? ("还没有天气数据：" + Weather.problemText
                                        + (Weather.loading ? "（正在重试）" : ""))
                                     : "还没有天气数据：确认网络可用，或把城市填成 auto")
                            font.pixelSize: 13
                            color: Weather.available ? "#1D9E75"
                                                     : (Weather.problemText.length > 0 ? "#D9822B"
                                                                                       : "#B8A492")
                            wrapMode: Text.Wrap
                        }
                    }
                }

                // ---- 语音朗读 ----
                // 可用性由 Speech（C++）判定：spd-say 在不在 + 有没有真正的输出模块。
                // 只查可执行文件会把「装了没配」误判成可用 —— 那种情况下按钮是亮的、
                // 却一点声音都没有，家长根本不知道去哪查。这里把 detail 原样显示出来，
                // 并给一个「重新检测」，家长装完包不用重启应用。
                Rectangle {
                    Layout.fillWidth: true
                    Layout.preferredHeight: 212
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
                                    text: "语音朗读"
                                    font.pixelSize: 17
                                    font.weight: Font.DemiBold
                                    color: "#4A3B32"
                                }

                                Text {
                                    text: "眼保健操口诀、故事朗读都靠它（speech-dispatcher）"
                                    font.pixelSize: 13
                                    color: "#B8A492"
                                }
                            }

                            Item {
                                Layout.fillWidth: true
                            }

                            Rectangle {
                                Layout.preferredWidth: speechBadge.width + 28
                                Layout.preferredHeight: 34
                                radius: 17
                                color: Speech.available ? "#E8F7EF" : "#FBEAEA"
                                border.color: Speech.available ? "#B7E4CB" : "#F5C9C9"
                                border.width: 1

                                Text {
                                    id: speechBadge

                                    anchors.centerIn: parent
                                    text: Speech.available ? "可用" : "不可用"
                                    font.pixelSize: 14
                                    font.weight: Font.DemiBold
                                    color: Speech.available ? "#1D9E75" : "#C0564F"
                                }
                            }
                        }

                        Text {
                            Layout.fillWidth: true
                            text: Speech.detail
                            font.pixelSize: 13
                            color: Speech.available ? "#1D9E75" : "#B8A492"
                            wrapMode: Text.Wrap
                        }

                        Item {
                            Layout.fillHeight: true
                        }

                        RowLayout {
                            spacing: 12

                            LargeButton {
                                Layout.preferredWidth: 130
                                Layout.preferredHeight: 48
                                text: "试听"
                                enabled: Speech.available
                                onClicked: Speech.say("小朋友你好，我是小安。")
                            }

                            LargeButton {
                                Layout.preferredWidth: 140
                                Layout.preferredHeight: 48
                                text: "重新检测"
                                accentColor: "#EFE1D0"
                                labelColor: "#7A6759"
                                onClicked: Speech.recheck()
                            }

                            Item {
                                Layout.fillWidth: true
                            }
                        }
                    }
                }

                // ---- 界面语言 ----
                // 首次引导里选过一次，这里给家长留个改的入口：老用户升级上来
                // 不会经过引导，只能从这里切。
                // 中文模式不显示「故事剧场（吉里吉里）」入口 —— 内容合规要求，
                // 所以这句提示写在按钮旁边，而不是藏在文档里。
                Rectangle {
                    Layout.fillWidth: true
                    Layout.preferredHeight: 104
                    radius: 24
                    color: "#FFFFFF"
                    border.color: "#F2E1CD"
                    border.width: 1

                    RowLayout {
                        anchors.fill: parent
                        anchors.margins: 22
                        spacing: 12

                        ColumnLayout {
                            spacing: 2

                            Text {
                                text: "界面语言"
                                font.pixelSize: 17
                                font.weight: Font.DemiBold
                                color: "#4A3B32"
                            }

                            Text {
                                text: "简体中文不显示「故事剧场」入口，English 不限制"
                                font.pixelSize: 13
                                color: "#B8A492"
                            }
                        }

                        Item { Layout.fillWidth: true }

                        LargeButton {
                            Layout.preferredWidth: 128
                            Layout.preferredHeight: 46
                            text: "简体中文"
                            accentColor: Settings.language === "zh_CN" ? "#FF8A5C" : "#EFE1D0"
                            labelColor: Settings.language === "zh_CN" ? "#FFFFFF" : "#7A6759"
                            onClicked: Settings.language = "zh_CN"
                        }

                        LargeButton {
                            Layout.preferredWidth: 120
                            Layout.preferredHeight: 46
                            text: "English"
                            accentColor: Settings.english ? "#FF8A5C" : "#EFE1D0"
                            labelColor: Settings.english ? "#FFFFFF" : "#7A6759"
                            onClicked: Settings.language = "en_US"
                        }
                    }
                }

                // ---- 护眼模式 ----
                // 只读状态来自 EyeCare（C++ 计算），这里不重复实现定时判断：
                // 跨零点、起止相同这些边界都在 eyecare.cpp 里处理过一次，
                // 两处各写一套迟早会不一致。
                Rectangle {
                    Layout.fillWidth: true
                    Layout.preferredHeight: 344
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
                                    text: "护眼模式"
                                    font.pixelSize: 17
                                    font.weight: Font.DemiBold
                                    color: "#4A3B32"
                                }
                                Text {
                                    text: "把画面调暖、减少蓝光，晚饭后尤其有用"
                                    font.pixelSize: 13
                                    color: "#B8A492"
                                }
                            }

                            Item { Layout.fillWidth: true }

                            Text {
                                text: "常开"
                                font.pixelSize: 14
                                color: "#9B8574"
                            }

                            Switch {
                                checked: Settings.eyeCareEnabled
                                onToggled: Settings.eyeCareEnabled = checked
                            }
                        }

                        RowLayout {
                            Layout.fillWidth: true
                            spacing: 12

                            Text {
                                text: "强度"
                                font.pixelSize: 14
                                color: "#9B8574"
                            }

                            Slider {
                                Layout.fillWidth: true
                                from: 0
                                to: 100
                                stepSize: 5
                                snapMode: Slider.SnapAlways
                                value: Settings.eyeCareIntensity
                                onMoved: Settings.eyeCareIntensity = value
                            }

                            Text {
                                Layout.preferredWidth: 92
                                text: page.eyeIntensityText(Settings.eyeCareIntensity)
                                      + " " + Settings.eyeCareIntensity + "%"
                                font.pixelSize: 14
                                color: "#7A6759"
                            }
                        }

                        RowLayout {
                            Layout.fillWidth: true
                            spacing: 12

                            Text {
                                text: "到点自动开启"
                                font.pixelSize: 14
                                color: "#9B8574"
                            }

                            Item { Layout.fillWidth: true }

                            Switch {
                                checked: Settings.eyeCareAutoEnabled
                                onToggled: Settings.eyeCareAutoEnabled = checked
                            }
                        }

                        RowLayout {
                            Layout.fillWidth: true
                            spacing: 12
                            visible: Settings.eyeCareAutoEnabled

                            Text {
                                Layout.preferredWidth: 92
                                text: "从 " + Settings.eyeCareStartHour + " 点"
                                font.pixelSize: 14
                                color: "#7A6759"
                            }

                            Slider {
                                Layout.fillWidth: true
                                from: 0
                                to: 23
                                stepSize: 1
                                snapMode: Slider.SnapAlways
                                value: Settings.eyeCareStartHour
                                onMoved: Settings.eyeCareStartHour = value
                            }
                        }

                        RowLayout {
                            Layout.fillWidth: true
                            spacing: 12
                            visible: Settings.eyeCareAutoEnabled

                            Text {
                                Layout.preferredWidth: 92
                                text: "到 " + Settings.eyeCareEndHour + " 点"
                                font.pixelSize: 14
                                color: "#7A6759"
                            }

                            Slider {
                                Layout.fillWidth: true
                                from: 0
                                to: 23
                                stepSize: 1
                                snapMode: Slider.SnapAlways
                                value: Settings.eyeCareEndHour
                                onMoved: Settings.eyeCareEndHour = value
                            }
                        }

                        Text {
                            Layout.fillWidth: true
                            text: EyeCare.statusText + "（只看本桌面；放行到其他应用时不生效）"
                            font.pixelSize: 13
                            color: EyeCare.active ? "#1D9E75" : "#B8A492"
                            wrapMode: Text.Wrap
                        }
                    }
                }

                // ---- 小游戏工坊 ----
                // 家长做游戏、孩子玩游戏：编辑器是网页，孩子那张「游戏乐园」卡片
                // 只管完。没有 WebEngine 时按钮置灰并说明原因，不给一个点了白屏的入口。
                Rectangle {
                    Layout.fillWidth: true
                    Layout.preferredHeight: 112
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
                                text: "小游戏工坊"
                                font.pixelSize: 17
                                font.weight: Font.DemiBold
                                color: "#4A3B32"
                            }

                            Text {
                                text: Maker.available
                                      ? "不写代码：选玩法、换表情或自己的图片，做出能玩的小游戏"
                                      : "需要网页组件（qt6-webengine），装好后重开本程序"
                                font.pixelSize: 13
                                color: Maker.available ? "#B8A492" : "#C0392B"
                                wrapMode: Text.Wrap
                                Layout.fillWidth: true
                            }
                        }

                        Item { Layout.fillWidth: true }

                        LargeButton {
                            Layout.preferredWidth: 150
                            Layout.preferredHeight: 56
                            text: "打开工坊"
                            accentColor: Maker.available ? "#7E57C2" : "#EFE1D0"
                            labelColor: Maker.available ? "white" : "#B8A492"
                            enabled: Maker.available
                            onClicked: page.navWin.pushPage(Qt.resolvedUrl("MakerPage.qml"))
                        }
                    }
                }

                // ---- 意见反馈与问卷 ----
                // 与 Windows / 安卓的「意见反馈与问卷」卡片对应：数据直接进官网，
                // 不跟家长自建服务器走（那台机器开发者看不到）。断网时先存本机队列。
                Rectangle {
                    Layout.fillWidth: true
                    Layout.preferredHeight: 128
                    radius: 24
                    color: "#FFFFFF"
                    border.color: "#F6D9BE"
                    border.width: 1

                    RowLayout {
                        anchors.fill: parent
                        anchors.margins: 22
                        spacing: 16

                        ColumnLayout {
                            Layout.fillWidth: true
                            spacing: 3

                            Text {
                                text: "💬 意见反馈与问卷"
                                font.pixelSize: 17
                                font.weight: Font.DemiBold
                                color: "#4A3B32"
                            }

                            Text {
                                text: "遇到问题、想要新功能都可以直接说；满意度与问卷会同步到官网统计"
                                font.pixelSize: 13
                                color: "#B8A492"
                                wrapMode: Text.Wrap
                                Layout.fillWidth: true
                            }

                            Text {
                                visible: Feedback.pending > 0
                                text: "本机还有 " + Feedback.pending + " 条没发出去，联网后会自动补传"
                                font.pixelSize: 12
                                color: "#B4552F"
                                wrapMode: Text.Wrap
                                Layout.fillWidth: true
                            }
                        }

                        LargeButton {
                            Layout.preferredWidth: 150
                            Layout.preferredHeight: 56
                            text: "去反馈"
                            accentColor: "#E2603D"
                            onClicked: page.navWin.pushPage(Qt.resolvedUrl("FeedbackPage.qml"))
                        }
                    }
                }

                // ---- 系统守护（自启 / 自愈 / 快捷键）----
                // 与 Windows 的「系统守护」卡片一一对应，但 Linux 侧的手段不同：
                // 自启走 XDG autostart，自愈走 systemd --user 的 Restart=always，
                // 快捷键只在 X11 下能抢（Wayland 靠会话裁剪，状态文案会如实说明）。
                Rectangle {
                    Layout.fillWidth: true
                    Layout.preferredHeight: 330
                    radius: 24
                    color: "#FFFFFF"
                    border.color: "#F2E1CD"
                    border.width: 1

                    ColumnLayout {
                        anchors.fill: parent
                        anchors.margins: 22
                        spacing: 10

                        Text {
                            text: "系统守护"
                            font.pixelSize: 17
                            font.weight: Font.DemiBold
                            color: "#4A3B32"
                        }

                        Text {
                            text: "登录自启和崩溃自愈只写当前用户目录；两项互斥，避免起来两份全屏窗口。"
                            font.pixelSize: 13
                            color: "#B8A492"
                            wrapMode: Text.Wrap
                            Layout.fillWidth: true
                        }

                        RowLayout {
                            Layout.fillWidth: true
                            spacing: 12
                            Text {
                                text: "① 登录自动进入儿童桌面"
                                font.pixelSize: 15
                                color: "#5D4037"
                                Layout.fillWidth: true
                            }
                            Switch {
                                checked: Startup.autostartEnabled
                                onToggled: {
                                    Startup.setAutostart(checked);
                                    checked = Startup.autostartEnabled;
                                }
                            }
                        }

                        RowLayout {
                            Layout.fillWidth: true
                            spacing: 12
                            Text {
                                text: "② 进程被结束后自动重启"
                                font.pixelSize: 15
                                color: "#5D4037"
                                Layout.fillWidth: true
                            }
                            Switch {
                                enabled: Startup.systemdAvailable
                                checked: Startup.watchdogEnabled
                                onToggled: {
                                    Startup.setWatchdog(checked);
                                    checked = Startup.watchdogEnabled;
                                }
                            }
                        }

                        RowLayout {
                            Layout.fillWidth: true
                            spacing: 12
                            Text {
                                text: "③ 屏蔽 Alt+Tab / Alt+F4 / Ctrl+Shift+Esc"
                                font.pixelSize: 15
                                color: "#5D4037"
                                Layout.fillWidth: true
                            }
                            Switch {
                                enabled: HotkeyGuard.available || HotkeyGuard.enabled
                                checked: HotkeyGuard.enabled
                                onToggled: {
                                    if (checked) {
                                        HotkeyGuard.install();
                                    } else {
                                        HotkeyGuard.uninstall();
                                    }
                                    checked = HotkeyGuard.enabled;
                                }
                            }
                        }

                        Text {
                            Layout.fillWidth: true
                            text: HotkeyGuard.statusText
                            font.pixelSize: 13
                            color: "#B8A492"
                            wrapMode: Text.Wrap
                        }

                        Text {
                            Layout.fillWidth: true
                            text: Startup.statusText
                            font.pixelSize: 13
                            color: Startup.statusText.indexOf("失败") >= 0 ? "#C0392B" : "#1D9E75"
                            wrapMode: Text.Wrap
                        }

                        Text {
                            Layout.fillWidth: true
                            text: "Ctrl+Alt+Del 是内核/显示服务器处理的安全序列，应用层拦不住；"
                                  + "真正的隔离建议用 kidssafe-session 专用会话（不加载桌面环境）。"
                            font.pixelSize: 12
                            color: "#C4AE99"
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
                                    // 这一行是排障用的分界线：终端/日志里看到它，
                                    // 说明点击确实到了、交给 C++ 了；看不到它就是
                                    // 按钮没收到点击（遮挡或事件问题），与退出逻辑无关。
                                    console.log("[exit] 家长确认退出，调用 Kiosk.quit()");
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
