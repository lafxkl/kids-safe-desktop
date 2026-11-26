import QtQuick
import QtQuick.Controls
import QtQuick.Layouts
import KidsSafe 1.0

/*
 * 任务管理器。
 *
 * 数据源是 /proc（C++ 侧 ProcModel），每 2 秒刷一次。第一次打开时所有进程
 * 的 CPU 都是 0% —— 这是正常的：占用率要靠两次采样做差，第二拍才有数。
 * 界面上不解释这件事，孩子看到数字动起来就明白了。
 *
 * 三个刻意的设计：
 *
 *   1. **结束进程分两步**。先「请它退出」（SIGTERM），进程有机会存盘；
 *      点第二下才「强制结束」（SIGKILL）。孩子可能正在写作业，直接强杀会
 *      把文档留成半截。
 *   2. **有些进程不给结束**。pid 1、桌面自己、会话脚本、Xorg 一律连按钮
 *      都不显示（C++ 侧有名单）。孩子想「看看会发生什么」是本能，
 *      点错一次就黑屏的代价太高。
 *   3. **不显示命令行全文**，只在底部详情里给一行。整屏都是
 *      `/usr/lib/x86_64-linux-gnu/...` 对八九岁的孩子毫无意义，
 *      他需要的是「哪个东西在占我的电脑」。
 */
