import QtQuick
import QtQuick.Controls
import QtQuick.Layouts
import QtQuick.Window
import KidsSafe 1.0

/*
 * 意见反馈 / 满意度调查 / 使用问卷（Linux）。
 *
 * 数据直接进官网（Feedback 单例 → POST /api/feedback/submit），
 * 与 Windows / 安卓 / 鸿蒙同一套接口、同一份后台数据。
 * 断网时 Feedback 会先写本机队列，联网后自动补传，页面只需显示状态。
 *
 * 配色说明：本页背景是 Backdrop 的暖米色（#FFF9F0 → #FFE3C4），
 * 全文用和 HomePage / ParentDashboardPage 同一套深棕字：
 *   #4A3B32 主文字 / #7A6759 正文 / #9B8574 次要 / #A9947F 占位
 * 白色只留给有色块（橙色按钮、选中项）上的字，别再往背景上直接压白字。
 */
Item {
    id: page

    readonly property var navWin: Window.window

    // 0 = 满意度，1 = 问卷，2 = 意见反馈
    property int tab: 0

    // 浅色主题下反复用到的几个颜色，集中在这里改一处就够
    readonly property color ink: "#4A3B32"
    readonly property color body: "#7A6759"
    readonly property color mute: "#8A7259"
    readonly property color line: "#E8D8C6"
    readonly property color accent: "#E2603D"
    readonly property color chipBg: "#FFFFFF"

    // ---- 满意度 ----
    property int rating: 0
    property int nps: -1
    property var dims: [
        { key: "ease", label: "好不好上手" },
        { key: "content", label: "内容合不合适" },
        { key: "stable", label: "稳不稳定" },
        { key: "design", label: "界面好不好看" },
        { key: "value", label: "值不值得推荐" }
    ]
    property var dimScores: ({})
    property var likeTags: ["护眼提醒", "小游戏", "故事屋", "学习内容", "家长管控", "日记本", "关爱中心", "开机自启"]
    property var pickedTags: ({})

    // ---- 问卷 ----
    property var surveyQs: [
        { id: "孩子年龄", title: "孩子现在多大？", options: ["3–5 岁", "6–8 岁", "9–12 岁", "12 岁以上"] },
        { id: "主要设备", title: "主要在什么设备上用？", options: ["Windows 电脑", "安卓平板", "安卓手机", "Linux", "鸿蒙"] },
        { id: "使用频率", title: "大概多久用一次？", options: ["几乎每天", "每周几次", "偶尔用", "刚装上"] },
        { id: "最常用", title: "孩子最常用哪一块？", options: ["小游戏", "学习/口算", "故事屋", "关爱中心", "家长管控", "日记本"] },
        { id: "最希望新增", title: "最希望我们下一步做什么？", options: ["更多课程", "更多小游戏", "学习报告", "多孩管理", "离线内容", "其他"] },
        { id: "愿意推荐吗", title: "愿意推荐给别的家长吗？", options: ["愿意", "看情况", "不愿意"] }
    ]
    property var surveyAnswers: ({})

    // ---- 意见反馈 ----
    property var fbCats: ["问题反馈", "功能建议", "内容纠错", "使用疑问", "其他"]
    property string fbCat: "问题反馈"

    // ---- 公共 ----
    property string satText: ""
    property string surveyText: ""
    property string fbText: ""
    property string contact: ""
    property bool allowPublic: false

    function answeredCount() {
        var n = 0;
        for (var i = 0; i < surveyQs.length; ++i) {
            if (surveyAnswers[surveyQs[i].id]) n++;
        }
        return n;
    }

    function pickedTagList() {
        var out = [];
        for (var i = 0; i < likeTags.length; ++i) {
            if (pickedTags[likeTags[i]]) out.push(likeTags[i]);
        }
        return out;
    }

    function submit() {
        if (Feedback.busy) return;
        if (tab === 0) {
            if (rating < 1) { page.toast("先点一下整体满意度（1–5 星）", true); return; }
            Feedback.submitSatisfaction(rating, nps, dimScores, pickedTagList(), satText, contact, allowPublic);
        } else if (tab === 1) {
            if (answeredCount() < 3) { page.toast("问卷至少选 3 题，我们才好判断", true); return; }
            Feedback.submitSurvey(surveyAnswers, surveyText, contact, allowPublic);
        } else {
            if (fbText.trim().length < 5) { page.toast("再多写几个字吧（至少 5 个字）", true); return; }
            Feedback.submitFeedback(fbCat, fbText, contact, allowPublic);
            fbText = "";
        }
    }

    // ---- 顶部提示条 ----
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

    // ---- 头部 ----
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
            color: backArea.containsMouse ? "#FFF3E6" : "#FFFFFF"
            border.width: 1
            border.color: page.line
            Text {
                id: backLabel
                anchors.centerIn: parent
                text: "← " + (Settings.english ? "Back" : "返回")
                font.pixelSize: 17
                color: page.body
            }
            MouseArea {
                id: backArea
                anchors.fill: parent
                hoverEnabled: true
                cursorShape: Qt.PointingHandCursor
                onClicked: { if (page.navWin) page.navWin.popToRoot(); }
            }
        }

        Text {
            Layout.fillWidth: true
            text: "💬 " + (Settings.english ? "Feedback" : "意见反馈与问卷")
            font.pixelSize: 26
            font.bold: true
            color: page.ink
        }

        Text {
            text: Feedback.pending > 0 ? (Settings.english ? Feedback.pending + " pending" : "待发 " + Feedback.pending + " 条") : ""
            font.pixelSize: 14
            color: "#B4552F"
        }
    }

    // ---- 主体 ----
    Flickable {
        anchors.left: parent.left
        anchors.right: parent.right
        anchors.top: header.bottom
        anchors.bottom: parent.bottom
        anchors.margins: 22
        clip: true
        contentHeight: body.implicitHeight + 24
        ScrollBar.vertical: ScrollBar {}

        ColumnLayout {
            id: body
            width: parent.width
            spacing: 14

            // 三个标签
            RowLayout {
                Layout.fillWidth: true
                spacing: 10
                Repeater {
                    model: [Settings.english ? "Satisfaction" : "满意度调查",
                            Settings.english ? "Survey" : "使用问卷",
                            Settings.english ? "Feedback" : "意见反馈"]
                    Rectangle {
                        Layout.fillWidth: true
                        Layout.preferredHeight: 44
                        radius: 14
                        color: page.tab === index ? page.accent : page.chipBg
                        border.width: 1
                        border.color: page.tab === index ? page.accent : page.line
                        Text {
                            anchors.centerIn: parent
                            text: modelData
                            font.pixelSize: 15
                            font.bold: true
                            color: page.tab === index ? "#FFFFFF" : page.body
                        }
                        MouseArea {
                            anchors.fill: parent
                            cursorShape: Qt.PointingHandCursor
                            onClicked: page.tab = index
                        }
                    }
                }
            }

            Text {
                Layout.fillWidth: true
                wrapMode: Text.WordWrap
                font.pixelSize: 14
                color: page.body
                text: page.tab === 0
                      ? "一分钟，帮我们把下一版做得更合适。只有整体满意度必填，其它可以跳过。"
                      : page.tab === 1
                        ? "6 道选择题，30 秒填完。我们靠它决定下一个版本先做什么。"
                        : "遇到问题、想要新功能、发现内容写错了，都可以直接说。不用注册，不用填孩子的任何信息。"
            }

            // ===== 满意度 =====
            ColumnLayout {
                Layout.fillWidth: true
                spacing: 10
                visible: page.tab === 0

                Text { text: "整体满意度（必填）"; font.pixelSize: 16; font.bold: true; color: page.ink }
                Row {
                    spacing: 4
                    Repeater {
                        model: 5
                        Text {
                            text: "★"
                            font.pixelSize: 36
                            color: page.rating >= index + 1 ? "#F2A03D" : "#E3D0BB"
                            MouseArea {
                                anchors.fill: parent
                                cursorShape: Qt.PointingHandCursor
                                onClicked: page.rating = index + 1
                            }
                        }
                    }
                }

                Text { text: "分项打分（可跳过）"; font.pixelSize: 16; font.bold: true; color: page.ink }
                Repeater {
                    model: page.dims
                    ColumnLayout {
                        // 外层 modelData 是 {key,label}；内层 Repeater 会把它盖掉，
                        // 所以先存一份，内层按钮点的时候才知道是哪个分项
                        property var dim: modelData

                        Layout.fillWidth: true
                        spacing: 4
                        Text { text: dim.label; font.pixelSize: 14; color: page.mute }
                        Row {
                            spacing: 6
                            Repeater {
                                model: 5
                                Rectangle {
                                    width: 54
                                    height: 34
                                    radius: 10
                                    color: page.dimScores[dim.key] === index + 1 ? page.accent : page.chipBg
                                    border.width: 1
                                    border.color: page.dimScores[dim.key] === index + 1 ? page.accent : page.line
                                    Text {
                                        anchors.centerIn: parent
                                        text: (index + 1) + " 分"
                                        font.pixelSize: 13
                                        color: page.dimScores[dim.key] === index + 1 ? "#FFFFFF" : page.body
                                    }
                                    MouseArea {
                                        anchors.fill: parent
                                        cursorShape: Qt.PointingHandCursor
                                        onClicked: {
                                            var m = Object.assign({}, page.dimScores);
                                            m[dim.key] = index + 1;
                                            page.dimScores = m;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                Text { text: "愿意推荐给别的家长吗？（可跳过，0 = 完全不会，10 = 一定推荐）"; font.pixelSize: 16; font.bold: true; color: page.ink }
                Flow {
                    Layout.fillWidth: true
                    spacing: 6
                    Repeater {
                        model: 11
                        Rectangle {
                            width: 40
                            height: 36
                            radius: 10
                            color: page.nps === index ? page.accent : page.chipBg
                            border.width: 1
                            border.color: page.nps === index ? page.accent : page.line
                            Text {
                                anchors.centerIn: parent
                                text: index
                                font.pixelSize: 14
                                font.bold: true
                                color: page.nps === index ? "#FFFFFF" : page.body
                            }
                            MouseArea {
                                anchors.fill: parent
                                cursorShape: Qt.PointingHandCursor
                                onClicked: page.nps = index
                            }
                        }
                    }
                }

                Text { text: "最喜欢哪一块？（可多选）"; font.pixelSize: 16; font.bold: true; color: page.ink }
                Flow {
                    Layout.fillWidth: true
                    spacing: 8
                    Repeater {
                        model: page.likeTags
                        Rectangle {
                            height: 34
                            width: chipText.implicitWidth + 26
                            radius: 12
                            color: page.pickedTags[modelData] ? page.accent : page.chipBg
                            border.width: 1
                            border.color: page.pickedTags[modelData] ? page.accent : page.line
                            Text {
                                id: chipText
                                anchors.centerIn: parent
                                text: modelData
                                font.pixelSize: 13
                                color: page.pickedTags[modelData] ? "#FFFFFF" : page.body
                            }
                            MouseArea {
                                anchors.fill: parent
                                cursorShape: Qt.PointingHandCursor
                                onClicked: {
                                    var m = page.pickedTags;
                                    m[modelData] = !m[modelData];
                                    page.pickedTags = Object.assign({}, m);
                                }
                            }
                        }
                    }
                }

                TextInputBox {
                    Layout.fillWidth: true
                    Layout.preferredHeight: 110
                    multiline: true
                    placeholder: "还有什么想说的？（选填）"
                    text: page.satText
                    onEdited: page.satText = content
                }
            }

            // ===== 问卷 =====
            ColumnLayout {
                Layout.fillWidth: true
                spacing: 10
                visible: page.tab === 1

                Text {
                    text: "已答 " + page.answeredCount() + " / " + page.surveyQs.length + " 题（至少 3 题）"
                    font.pixelSize: 14
                    font.bold: true
                    color: "#B4552F"
                }

                Repeater {
                    model: page.surveyQs
                    ColumnLayout {
                        // 同上：题目对象存一份，内层选项才拿得到题号
                        property var q: modelData

                        Layout.fillWidth: true
                        spacing: 6
                        Text { text: q.title; font.pixelSize: 16; font.bold: true; color: page.ink }
                        Flow {
                            Layout.fillWidth: true
                            spacing: 8
                            Repeater {
                                model: q.options
                                Rectangle {
                                    height: 34
                                    width: optText.implicitWidth + 26
                                    radius: 12
                                    color: page.surveyAnswers[q.id] === modelData ? page.accent : page.chipBg
                                    border.width: 1
                                    border.color: page.surveyAnswers[q.id] === modelData ? page.accent : page.line
                                    Text {
                                        id: optText
                                        anchors.centerIn: parent
                                        text: modelData
                                        font.pixelSize: 13
                                        color: page.surveyAnswers[q.id] === modelData ? "#FFFFFF" : page.body
                                    }
                                    MouseArea {
                                        anchors.fill: parent
                                        cursorShape: Qt.PointingHandCursor
                                        onClicked: {
                                            var copy = Object.assign({}, page.surveyAnswers);
                                            copy[q.id] = modelData;
                                            page.surveyAnswers = copy;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                TextInputBox {
                    Layout.fillWidth: true
                    Layout.preferredHeight: 100
                    multiline: true
                    placeholder: "其他想补充的（选填）"
                    text: page.surveyText
                    onEdited: page.surveyText = content
                }
            }

            // ===== 意见反馈 =====
            ColumnLayout {
                Layout.fillWidth: true
                spacing: 10
                visible: page.tab === 2

                Text { text: "这是什么类型？"; font.pixelSize: 16; font.bold: true; color: page.ink }
                Flow {
                    Layout.fillWidth: true
                    spacing: 8
                    Repeater {
                        model: page.fbCats
                        Rectangle {
                            height: 34
                            width: fbText.implicitWidth + 26
                            radius: 12
                            color: page.fbCat === modelData ? page.accent : page.chipBg
                            border.width: 1
                            border.color: page.fbCat === modelData ? page.accent : page.line
                            Text {
                                id: fbText
                                anchors.centerIn: parent
                                text: modelData
                                font.pixelSize: 13
                                color: page.fbCat === modelData ? "#FFFFFF" : page.body
                            }
                            MouseArea {
                                anchors.fill: parent
                                cursorShape: Qt.PointingHandCursor
                                onClicked: page.fbCat = modelData
                            }
                        }
                    }
                }

                TextInputBox {
                    Layout.fillWidth: true
                    Layout.preferredHeight: 150
                    multiline: true
                    placeholder: "例如：安卓平板上「宝宝听」播到一半切后台回来会从头开始。设备是荣耀 Pad 8，版本 1.20。"
                    text: page.fbText
                    onEdited: page.fbText = content
                }
            }

            // ===== 公共字段 =====
            TextInputBox {
                Layout.fillWidth: true
                Layout.preferredHeight: 48
                placeholder: "联系方式（选填，只有开发者能看到）"
                text: page.contact
                onEdited: page.contact = content
            }

            RowLayout {
                Layout.fillWidth: true
                spacing: 10
                Text {
                    Layout.fillWidth: true
                    wrapMode: Text.WordWrap
                    text: "允许匿名展示在官网（只显示正文、星级和月份）"
                    font.pixelSize: 13
                    color: page.body
                }

                // 自制开关：不依赖 Controls 样式，配色和页面一致
                Rectangle {
                    id: publicSwitch

                    Layout.preferredWidth: 46
                    Layout.preferredHeight: 26
                    radius: 13
                    color: page.allowPublic ? page.accent : "#EADFD0"

                    Rectangle {
                        width: 20
                        height: 20
                        radius: 10
                        anchors.verticalCenter: parent.verticalCenter
                        x: page.allowPublic ? parent.width - width - 3 : 3
                        color: "#FFFFFF"

                        Behavior on x { NumberAnimation { duration: 120 } }
                    }

                    MouseArea {
                        anchors.fill: parent
                        cursorShape: Qt.PointingHandCursor
                        onClicked: page.allowPublic = !page.allowPublic
                    }
                }
            }

            Rectangle {
                Layout.fillWidth: true
                Layout.preferredHeight: 52
                radius: 26
                color: Feedback.busy ? "#F0A088" : page.accent
                Text {
                    anchors.centerIn: parent
                    text: Feedback.busy ? "提交中…"
                          : (page.tab === 0 ? "提交满意度" : page.tab === 1 ? "提交问卷" : "提交反馈")
                    font.pixelSize: 16
                    font.bold: true
                    color: "#FFFFFF"
                }
                MouseArea {
                    anchors.fill: parent
                    cursorShape: Qt.PointingHandCursor
                    onClicked: page.submit()
                }
            }

            Text {
                Layout.fillWidth: true
                wrapMode: Text.WordWrap
                visible: Feedback.status.length > 0
                text: Feedback.status
                font.pixelSize: 14
                color: "#2E7D32"
            }

            Text {
                Layout.fillWidth: true
                wrapMode: Text.WordWrap
                font.pixelSize: 12
                color: "#8A7259"
                text: "提交后立刻计入官网「反馈与问卷」页的公开统计；不收集身份信息，需要删除时发邮件到 2279125469@qq.com。"
            }
        }
    }

    /*
     * 校验提示 / 提交结果。浮在页面底部，不在滚动内容里，
     * 用户点了提交就能看到，不用往回滚。浅底深字，配白卡底才读得出来。
     */
    Rectangle {
        id: toastLabel

        anchors.horizontalCenter: parent.horizontalCenter
        anchors.bottom: parent.bottom
        anchors.bottomMargin: 18
        z: 10

        visible: page.toastText.length > 0
        width: toastText.implicitWidth + 36
        height: 40
        radius: 20
        color: page.toastBad ? "#FFF1EC" : "#EDF7EE"
        border.width: 1
        border.color: page.toastBad ? "#F0BFAF" : "#BFDCC3"

        Text {
            id: toastText

            anchors.centerIn: parent
            text: page.toastText
            font.pixelSize: 14
            font.bold: true
            color: page.toastBad ? "#B73B2A" : "#2E6B3A"
        }
    }
}
