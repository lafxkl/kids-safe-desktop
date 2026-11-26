import QtQuick
import QtQuick.Controls
import QtQuick.Layouts
import KidsSafe 1.0

/*
 * 首次引导：选语言 → 起名字 → 设家长密码 → 挑允许的应用。
 * 四步走完才进桌面，等价于 C# 版的 OnboardingPage（那边没有语言这一步）。
 *
 * 语言放在第一步，是因为它决定后面几步的文案，也决定首页出现哪些入口：
 *   简体中文 → 不出现「故事剧场（吉里吉里）」（Settings.vnAllowed = false）；
 *   English  → 不限制。
 * 这个取舍是内容合规上的硬要求，不是偏好开关，所以不放进家长后台的
 * 常规设置里，只在首次引导选一次（配置文件里仍可手改 Language=）。
 */
Item {
    id: page

    property int step: 0
    property string firstPin: ""
    property string tipText: ""

    /*
     * 卡片宽度只在 page 上算一次，卡片和内层内容都用这个数。
     *
     * 原来内层写的是 `width: card.width - 72`（再往前是 anchors），两种写法
     * 在实测里都出过同一类故障：第 2 步「起名字」的内层 ColumnLayout 只有
     * 360 宽 —— 正好是 TextField 的 implicitWidth —— 标题、输入框、按钮
     * 全挤在卡片左边，右边空一大片；可第 4 步「挑应用」因为有 ListView
     * 反而铺满了。同一份 `Layout.fillWidth`，时灵时不灵，说明靠布局链
     * 一层层把宽度传下去不可靠（外层布局给 card 赋值、card 再反推给内层，
     * 中间任何一次尺寸缓存没失效，内层就停在 implicitWidth 上）。
     *
     * 现在宽度是纯算术：页面宽度 → 卡片宽度 → 内层宽度，中间不经过任何
     * 布局结果。四个步骤容器都显式拿 innerWidth，不再指望 fillWidth 传下来。
     */
    readonly property real cardWidth: Math.max(280, Math.min(width - 80, 720))
    readonly property real innerWidth: Math.max(0, cardWidth - 72)

    Backdrop {
        anchors.fill: parent
    }

    // 引导层盖在桌面上，但 Backdrop 本身不接管鼠标。不铺这一层的话，
    // 点在卡片之外的空白处会穿透到下面的桌面图标，小朋友可能直接
    // 启动一个还没被允许的应用。MouseArea 只要接受按下事件就够，
    // 不需要写处理函数。
    MouseArea {
        anchors.fill: parent
        acceptedButtons: Qt.AllButtons
    }

    ColumnLayout {
        anchors.centerIn: parent
        width: page.cardWidth
        spacing: 24

        // 步骤指示
        RowLayout {
            Layout.alignment: Qt.AlignHCenter
            spacing: 10

            Repeater {
                model: 4

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
            id: card

            Layout.fillWidth: true
            Layout.preferredWidth: page.cardWidth
            Layout.preferredHeight: content.implicitHeight + 72
            radius: 32
            color: "#FFFFFF"
            border.color: "#F2E1CD"
            border.width: 1

            /*
             * 内层尺寸用 page 上的计算结果，不用 anchors、也不从 card 反推。
             *
             * 这里踩过两次：先是 anchors.left + right + margins: 36，实测
             * right 那条没起作用，内层只有 319 宽（密码盘的隐式宽度），
             * 内容全挤在左边；改成 `width: card.width - 72` 之后，第 2 步
             * 仍然只有 360 宽（TextField 的隐式宽度）。两次都是内层停在
             * implicitWidth 上，见文件顶部 page.cardWidth 的注释。
             *
             * 高度仍不写，交给 ColumnLayout 用 implicitHeight，否则和外层
             * Rectangle 的 preferredHeight 形成绑定环；宽度直接取 innerWidth，
             * 内容和卡片之间仍然留 36 的内边距。
             */
            ColumnLayout {
                id: content

                x: 36
                y: 36
                width: page.innerWidth
                spacing: 20

                // ---- 第 1 步：选语言 ----
                ColumnLayout {
                    visible: page.step === 0
                    Layout.fillWidth: true
                    // fillWidth 只是「有富余就给我」，真正保证宽度的是这行：
                    // 父布局一定按 preferredWidth 赋值，不经过隐式宽度推断。
                    Layout.preferredWidth: page.innerWidth
                    spacing: 18

                    Text {
                        Layout.alignment: Qt.AlignHCenter
                        text: Settings.t("lang.title")
                        font.pixelSize: 30
                        font.weight: Font.Bold
                        color: "#4A3B32"
                    }

                    Text {
                        Layout.alignment: Qt.AlignHCenter
                        text: Settings.t("lang.hint")
                        font.pixelSize: 16
                        color: "#9B8574"
                    }

                    RowLayout {
                        Layout.alignment: Qt.AlignHCenter
                        Layout.topMargin: 6
                        spacing: 18

                        Repeater {
                            model: [
                                { code: "zh_CN", glyph: "zh", title: "简体中文", sub: "Simplified Chinese" },
                                { code: "en_US", glyph: "en", title: "English", sub: "英语" }
                            ]

                            Rectangle {
                                required property var modelData

                                readonly property bool chosen: Settings.language === modelData.code

                                Layout.preferredWidth: 260
                                Layout.preferredHeight: 148
                                radius: 24
                                color: chosen ? "#FFF1E2" : "#FBF8F4"
                                border.color: chosen ? "#FF8A5C" : (cardHover.hovered ? "#F0D6BC" : "#EEDCC8")
                                border.width: chosen ? 2 : 1

                                ColumnLayout {
                                    anchors.centerIn: parent
                                    spacing: 6

                                    Text {
                                        Layout.alignment: Qt.AlignHCenter
                                        text: modelData.glyph === "zh" ? "中" : "EN"
                                        font.pixelSize: 40
                                        font.weight: Font.Bold
                                        color: chosen ? "#E65100" : "#B8A492"
                                    }

                                    Text {
                                        Layout.alignment: Qt.AlignHCenter
                                        text: modelData.title
                                        font.pixelSize: 20
                                        font.weight: Font.DemiBold
                                        color: "#4A3B32"
                                    }

                                    Text {
                                        Layout.alignment: Qt.AlignHCenter
                                        text: modelData.sub
                                        font.pixelSize: 13
                                        color: "#9B8574"
                                    }
                                }

                                HoverHandler { id: cardHover }

                                TapHandler {
                                    onTapped: {
                                        Settings.language = modelData.code;
                                        page.step = 1;
                                    }
                                }
                            }
                        }
                    }

                    // 中文模式下一句话说明为什么没有「故事剧场」入口，
                    // 免得家长以为是 bug。英文模式不显示。
                    Text {
                        Layout.alignment: Qt.AlignHCenter
                        Layout.fillWidth: true
                        visible: !Settings.english
                        text: "简体中文版不含「故事剧场」入口；切换到 English 才会出现。"
                        font.pixelSize: 12
                        color: "#B8A492"
                        wrapMode: Text.Wrap
                        horizontalAlignment: Text.AlignHCenter
                        elide: Text.ElideRight
                        maximumLineCount: 2
                    }
                }

                // ---- 第 2 步：起名字 ----
                ColumnLayout {
                    visible: page.step === 1
                    Layout.fillWidth: true
                    Layout.preferredWidth: page.innerWidth
                    spacing: 18

                    Text {
                        Layout.alignment: Qt.AlignHCenter
                        text: Settings.t("onboard.welcome")
                        font.pixelSize: 30
                        font.weight: Font.Bold
                        color: "#4A3B32"
                    }

                    Text {
                        Layout.alignment: Qt.AlignHCenter
                        text: Settings.t("onboard.nameAsk")
                        font.pixelSize: 17
                        color: "#9B8574"
                    }

                    TextField {
                        id: nicknameField

                        Layout.alignment: Qt.AlignHCenter
                        Layout.preferredWidth: 360
                        Layout.preferredHeight: 64
                        horizontalAlignment: TextInput.AlignHCenter
                        placeholderText: Settings.t("onboard.namePlaceholder")
                        text: Settings.nickname === "小朋友" || Settings.nickname === "Little friend"
                              ? "" : Settings.nickname
                        font.pixelSize: 24

                        // 外观全部自己画，不用控件默认样式：Basic 风格的
                        // 输入框底色取自系统调色板，遇到深色主题就是白字白底，
                        // 摆在白色卡片上等于「整个输入框消失」——deepin 上
                        // 实测就是这个现象，家长会以为这里根本没有可填的地方。
                        color: "#4A3B32"
                        placeholderTextColor: "#C4AE99"
                        background: Rectangle {
                            radius: 16
                            color: "#FFF8EE"
                            border.width: nicknameField.activeFocus ? 2 : 1
                            border.color: nicknameField.activeFocus ? "#FF8A5C" : "#EFD9C1"
                        }

                        onAccepted: page.step = 2
                    }

                    LargeButton {
                        Layout.alignment: Qt.AlignHCenter
                        Layout.preferredWidth: 240
                        text: Settings.t("onboard.next")
                        onClicked: {
                            const name = nicknameField.text.trim();
                            // 留空也要落一个名字：中文默认「小朋友」，英文默认
                            // 「Little friend」。否则英文界面会出现
                            // 「Hello, 小朋友」这种半截混排。
                            Settings.nickname = name.length > 0
                                    ? name
                                    : (Settings.english ? "Little friend" : "小朋友");
                            page.step = 2;
                        }
                    }
                }

                // ---- 第 3 步：设家长密码 ----
                ColumnLayout {
                    visible: page.step === 2
                    Layout.fillWidth: true
                    Layout.preferredWidth: page.innerWidth
                    spacing: 16

                    Text {
                        Layout.fillWidth: true
                        text: page.firstPin === "" ? Settings.t("onboard.setPin")
                                                   : Settings.t("onboard.confirmPin")
                        font.pixelSize: 30
                        font.weight: Font.Bold
                        color: "#4A3B32"
                        horizontalAlignment: Text.AlignHCenter
                        wrapMode: Text.Wrap
                    }

                    Text {
                        Layout.fillWidth: true
                        text: page.tipText.length > 0 ? page.tipText : Settings.t("onboard.pinHint")
                        font.pixelSize: 16
                        color: page.tipText.length > 0 ? "#D85A30" : "#9B8574"
                        horizontalAlignment: Text.AlignHCenter
                        wrapMode: Text.Wrap
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
                                page.step = 3;
                            } else {
                                page.firstPin = "";
                                page.tipText = Settings.t("onboard.pinMismatch");
                                setupPad.clear();
                            }
                        }
                        onEdited: page.tipText = ""
                    }
                }

                // ---- 第 4 步：挑应用 ----
                ColumnLayout {
                    visible: page.step === 3
                    Layout.fillWidth: true
                    Layout.preferredWidth: page.innerWidth
                    spacing: 14

                    Text {
                        Layout.alignment: Qt.AlignHCenter
                        text: Settings.t("onboard.pickApps")
                        font.pixelSize: 28
                        font.weight: Font.Bold
                        color: "#4A3B32"
                    }

                    Text {
                        Layout.alignment: Qt.AlignHCenter
                        text: Settings.t("onboard.pickAppsHint")
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
                            /*
                             * 必须显式声明 index。
                             *
                             * 委托里用了 `required property appId/name` 之后，
                             * 旧式的上下文属性注入就关了 —— `index` 不再是
                             * 自动可见的变量。开关的 onToggled 里读 index 时
                             * 就抛 `ReferenceError: index is not defined`，
                             * 于是「勾上应用」这一步永远写不进配置：孩子点五下，
                             * 日志五行警告，桌面永远是空的。
                             * 声明成 required 之后由视图注入，和角色属性一样可靠。
                             */
                            required property int index

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
                                            // 用行号回查清单，而不是自己拼 map：
                                            // 磁贴底色（accent）是 AppCatalog 按 id
                                            // 算出来的，自己拼会丢掉这个字段。
                                            const item = AppCatalog.get(index);
                                            if (item && item.id) {
                                                Settings.addApp(item);
                                            } else {
                                                console.warn("[onboard] 取不到应用信息，行号", index);
                                            }
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
                        text: Settings.t("onboard.start")
                        onClicked: {
                            // 只写配置。盖层由 Main.qml 监听 Settings.changed
                            // 卸掉——这里不碰 StackView（见 Main.qml 顶部注释）。
                            //
                            // 两行日志是为了排障：这一步一旦出问题，日志停在
                            // 哪一行就能分清「写配置炸了」还是「卸盖层炸了」。
                            console.log("[onboard] 点「开始使用」，写入 onboardingDone");
                            Settings.onboardingDone = true;
                            console.log("[onboard] 配置已写入，等 Main.qml 收盖层");
                        }
                    }
                }
            }
        }
    }

    Component.onCompleted: AppCatalog.refresh()
}
