import QtQuick
import QtQuick.Controls
import QtQuick.Layouts
import KidsSafe 1.0

/*
 * 文件管理器。
 *
 * 定位是「孩子能安全地翻自己的东西」，不是 Nautilus 的替代品。所以：
 *
 *   - 只做一层目录，没有左侧目录树。孩子的心智模型是「进去、返回、再进去」，
 *     树形展开状态、懒加载、跨目录刷新这些成本换不来任何儿童可用的功能。
 *   - 删除进回收站（freedesktop 规范的 ~/.local/share/Trash），删错能捞回来。
 *   - 系统目录只读：能进 /usr 看，但改名/删除/新建一律拒绝并给一句解释。
 *     这条在 C++ 侧（FileModel::isProtected）兜底，界面只是把按钮灰掉。
 *   - 单击文件夹就进去，单击文件就地预览。不做双击 —— 七八岁的手速下
 *     双击丢事件的概率很高，孩子只会觉得「点了没反应」。
 *
 * 界面分四段：工具条（上级 / 面包屑 / 搜索 / 新建）、左侧常用位置、
 * 中间文件列表、底部状态条。列表行的「改名 / 删除」按钮在悬停时才出现，
 * 平时不占地方也不误触。
 */
Item {
    id: root

    // 当前悬停/选中的行，操作按钮要用
    property int hoverRow: -1

    // ---- 新建 / 改名 共用的小弹窗 ----
    property string dialogMode: ""          // "" | "mkdir" | "rename"
    property int dialogRow: -1
    property string dialogTitle: ""

    // ---- 删除确认 ----
    property int confirmRow: -1

    // ---- 预览 ----
    property bool previewing: false
    property bool previewIsImage: false
    property string previewText: ""
    property string previewPath: ""
    property string previewName: ""

    // ---- 底部一句话提示 ----
    property string toastText: ""
    property bool toastError: false

    function say(text, isError) {
        root.toastText = text;
        root.toastError = isError === true;
        toastTimer.restart();
    }

    // 换了目录就把搜索框清掉。否则「在 A 目录搜到 X、进 B 目录却什么都看不到」，
    // 孩子会以为文件丢了。
    function navigateTo(path) {
        const err = Files.navigate(path);
        Files.filter = "";
        search.text = "";
        if (err.length > 0) {
            root.say(err, true);
        }
        root.hoverRow = -1;
        root.closePreview();
    }

    function closePreview() {
        root.previewing = false;
        root.previewText = "";
        root.previewPath = "";
        root.previewName = "";
    }

    function openRow(row) {
        const kindResult = Files.open(row);

        if (kindResult === "dir") {
            // 已经进去了：清掉搜索与预览状态
            Files.filter = "";
            search.text = "";
            root.closePreview();
            root.hoverRow = -1;
            return;
        }
        if (kindResult === "text") {
            root.previewIsImage = false;
            // 第二个参数必须显式给：Q_INVOKABLE 的默认参数在 QML 里不生效，
            // 少传一个就是 Insufficient arguments，界面上表现为点了没反应。
            root.previewText = Files.preview(row, 200000);
            root.previewPath = "";
            root.previewName = rowName(row);
            root.previewing = true;
            return;
        }
        if (kindResult === "image") {
            root.previewIsImage = true;
            root.previewText = "";
            root.previewPath = rowPath(row);
            root.previewName = rowName(row);
            root.previewing = true;
            return;
        }
        if (kindResult === "launch") {
            root.say("交给系统里的程序打开了", false);
            return;
        }
        root.say("这个文件这里打不开，要请家长帮忙", true);
    }

    // 行号 → 名字 / 路径。自定义模型没有 ListModel 的 get()，
    // 走 C++ 侧的 at()（越界返回空 map，所以这里要兜一层）。
    function rowName(row) {
        const item = Files.at(row);
        return item && item.name !== undefined ? item.name : "";
    }

    function rowPath(row) {
        const item = Files.at(row);
        return item && item.path !== undefined ? item.path : "";
    }

    function glyphFor(kind, isDir) {
        if (isDir) {
            return "folder";
        }
        if (kind === "text") {
            return "doc";
        }
        if (kind === "image") {
            return "picture";
        }
        if (kind === "audio") {
            return "music";
        }
        if (kind === "video") {
            return "monitor";
        }
        return "doc";
    }

    function placeGlyph(key) {
        switch (key) {
        case "home":      return "home";
        case "desktop":   return "monitor";
        case "docs":      return "doc";
        case "pictures":  return "picture";
        case "download":  return "download";
        case "root":      return "disk";
        default:          return "folder";
        }
    }

    Component.onCompleted: Files.refresh()

    Timer {
        id: toastTimer
        interval: 2600
        onTriggered: root.toastText = ""
    }

    // 搜索防抖：每敲一个字母重扫一次目录，在几百个文件的目录里会明显卡顿
    Timer {
        id: searchDebounce
        interval: 220
        onTriggered: Files.filter = search.text
    }

    ColumnLayout {
        anchors.fill: parent
        spacing: 0

        // ============================================================
        // 工具条
        // ============================================================
        Rectangle {
            Layout.fillWidth: true
            Layout.preferredHeight: 56
            color: "#FFFFFF"

            Rectangle {
                anchors.bottom: parent.bottom
                anchors.left: parent.left
                anchors.right: parent.right
                height: 1
                color: "#EEF1F5"
            }

            RowLayout {
                anchors.fill: parent
                anchors.leftMargin: 14
                anchors.rightMargin: 14
                spacing: 8

                // 上一级
                Rectangle {
                    Layout.preferredWidth: 38
                    Layout.preferredHeight: 38
                    radius: 11
                    color: upArea.active ? "#EEF2F7" : "transparent"
                    opacity: Files.canGoUp ? 1 : 0.4

                    GlyphIcon {
                        anchors.centerIn: parent
                        name: "up"
                        width: 17
                        height: 17
                        color: "#5B6B7C"
                    }

                    HoverHandler { id: upArea }

                    TapHandler {
                        onTapped: {
                            if (Files.canGoUp) {
                                root.navigateTo(Files.goUp());
                            }
                        }
                    }
                }

                // 面包屑：横向可滚，路径深了也不会挤爆工具条
                Flickable {
                    Layout.fillWidth: true
                    Layout.preferredHeight: 38
                    clip: true
                    contentWidth: crumbRow.implicitWidth
                    contentHeight: height
                    boundsBehavior: Flickable.StopAtBounds

                    Row {
                        id: crumbRow
                        height: parent.height
                        spacing: 2

                        Repeater {
                            model: Files.crumbs

                            Rectangle {
                                required property var modelData
                                required property int index

                                height: 30
                                anchors.verticalCenter: parent.verticalCenter
                                width: crumbText.implicitWidth + 18
                                radius: 9
                                // 最后一段是当前目录，用实心强调一下
                                color: index === Files.crumbs.length - 1
                                       ? "#EDF2FA" : (crumbHover.hovered ? "#F4F6F9" : "transparent")

                                Text {
                                    id: crumbText
                                    anchors.centerIn: parent
                                    text: modelData.name
                                    font.pixelSize: 13
                                    font.weight: index === Files.crumbs.length - 1
                                                 ? Font.DemiBold : Font.Normal
                                    color: "#48586A"
                                }

                                HoverHandler { id: crumbHover }

                                TapHandler {
                                    onTapped: root.navigateTo(modelData.path)
                                }
                            }
                        }
                    }
                }

                // 搜索
                Rectangle {
                    Layout.preferredWidth: 190
                    Layout.preferredHeight: 36
                    radius: 11
                    color: "#F5F7FA"
                    border.width: 1
                    border.color: search.activeFocus ? "#BBD3F2" : "#EDF0F5"

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
                            id: search

                            Layout.fillWidth: true
                            placeholderText: "找文件"
                            font.pixelSize: 13
                            color: "#3D4A57"
                            placeholderTextColor: "#AAB6C4"
                            background: null
                            padding: 0
                            onTextChanged: searchDebounce.restart()
                        }
                    }
                }

                // 新建文件夹
                Rectangle {
                    Layout.preferredWidth: 38
                    Layout.preferredHeight: 38
                    radius: 11
                    color: newArea.active ? "#EEF2F7" : "transparent"
                    opacity: Files.writable ? 1 : 0.35

                    GlyphIcon {
                        anchors.centerIn: parent
                        name: "plus"
                        width: 16
                        height: 16
                        color: "#5B6B7C"
                    }

                    HoverHandler { id: newArea }

                    TapHandler {
                        onTapped: {
                            if (!Files.writable) {
                                root.say(Files.guardReason(), true);
                                return;
                            }
                            root.dialogMode = "mkdir";
                            root.dialogRow = -1;
                            root.dialogTitle = "新建文件夹";
                            dialogField.text = "";
                            dialog.open();
                        }
                    }
                }

                // 显示隐藏文件
                Rectangle {
                    Layout.preferredWidth: 38
                    Layout.preferredHeight: 38
                    radius: 11
                    color: Files.showHidden ? "#EDF2FA" : (eyeArea.active ? "#EEF2F7" : "transparent")

                    GlyphIcon {
                        anchors.centerIn: parent
                        name: "eye"
                        width: 17
                        height: 17
                        color: Files.showHidden ? "#3B82F6" : "#5B6B7C"
                    }

                    HoverHandler { id: eyeArea }

                    TapHandler {
                        onTapped: Files.showHidden = !Files.showHidden
                    }
                }
            }
        }

        // ============================================================
        // 主体：常用位置 + 文件列表
        // ============================================================
        RowLayout {
            Layout.fillWidth: true
            Layout.fillHeight: true
            spacing: 0

            // ---- 左：常用位置 ----
            Rectangle {
                Layout.preferredWidth: 176
                Layout.fillHeight: true
                color: "#FAFBFD"

                Rectangle {
                    anchors.right: parent.right
                    anchors.top: parent.top
                    anchors.bottom: parent.bottom
                    width: 1
                    color: "#EEF1F5"
                }

                Column {
                    anchors.top: parent.top
                    anchors.left: parent.left
                    anchors.right: parent.right
                    anchors.topMargin: 12
                    spacing: 2

                    Repeater {
                        model: Files.places

                        Rectangle {
                            required property var modelData

                            width: parent.width - 16
                            x: 8
                            height: 38
                            radius: 10
                            color: placeHover.hovered ? "#EDF2FA" : "transparent"

                            Row {
                                anchors.verticalCenter: parent.verticalCenter
                                anchors.left: parent.left
                                anchors.leftMargin: 10
                                spacing: 9

                                GlyphIcon {
                                    anchors.verticalCenter: parent.verticalCenter
                                    name: root.placeGlyph(modelData.glyph)
                                    width: 16
                                    height: 16
                                    color: "#5B6B7C"
                                }

                                Text {
                                    anchors.verticalCenter: parent.verticalCenter
                                    text: modelData.name
                                    font.pixelSize: 13
                                    color: "#48586A"
                                }
                            }

                            HoverHandler { id: placeHover }

                            TapHandler {
                                onTapped: root.navigateTo(modelData.path)
                            }
                        }
                    }
                }
            }

            // ---- 右：文件列表 ----
            Item {
                Layout.fillWidth: true
                Layout.fillHeight: true

                // 表头
                Rectangle {
                    id: listHeader

                    anchors.top: parent.top
                    anchors.left: parent.left
                    anchors.right: parent.right
                    height: 30
                    color: "#FFFFFF"

                    Row {
                        anchors.fill: parent
                        anchors.leftMargin: 18
                        anchors.rightMargin: 18

                        Text {
                            width: parent.width - 286
                            anchors.verticalCenter: parent.verticalCenter
                            text: "名字"
                            font.pixelSize: 12
                            font.weight: Font.DemiBold
                            color: "#9AA8B7"
                        }

                        Text {
                            width: 110
                            anchors.verticalCenter: parent.verticalCenter
                            text: "大小"
                            font.pixelSize: 12
                            font.weight: Font.DemiBold
                            color: "#9AA8B7"
                        }

                        Text {
                            width: 100
                            anchors.verticalCenter: parent.verticalCenter
                            text: "修改时间"
                            font.pixelSize: 12
                            font.weight: Font.DemiBold
                            color: "#9AA8B7"
                        }

                        // 右侧 76px 是给悬停操作按钮留的，表头这里空着，
                        // 保证表头和下面的行逐列对齐
                        Item {
                            width: 76
                            height: 1
                        }
                    }

                    Rectangle {
                        anchors.bottom: parent.bottom
                        anchors.left: parent.left
                        anchors.right: parent.right
                        height: 1
                        color: "#F0F3F7"
                    }
                }

                ListView {
                    id: fileList

                    anchors.top: listHeader.bottom
                    anchors.left: parent.left
                    anchors.right: parent.right
                    anchors.bottom: parent.bottom
                    clip: true
                    model: Files
                    boundsBehavior: Flickable.StopAtBounds
                    // 一次显示约 10 行，滚一格就是一整行，不会停在半行上
                    cacheBuffer: 800

                    ScrollBar.vertical: ScrollBar {
                        policy: ScrollBar.AsNeeded
                    }

                    delegate: Rectangle {
                        id: fileRow

                        required property int index
                        required property string name
                        required property bool isDir
                        required property string size
                        required property string mtime
                        required property string kind

                        width: fileList.width
                        height: 44
                        color: rowHover.hovered ? "#F5F8FC" : "transparent"

                        Row {
                            anchors.fill: parent
                            anchors.leftMargin: 18
                            anchors.rightMargin: 18

                            // 图标 + 名字
                            Item {
                                width: parent.width - 286
                                height: parent.height

                                Row {
                                    anchors.verticalCenter: parent.verticalCenter
                                    spacing: 10

                                    GlyphIcon {
                                        anchors.verticalCenter: parent.verticalCenter
                                        name: root.glyphFor(fileRow.kind, fileRow.isDir)
                                        width: 20
                                        height: 20
                                        color: fileRow.isDir ? "#3B82F6" : "#8A98A8"
                                    }

                                    Text {
                                        anchors.verticalCenter: parent.verticalCenter
                                        text: fileRow.name
                                        font.pixelSize: 13.5
                                        color: "#3D4A57"
                                        elide: Text.ElideMiddle
                                        width: Math.min(implicitWidth, parent.width - 30)
                                    }
                                }
                            }

                            Text {
                                width: 110
                                height: parent.height
                                verticalAlignment: Text.AlignVCenter
                                text: fileRow.size
                                font.pixelSize: 12.5
                                color: "#93A1B0"
                            }

                            Text {
                                width: 100
                                height: parent.height
                                verticalAlignment: Text.AlignVCenter
                                text: fileRow.mtime
                                font.pixelSize: 12.5
                                color: "#93A1B0"
                            }
                        }

                        // 悬停时浮出的两个操作。做成小按钮而不是右键菜单：
                        // 儿童很少会想到「右键」，而这两个动作正是他最需要的。
                        Row {
                            anchors.right: parent.right
                            anchors.rightMargin: 6
                            anchors.verticalCenter: parent.verticalCenter
                            spacing: 4
                            visible: rowHover.hovered && Files.canModify(fileRow.index)

                            Rectangle {
                                width: 30
                                height: 30
                                radius: 9
                                color: renameHover.hovered ? "#E8EFF9" : "#FFFFFF"
                                border.width: 1
                                border.color: "#E7ECF3"

                                GlyphIcon {
                                    anchors.centerIn: parent
                                    name: "pencil"
                                    width: 14
                                    height: 14
                                    color: "#5B6B7C"
                                }

                                HoverHandler { id: renameHover }

                                TapHandler {
                                    onTapped: {
                                        root.dialogMode = "rename";
                                        root.dialogRow = fileRow.index;
                                        root.dialogTitle = "给「" + fileRow.name + "」改个名字";
                                        dialogField.text = fileRow.name;
                                        dialog.open();
                                    }
                                }
                            }

                            Rectangle {
                                width: 30
                                height: 30
                                radius: 9
                                color: deleteHover.hovered ? "#FDECEC" : "#FFFFFF"
                                border.width: 1
                                border.color: "#F0E4E4"

                                GlyphIcon {
                                    anchors.centerIn: parent
                                    name: "trash"
                                    width: 14
                                    height: 14
                                    color: "#D05252"
                                }

                                HoverHandler { id: deleteHover }

                                TapHandler {
                                    onTapped: root.confirmRow = fileRow.index
                                }
                            }
                        }

                        HoverHandler {
                            id: rowHover
                            onHoveredChanged: {
                                if (hovered) {
                                    root.hoverRow = fileRow.index;
                                } else if (root.hoverRow === fileRow.index) {
                                    root.hoverRow = -1;
                                }
                            }
                        }

                        TapHandler {
                            onTapped: root.openRow(fileRow.index)
                        }
                    }
                }

                // 空目录 / 搜不到
                Column {
                    anchors.centerIn: parent
                    spacing: 8
                    visible: Files.count === 0

                    GlyphIcon {
                        anchors.horizontalCenter: parent.horizontalCenter
                        name: Files.filter.length > 0 ? "search" : "folder"
                        width: 40
                        height: 40
                        color: "#D3DBE4"
                    }

                    Text {
                        anchors.horizontalCenter: parent.horizontalCenter
                        text: Files.filter.length > 0
                              ? "这个文件夹里没有名字带「" + Files.filter + "」的东西"
                              : "这个文件夹是空的"
                        font.pixelSize: 13
                        color: "#A8B4C1"
                    }
                }

                // ---- 预览层 ----
                Rectangle {
                    anchors.fill: parent
                    visible: root.previewing
                    color: "#FFFFFF"

                    ColumnLayout {
                        anchors.fill: parent
                        spacing: 0

                        RowLayout {
                            Layout.fillWidth: true
                            Layout.preferredHeight: 46
                            Layout.leftMargin: 16
                            Layout.rightMargin: 10
                            spacing: 8

                            Text {
                                Layout.fillWidth: true
                                text: root.previewName
                                font.pixelSize: 13.5
                                font.weight: Font.DemiBold
                                color: "#3D4A57"
                                elide: Text.ElideMiddle
                            }

                            Rectangle {
                                Layout.preferredWidth: 30
                                Layout.preferredHeight: 30
                                radius: 9
                                color: closePrev.active ? "#EEF2F7" : "transparent"

                                GlyphIcon {
                                    anchors.centerIn: parent
                                    name: "close"
                                    width: 12
                                    height: 12
                                    color: "#7C8A99"
                                }

                                HoverHandler { id: closePrev }

                                TapHandler {
                                    onTapped: root.closePreview()
                                }
                            }
                        }

                        Rectangle {
                            Layout.fillWidth: true
                            height: 1
                            color: "#EEF1F5"
                        }

                        // 图片
                        Item {
                            Layout.fillWidth: true
                            Layout.fillHeight: true
                            visible: root.previewIsImage

                            Image {
                                anchors.fill: parent
                                anchors.margins: 18
                                source: root.previewPath.length > 0 ? "file://" + root.previewPath : ""
                                fillMode: Image.PreserveAspectFit
                                asynchronous: true

                                onStatusChanged: {
                                    if (status === Image.Error) {
                                        root.say("这张图片打不开", true);
                                    }
                                }
                            }
                        }

                        // 文本
                        Flickable {
                            Layout.fillWidth: true
                            Layout.fillHeight: true
                            visible: !root.previewIsImage
                            clip: true
                            contentWidth: width
                            contentHeight: previewBody.implicitHeight + 32
                            boundsBehavior: Flickable.StopAtBounds

                            ScrollBar.vertical: ScrollBar {
                                policy: ScrollBar.AsNeeded
                            }

                            Text {
                                id: previewBody

                                x: 18
                                y: 16
                                width: parent.width - 36
                                text: root.previewText
                                font.pixelSize: 13
                                font.family: "monospace"
                                lineHeight: 1.35
                                color: "#3D4A57"
                                wrapMode: Text.WrapAnywhere
                                textFormat: Text.PlainText
                            }
                        }
                    }
                }
            }
        }

        // ============================================================
        // 状态条
        // ============================================================
        Rectangle {
            Layout.fillWidth: true
            Layout.preferredHeight: 32
            color: "#FAFBFD"

            Rectangle {
                anchors.top: parent.top
                anchors.left: parent.left
                anchors.right: parent.right
                height: 1
                color: "#EEF1F5"
            }

            RowLayout {
                anchors.fill: parent
                anchors.leftMargin: 18
                anchors.rightMargin: 18
                spacing: 12

                Text {
                    text: Files.count + " 个东西"
                    font.pixelSize: 12
                    color: "#9AA8B7"
                }

                Text {
                    Layout.fillWidth: true
                    text: root.toastText
                    font.pixelSize: 12
                    color: root.toastError ? "#D05252" : "#2E9E6B"
                    elide: Text.ElideRight
                }

                Text {
                    text: Files.diskText
                    font.pixelSize: 12
                    color: "#9AA8B7"
                }
            }
        }
    }

    // ============================================================
    // 新建 / 改名 弹窗
    // ============================================================
    Rectangle {
        id: dialog

        anchors.fill: parent
        visible: opacity > 0
        opacity: 0
        color: "#66000000"

        Behavior on opacity {
            NumberAnimation { duration: 120 }
        }

        function open() {
            opacity = 1;
            dialogField.forceActiveFocus();
        }

        function close() {
            opacity = 0;
            root.dialogMode = "";
        }

        MouseArea {
            anchors.fill: parent
            onClicked: dialog.close()
        }

        Rectangle {
            anchors.centerIn: parent
            width: 400
            height: 176
            radius: 18
            color: "#FFFFFF"

            ColumnLayout {
                anchors.fill: parent
                anchors.margins: 20
                spacing: 14

                Text {
                    Layout.fillWidth: true
                    text: root.dialogTitle
                    font.pixelSize: 15
                    font.weight: Font.DemiBold
                    color: "#3D4A57"
                    elide: Text.ElideRight
                }

                Rectangle {
                    Layout.fillWidth: true
                    Layout.preferredHeight: 42
                    radius: 11
                    color: "#F5F7FA"
                    border.width: 1
                    border.color: dialogField.activeFocus ? "#BBD3F2" : "#EDF0F5"

                    TextField {
                        id: dialogField

                        anchors.fill: parent
                        anchors.leftMargin: 12
                        anchors.rightMargin: 12
                        verticalAlignment: TextInput.AlignVCenter
                        font.pixelSize: 14
                        color: "#3D4A57"
                        background: null
                        padding: 0
                        selectByMouse: true

                        onAccepted: commitButton.commit()
                        Keys.onEscapePressed: dialog.close()
                    }
                }

                RowLayout {
                    Layout.fillWidth: true
                    spacing: 10

                    Item { Layout.fillWidth: true }

                    Rectangle {
                        Layout.preferredWidth: 84
                        Layout.preferredHeight: 38
                        radius: 11
                        color: cancelHover.hovered ? "#F1F4F8" : "#FFFFFF"
                        border.width: 1
                        border.color: "#E4E9F0"

                        Text {
                            anchors.centerIn: parent
                            text: "取消"
                            font.pixelSize: 13.5
                            color: "#5B6B7C"
                        }

                        HoverHandler { id: cancelHover }

                        TapHandler {
                            onTapped: dialog.close()
                        }
                    }

                    Rectangle {
                        id: commitButton

                        Layout.preferredWidth: 96
                        Layout.preferredHeight: 38
                        radius: 11
                        color: "#3B82F6"

                        function commit() {
                            const name = dialogField.text.trim();
                            if (name.length === 0) {
                                root.say("名字不能空着", true);
                                return;
                            }

                            let err = "";
                            if (root.dialogMode === "mkdir") {
                                err = Files.makeFolder(name);
                            } else if (root.dialogMode === "rename") {
                                err = Files.renameItem(root.dialogRow, name);
                            }
                            if (err.length > 0) {
                                root.say(err, true);
                                return;
                            }
                            root.say(root.dialogMode === "mkdir" ? "建好了" : "改好了", false);
                            dialog.close();
                        }

                        Text {
                            anchors.centerIn: parent
                            text: "确定"
                            font.pixelSize: 13.5
                            font.weight: Font.DemiBold
                            color: "white"
                        }

                        TapHandler {
                            onTapped: commitButton.commit()
                        }
                    }
                }
            }
        }
    }

    // ============================================================
    // 删除确认
    // ============================================================
    Rectangle {
        anchors.fill: parent
        visible: root.confirmRow >= 0
        color: "#66000000"

        MouseArea {
            anchors.fill: parent
            onClicked: root.confirmRow = -1
        }

        Rectangle {
            anchors.centerIn: parent
            width: 400
            height: 186
            radius: 18
            color: "#FFFFFF"

            ColumnLayout {
                anchors.fill: parent
                anchors.margins: 20
                spacing: 12

                Text {
                    Layout.fillWidth: true
                    text: "把「" + root.rowName(root.confirmRow) + "」放进回收站？"
                    font.pixelSize: 15
                    font.weight: Font.DemiBold
                    color: "#3D4A57"
                    wrapMode: Text.Wrap
                    elide: Text.ElideRight
                    maximumLineCount: 2
                }

                Text {
                    Layout.fillWidth: true
                    text: "放进回收站还能找回来，不用怕。"
                    font.pixelSize: 12.5
                    color: "#93A1B0"
                }

                Item { Layout.fillHeight: true }

                RowLayout {
                    Layout.fillWidth: true
                    spacing: 10

                    Item { Layout.fillWidth: true }

                    Rectangle {
                        Layout.preferredWidth: 84
                        Layout.preferredHeight: 38
                        radius: 11
                        color: nopeHover.hovered ? "#F1F4F8" : "#FFFFFF"
                        border.width: 1
                        border.color: "#E4E9F0"

                        Text {
                            anchors.centerIn: parent
                            text: "不删了"
                            font.pixelSize: 13.5
                            color: "#5B6B7C"
                        }

                        HoverHandler { id: nopeHover }

                        TapHandler {
                            onTapped: root.confirmRow = -1
                        }
                    }

                    Rectangle {
                        Layout.preferredWidth: 96
                        Layout.preferredHeight: 38
                        radius: 11
                        color: "#E05252"

                        Text {
                            anchors.centerIn: parent
                            text: "放回收站"
                            font.pixelSize: 13.5
                            font.weight: Font.DemiBold
                            color: "white"
                        }

                        TapHandler {
                            onTapped: {
                                const err = Files.removeItem(root.confirmRow);
                                if (err.length > 0) {
                                    root.say(err, true);
                                } else {
                                    root.say("已经放进回收站了", false);
                                }
                                root.confirmRow = -1;
                            }
                        }
                    }
                }
            }
        }
    }
}