Item {
    id: root

    // 结束确认
    property int confirmPid: -1
    property string confirmName: ""
    property bool confirmGuarded: false

    // 底部详情
    property int selectedPid: -1

    property string toastText: ""
    property bool toastError: false

    function say(text, isError) {
        root.toastText = text;
        root.toastError = isError === true;
        toastTimer.restart();
    }

    function humanKb(kb) {
        if (kb >= 1048576) {
            return (kb / 1048576).toFixed(1) + " GB";
        }
        if (kb >= 1024) {
            return (kb / 1024).toFixed(0) + " MB";
        }
        return kb + " KB";
    }

    function cpuText(v) {
        // 小于 0.05% 一律显示 0.0，避免一屏「0.03」看着像噪声
        return (v < 0.05 ? "0.0" : v.toFixed(1)) + "%";
    }

    function stateLabel(s) {
        switch (s) {
        case "R": return "在跑";
        case "S": return "等着";
        case "D": return "卡住";
        case "T": return "暂停";
        case "Z": return "已结束";
        case "I": return "空闲";
        default:  return s;
        }
    }

    Component.onCompleted: {
        Processes.refresh();
        // running 一开，C++ 侧的定时器才开始跑；窗口关掉要记得关回去，
        // 否则关掉任务管理器它还在后台每 2 秒扫一遍 /proc。
        Processes.running = true;
    }

    Component.onDestruction: Processes.running = false

    Timer {
        id: toastTimer
        interval: 2800
        onTriggered: root.toastText = ""
    }

    ColumnLayout {
        anchors.fill: parent
        spacing: 0

        // ============================================================
        // 概览：四张卡片
        // ============================================================
        Rectangle {
            Layout.fillWidth: true
            Layout.preferredHeight: 96
            color: "#FAFBFD"

            RowLayout {
                anchors.fill: parent
                anchors.margins: 14
                spacing: 12

                // CPU
                Rectangle {
                    Layout.fillWidth: true
                    Layout.fillHeight: true
                    radius: 14
                    color: "#FFFFFF"
                    border.width: 1
                    border.color: "#EEF1F5"

                    ColumnLayout {
                        anchors.fill: parent
                        anchors.margins: 14
                        spacing: 2

                        Text {
                            text: "电脑有多忙"
                            font.pixelSize: 12
                            color: "#93A1B0"
                        }

                        Text {
                            text: root.cpuText(Processes.cpuPercent)
                            font.pixelSize: 22
                            font.weight: Font.Bold
                            color: "#3D4A57"
                        }

                        // 一条细进度条：数字之外还要有「长度」的直观感受
                        Rectangle {
                            Layout.fillWidth: true
                            Layout.preferredHeight: 5
                            radius: 3
                            color: "#EDF1F6"

                            Rectangle {
                                width: parent.width * Math.min(1, Processes.cpuPercent / 100)
                                height: parent.height
                                radius: 3
                                color: Processes.cpuPercent > 80 ? "#E05252"
                                     : (Processes.cpuPercent > 50 ? "#F0A83C" : "#3B82F6")

                                Behavior on width {
                                    NumberAnimation { duration: 400 }
                                }
                            }
                        }
                    }
                }

                // 内存
                Rectangle {
                    Layout.fillWidth: true
                    Layout.fillHeight: true
                    radius: 14
                    color: "#FFFFFF"
                    border.width: 1
                    border.color: "#EEF1F5"

                    ColumnLayout {
                        anchors.fill: parent
                        anchors.margins: 14
                        spacing: 2

                        Text {
                            text: "记住的东西"
                            font.pixelSize: 12
                            color: "#93A1B0"
                        }

                        Text {
                            text: root.humanKb(Processes.memUsedKb) + " / " + root.humanKb(Processes.memTotalKb)
                            font.pixelSize: 18
                            font.weight: Font.Bold
                            color: "#3D4A57"
                        }

                        Rectangle {
                            Layout.fillWidth: true
                            Layout.preferredHeight: 5
                            radius: 3
                            color: "#EDF1F6"

                            Rectangle {
                                width: parent.width * Math.min(1, Processes.memPercent / 100)
                                height: parent.height
                                radius: 3
                                color: Processes.memPercent > 85 ? "#E05252"
                                     : (Processes.memPercent > 65 ? "#F0A83C" : "#8B5CF6")

                                Behavior on width {
                                    NumberAnimation { duration: 400 }
                                }
                            }
                        }
                    }
                }

                // 进程数
                Rectangle {
                    Layout.preferredWidth: 150
                    Layout.fillHeight: true
                    radius: 14
                    color: "#FFFFFF"
                    border.width: 1
                    border.color: "#EEF1F5"

                    ColumnLayout {
                        anchors.fill: parent
                        anchors.margins: 14
                        spacing: 2

                        Text {
                            text: "开着多少个东西"
                            font.pixelSize: 12
                            color: "#93A1B0"
                        }

                        Text {
                            text: Processes.procTotal + " 个"
                            font.pixelSize: 22
                            font.weight: Font.Bold
                            color: "#3D4A57"
                        }
                    }
                }

                // 开机多久
                Rectangle {
                    Layout.preferredWidth: 150
                    Layout.fillHeight: true
                    radius: 14
                    color: "#FFFFFF"
                    border.width: 1
                    border.color: "#EEF1F5"

                    ColumnLayout {
                        anchors.fill: parent
                        anchors.margins: 14
                        spacing: 2

                        Text {
                            text: "电脑醒着多久了"
                            font.pixelSize: 12
                            color: "#93A1B0"
                        }

                        Text {
                            text: Processes.uptimeText
                            font.pixelSize: 18
                            font.weight: Font.Bold
                            color: "#3D4A57"
                        }
                    }
                }
            }
        }

        // ============================================================
        // 搜索 + 表头
        // ============================================================
        Rectangle {
            Layout.fillWidth: true
            Layout.preferredHeight: 52
            color: "#FFFFFF"

            RowLayout {
                anchors.fill: parent
                anchors.leftMargin: 16
                anchors.rightMargin: 16
                spacing: 12

                Rectangle {
                    Layout.preferredWidth: 240
                    Layout.preferredHeight: 36
                    radius: 11
                    color: "#F5F7FA"
                    border.width: 1
                    border.color: searchField.activeFocus ? "#BBD3F2" : "#EDF0F5"

                    RowLayout {
                        anchors.fill: parent
                        anchors.leftMargin: 10
                        anchors.rightMargin: 8
                        spacing: 6

                        GlyphIcon {
                            Layout.preferredWidth: 14
                            Layout.preferredHeight: 14
                            name: "search"
                            color: "#98A6B5"
                        }

                        TextField {
                            id: searchField

                            Layout.fillWidth: true
                            placeholderText: "找哪个程序"
                            font.pixelSize: 13
                            color: "#3D4A57"
                            placeholderTextColor: "#AAB6C4"
                            background: null
                            padding: 0
                            onTextChanged: filterDebounce.restart()
                        }
                    }
                }

                Item { Layout.fillWidth: true }

                Text {
                    text: root.toastText
                    font.pixelSize: 12
                    color: root.toastError ? "#D05252" : "#2E9E6B"
                }

                // 手动刷新
                Rectangle {
                    Layout.preferredWidth: 36
                    Layout.preferredHeight: 34
                    radius: 10
                    color: refreshHover.hovered ? "#EEF2F7" : "transparent"

                    GlyphIcon {
                        anchors.centerIn: parent
                        name: "refresh"
                        width: 15
                        height: 15
                        color: "#5B6B7C"
                    }

                    HoverHandler { id: refreshHover }

                    TapHandler {
                        onTapped: Processes.refresh()
                    }
                }
            }
        }

        // 表头
        Rectangle {
            Layout.fillWidth: true
            Layout.preferredHeight: 30
            color: "#FFFFFF"

            Rectangle {
                anchors.bottom: parent.bottom
                anchors.left: parent.left
                anchors.right: parent.right
                height: 1
                color: "#EEF1F5"
            }

            Row {
                anchors.fill: parent
                anchors.leftMargin: 18
                anchors.rightMargin: 18

                Text {
                    width: parent.width - 454
                    anchors.verticalCenter: parent.verticalCenter
                    text: "名字"
                    font.pixelSize: 12
                    font.weight: Font.DemiBold
                    color: "#9AA8B7"
                }
                Text {
                    width: 74
                    anchors.verticalCenter: parent.verticalCenter
                    text: "编号"
                    font.pixelSize: 12
                    font.weight: Font.DemiBold
                    color: "#9AA8B7"
                }
                Text {
                    width: 92
                    anchors.verticalCenter: parent.verticalCenter
                    text: "谁在跑"
                    font.pixelSize: 12
                    font.weight: Font.DemiBold
                    color: "#9AA8B7"
                }
                Text {
                    width: 74
                    anchors.verticalCenter: parent.verticalCenter
                    text: "忙不忙"
                    font.pixelSize: 12
                    font.weight: Font.DemiBold
                    color: "#9AA8B7"
                }
                Text {
                    width: 84
                    anchors.verticalCenter: parent.verticalCenter
                    text: "占内存"
                    font.pixelSize: 12
                    font.weight: Font.DemiBold
                    color: "#9AA8B7"
                }
                Text {
                    width: 56
                    anchors.verticalCenter: parent.verticalCenter
                    text: "状态"
                    font.pixelSize: 12
                    font.weight: Font.DemiBold
                    color: "#9AA8B7"
                }

                // 右侧 74px 留给悬停出现的「结束」按钮，表头这里空着，
                // 保证表头和行逐列对齐
                Item {
                    width: 74
                    height: 1
                }
            }
        }

        // ============================================================
        // 进程列表
        // ============================================================
        ListView {
            id: procList

            Layout.fillWidth: true
            Layout.fillHeight: true
            clip: true
            model: Processes
            boundsBehavior: Flickable.StopAtBounds
            cacheBuffer: 600

            ScrollBar.vertical: ScrollBar {
                policy: ScrollBar.AsNeeded
            }

            delegate: Rectangle {
                id: procRow

                required property int index
                required property int pid
                required property string name
                required property string cmd
                required property string user
                required property real cpu
                required property real mem
                required property string stateText
                required property bool guarded
                required property bool isSelf

                width: procList.width
                height: 40
                color: rowHover.hovered ? "#F5F8FC"
                     : (root.selectedPid === pid ? "#EEF4FD" : "transparent")

                Row {
                    anchors.fill: parent
                    anchors.leftMargin: 18
                    anchors.rightMargin: 18

                    // 名字（+ 自己 / 受保护的小标记）
                    Item {
                        width: parent.width - 454
                        height: parent.height

                        Row {
                            anchors.verticalCenter: parent.verticalCenter
                            spacing: 7

                            Text {
                                anchors.verticalCenter: parent.verticalCenter
                                text: procRow.name
                                font.pixelSize: 13
                                font.weight: procRow.cpu > 20 ? Font.DemiBold : Font.Normal
                                color: procRow.cpu > 20 ? "#2E7D32" : "#3D4A57"
                                elide: Text.ElideRight
                                width: Math.min(implicitWidth, parent.parent.width - 60)
                            }

                            Rectangle {
                                anchors.verticalCenter: parent.verticalCenter
                                visible: procRow.isSelf
                                width: 34
                                height: 16
                                radius: 8
                                color: "#E3F4EA"

                                Text {
                                    anchors.centerIn: parent
                                    text: "桌面"
                                    font.pixelSize: 10
                                    color: "#2E7D32"
                                }
                            }

                            Rectangle {
                                anchors.verticalCenter: parent.verticalCenter
                                visible: procRow.guarded && !procRow.isSelf
                                width: 42
                                height: 16
                                radius: 8
                                color: "#F2F4F7"

                                Text {
                                    anchors.centerIn: parent
                                    text: "别动"
                                    font.pixelSize: 10
                                    color: "#9AA8B7"
                                }
                            }
                        }
                    }

                    Text {
                        width: 74
                        height: parent.height
                        verticalAlignment: Text.AlignVCenter
                        text: procRow.pid
                        font.pixelSize: 12
                        font.family: "monospace"
                        color: "#93A1B0"
                    }

                    Text {
                        width: 92
                        height: parent.height
                        verticalAlignment: Text.AlignVCenter
                        text: procRow.user
                        font.pixelSize: 12
                        color: "#93A1B0"
                        elide: Text.ElideRight
                    }

                    Text {
                        width: 74
                        height: parent.height
                        verticalAlignment: Text.AlignVCenter
                        text: root.cpuText(procRow.cpu)
                        font.pixelSize: 12
                        color: procRow.cpu > 50 ? "#D05252" : "#5B6B7C"
                    }

                    Text {
                        width: 84
                        height: parent.height
                        verticalAlignment: Text.AlignVCenter
                        text: procRow.mem.toFixed(1) + "%"
                        font.pixelSize: 12
                        color: "#5B6B7C"
                    }

                    Text {
                        width: 56
                        height: parent.height
                        verticalAlignment: Text.AlignVCenter
                        text: root.stateLabel(procRow.stateText)
                        font.pixelSize: 12
                        color: "#93A1B0"
                    }
                }

                // 结束按钮。受保护的进程连按钮都不画 —— 灰掉的按钮仍然会被点，
                // 每次点都弹一句「不能结束」，反而让孩子反复试。
                Rectangle {
                    anchors.right: parent.right
                    anchors.rightMargin: 6
                    anchors.verticalCenter: parent.verticalCenter
                    width: 62
                    height: 26
                    radius: 8
                    visible: rowHover.hovered && !procRow.guarded
                    color: killHover.hovered ? "#FDECEC" : "#FFFFFF"
                    border.width: 1
                    border.color: "#F0E4E4"

                    Text {
                        anchors.centerIn: parent
                        text: "结束"
                        font.pixelSize: 12
                        color: "#D05252"
                    }

                    HoverHandler { id: killHover }

                    TapHandler {
                        onTapped: {
                            root.confirmPid = procRow.pid;
                            root.confirmName = procRow.name;
                            root.confirmGuarded = procRow.guarded;
                        }
                    }
                }

                HoverHandler {
                    id: rowHover
                }

                TapHandler {
                    onTapped: root.selectedPid = procRow.pid
                }
            }

            // 没匹配到
            Column {
                anchors.centerIn: parent
                spacing: 8
                visible: Processes.count === 0

                GlyphIcon {
                    anchors.horizontalCenter: parent.horizontalCenter
                    name: "search"
                    width: 38
                    height: 38
                    color: "#D3DBE4"
                }

                Text {
                    anchors.horizontalCenter: parent.horizontalCenter
                    text: Processes.filter.length > 0
                          ? "没有名字带「" + Processes.filter + "」的程序"
                          : "正在读进程…"
                    font.pixelSize: 13
                    color: "#A8B4C1"
                }
            }
        }

        // ============================================================
        // 底部：选中进程的命令行
        // ============================================================
        Rectangle {
            Layout.fillWidth: true
            Layout.preferredHeight: 34
            color: "#FAFBFD"

            Rectangle {
                anchors.top: parent.top
                anchors.left: parent.left
                anchors.right: parent.right
                height: 1
                color: "#EEF1F5"
            }

            Text {
                anchors.fill: parent
                anchors.leftMargin: 18
                anchors.rightMargin: 18
                verticalAlignment: Text.AlignVCenter
                text: {
                    if (root.selectedPid < 0) {
                        return "点一行可以看它到底是哪个程序";
                    }
                    const info = Processes.at(root.selectedPid);
                    return info && info.cmd !== undefined ? info.cmd : "";
                }
                font.pixelSize: 12
                font.family: "monospace"
                color: "#9AA8B7"
                elide: Text.ElideRight
            }
        }
    }

    Timer {
        id: filterDebounce
        interval: 200
        onTriggered: Processes.filter = searchField.text
    }

    // ============================================================
    // 结束确认
    // ============================================================
    Rectangle {
        anchors.fill: parent
        visible: root.confirmPid >= 0
        color: "#66000000"

        MouseArea {
            anchors.fill: parent
            onClicked: root.confirmPid = -1
        }

        Rectangle {
            anchors.centerIn: parent
            width: 430
            height: 210
            radius: 18
            color: "#FFFFFF"

            ColumnLayout {
                anchors.fill: parent
                anchors.margins: 20
                spacing: 10

                Text {
                    Layout.fillWidth: true
                    text: "要结束「" + root.confirmName + "」吗？"
                    font.pixelSize: 16
                    font.weight: Font.DemiBold
                    color: "#3D4A57"
                    elide: Text.ElideRight
                }

                Text {
                    Layout.fillWidth: true
                    text: "先点「请它退出」，它会自己收拾好再走。\n要是没反应，再强制结束。"
                    font.pixelSize: 12
                    lineHeight: 1.4
                    color: "#93A1B0"
                }

                Item { Layout.fillHeight: true }

                RowLayout {
                    Layout.fillWidth: true
                    spacing: 8

                    Rectangle {
                        Layout.preferredWidth: 76
                        Layout.preferredHeight: 38
                        radius: 11
                        color: cancelHover.hovered ? "#F1F4F8" : "#FFFFFF"
                        border.width: 1
                        border.color: "#E4E9F0"

                        Text {
                            anchors.centerIn: parent
                            text: "算了"
                            font.pixelSize: 13
                            color: "#5B6B7C"
                        }

                        HoverHandler { id: cancelHover }

                        TapHandler {
                            onTapped: root.confirmPid = -1
                        }
                    }

                    Item { Layout.fillWidth: true }

                    Rectangle {
                        Layout.preferredWidth: 104
                        Layout.preferredHeight: 38
                        radius: 11
                        color: gentleHover.hovered ? "#FDF3E6" : "#FFF9F0"
                        border.width: 1
                        border.color: "#F3E3CB"

                        Text {
                            anchors.centerIn: parent
                            text: "请它退出"
                            font.pixelSize: 13
                            font.weight: Font.DemiBold
                            color: "#B4761F"
                        }

                        HoverHandler { id: gentleHover }

                        TapHandler {
                            onTapped: {
                                const err = Processes.terminate(root.confirmPid, false);
                                root.say(err.length > 0 ? err
                                                        : "已经请它退出了，等两秒看看",
                                         err.length > 0);
                                root.confirmPid = -1;
                                refreshSoon.start();
                            }
                        }
                    }

                    Rectangle {
                        Layout.preferredWidth: 104
                        Layout.preferredHeight: 38
                        radius: 11
                        color: "#E05252"

                        Text {
                            anchors.centerIn: parent
                            text: "强制结束"
                            font.pixelSize: 13
                            font.weight: Font.DemiBold
                            color: "white"
                        }

                        TapHandler {
                            onTapped: {
                                const err = Processes.terminate(root.confirmPid, true);
                                root.say(err.length > 0 ? err : "已经强制结束了",
                                         err.length > 0);
                                root.confirmPid = -1;
                                refreshSoon.start();
                            }
                        }
                    }
                }
            }
        }
    }

    // 结束进程之后立刻刷一次，让那一行马上消失，而不是等 2 秒的定时器
    Timer {
        id: refreshSoon
        interval: 500
        onTriggered: Processes.refresh()
    }
}
