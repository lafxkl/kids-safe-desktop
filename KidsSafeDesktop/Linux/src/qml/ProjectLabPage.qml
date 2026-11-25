import QtQuick
import QtQuick.Controls
import QtQuick.Layouts
import KidsSafe 1.0

/*
 * 项目工坊：三个「现成的作品」放在这里给孩子拆。
 *
 * 为什么不是又一批小游戏：这三款的重点不是玩，而是「它是怎么写出来的」。
 * 每一款都配了三样东西 —— 玩法说明、关键代码、可调参数。调一个滑块，画面
 * 立刻不一样，孩子第一次能直观感到「程序的行为是数字决定的」。
 */
GameShell {
    id: page

    title: "项目工坊"
    glyph: "rocket"
    accent: "#EF6C00"
    prompt: "玩一玩 · 看代码 · 改参数"
    scoreText: page.projectTitle

    property int projectIndex: 0
    property string projectTitle: "飞机大战"
    property bool codeOpen: false

    ListModel {
        id: projects

        ListElement {
            name: "飞机大战"
            source: "projects/PlaneWar.qml"
            badge: "入门"
            desc: "躲敌机 · 自动开火"
            accent: "#42A5F5"
        }
        ListElement {
            name: "推箱子"
            source: "projects/Sokoban.qml"
            badge: "入门"
            desc: "地图就是一张表"
            accent: "#8D6E63"
        }
        ListElement {
            name: "横板冒险"
            source: "projects/Platformer.qml"
            badge: "进阶"
            desc: "重力 · 跳跃 · 摄像机"
            accent: "#43A047"
        }
    }

    ListModel {
        id: knobsModel
    }

    function openProject(index) {
        if (index < 0 || index >= projects.count) {
            return
        }
        page.projectIndex = index
        page.projectTitle = projects.get(index).name
        loader.source = projects.get(index).source
        Qt.callLater(page.grabFocus)
    }

    // Loader 里的项目要自己拿键盘焦点，否则方向键没用。
    // 不能只在项目的 Component.onCompleted 里 forceActiveFocus：那一刻页面
    // 可能还没被压进 StackView（没 visible），焦点会被上层窗口吃掉。
    function grabFocus() {
        if (loader.item && loader.item.forceActiveFocus) {
            loader.item.forceActiveFocus()
        }
    }

    onVisibleChanged: {
        if (visible) {
            Qt.callLater(page.grabFocus)
        }
    }

    function rebuildKnobs() {
        knobsModel.clear()
        const item = loader.item
        if (!item || !item.knobs) {
            return
        }
        for (let i = 0; i < item.knobs.length; ++i) {
            const k = item.knobs[i]
            knobsModel.append({
                key: k.key,
                label: k.label,
                minV: k.min,
                maxV: k.max,
                stepV: k.step,
                val: k.value
            })
        }
    }

    function knobText(stepV, val) {
        return stepV < 1 ? val.toFixed(1) : String(Math.round(val))
    }

    Component.onCompleted: page.openProject(0)

    Item {
        anchors.fill: page.contentArea

        RowLayout {
            anchors.fill: parent
            spacing: 12

            // ---- 左：项目列表 ----
            ColumnLayout {
                Layout.preferredWidth: 172
                Layout.fillHeight: true
                spacing: 8

                Text {
                    text: "内置项目"
                    font.pixelSize: 14
                    font.bold: true
                    color: "#8D6E63"
                }

                Repeater {
                    model: projects

                    Rectangle {
                        id: card

                        required property int index
                        required property string name
                        required property string badge
                        required property string desc
                        required property var accent

                        Layout.fillWidth: true
                        Layout.preferredHeight: 84
                        radius: 16
                        color: "#FFFFFF"
                        border.width: page.projectIndex === index ? 3 : 0
                        border.color: card.accent

                        Rectangle {
                            anchors.left: parent.left
                            anchors.top: parent.top
                            anchors.bottom: parent.bottom
                            anchors.margins: 8
                            width: 5
                            radius: 3
                            color: card.accent
                        }

                        Column {
                            anchors.verticalCenter: parent.verticalCenter
                            anchors.left: parent.left
                            anchors.leftMargin: 20
                            anchors.right: parent.right
                            anchors.rightMargin: 8
                            spacing: 3

                            Row {
                                spacing: 6

                                Text {
                                    text: card.name
                                    font.pixelSize: 15
                                    font.bold: true
                                    color: "#5D4037"
                                }

                                Rectangle {
                                    width: badgeText.implicitWidth + 12
                                    height: 18
                                    radius: 9
                                    color: card.badge === "进阶" ? "#FFF3E0" : "#EDE7F6"

                                    Text {
                                        id: badgeText
                                        anchors.centerIn: parent
                                        text: card.badge
                                        font.pixelSize: 10
                                        font.bold: true
                                        color: card.badge === "进阶" ? "#E65100" : "#5E35B1"
                                    }
                                }
                            }

                            Text {
                                width: parent.width
                                text: card.desc
                                font.pixelSize: 11
                                color: "#9B8574"
                                elide: Text.ElideRight
                            }
                        }

                        MouseArea {
                            anchors.fill: parent
                            onClicked: page.openProject(card.index)
                        }
                    }
                }

                Item {
                    Layout.fillHeight: true
                }

                Text {
                    Layout.fillWidth: true
                    text: "这些都是现成的作品，先玩，再看它怎么写。"
                    font.pixelSize: 11
                    color: "#B8A492"
                    wrapMode: Text.WordWrap
                }
            }

            // ---- 中：游戏 + 代码 ----
            ColumnLayout {
                Layout.fillWidth: true
                Layout.fillHeight: true
                spacing: 10

                // 包一层 Item：Loader 出错时它自己什么都不画，中间会留一块
                // 白板，孩子只会以为「卡住了」。这里补一张说明卡。
                Item {
                    Layout.fillWidth: true
                    Layout.fillHeight: true

                    Loader {
                        id: loader

                        anchors.fill: parent

                        onLoaded: page.rebuildKnobs()
                        onStatusChanged: {
                            if (status === Loader.Error) {
                                console.warn("[lab] 项目加载失败：", source)
                                page.rebuildKnobs()
                            }
                        }
                    }

                    Rectangle {
                        anchors.fill: parent
                        visible: loader.status === Loader.Error
                        radius: 16
                        color: "#FFF3E0"

                        Column {
                            anchors.centerIn: parent
                            spacing: 8

                            Text {
                                anchors.horizontalCenter: parent.horizontalCenter
                                text: "这一款没打开"
                                font.pixelSize: 18
                                font.bold: true
                                color: "#E65100"
                            }

                            Text {
                                anchors.horizontalCenter: parent.horizontalCenter
                                width: 320
                                horizontalAlignment: Text.AlignHCenter
                                text: "点右边的「重新开始」再试一次，或者换左边另一款。"
                                font.pixelSize: 12
                                color: "#A1887F"
                                wrapMode: Text.WordWrap
                            }
                        }
                    }
                }

                Rectangle {
                    Layout.fillWidth: true
                    Layout.preferredHeight: page.codeOpen ? 186 : 44
                    radius: 16
                    color: "#263238"
                    clip: true

                    RowLayout {
                        id: codeHeader

                        anchors.left: parent.left
                        anchors.right: parent.right
                        anchors.top: parent.top
                        height: 44
                        anchors.leftMargin: 14
                        anchors.rightMargin: 8
                        spacing: 8

                        Text {
                            Layout.fillWidth: true
                            text: "关键代码 · 它是怎么动起来的"
                            font.pixelSize: 12
                            font.bold: true
                            color: "#B0BEC5"
                            elide: Text.ElideRight
                        }

                        Rectangle {
                            Layout.preferredWidth: 84
                            Layout.preferredHeight: 28
                            radius: 14
                            color: toggleArea.pressed ? "#37474F" : "#37474F00"

                            Text {
                                anchors.centerIn: parent
                                text: page.codeOpen ? "收起" : "看代码"
                                font.pixelSize: 12
                                font.bold: true
                                color: "#80CBC4"
                            }

                            MouseArea {
                                id: toggleArea
                                anchors.fill: parent
                                onClicked: page.codeOpen = !page.codeOpen
                            }
                        }
                    }

                    ScrollView {
                        anchors.left: parent.left
                        anchors.right: parent.right
                        anchors.top: codeHeader.bottom
                        anchors.bottom: parent.bottom
                        anchors.margins: 10
                        clip: true
                        visible: page.codeOpen
                        background: null

                        TextArea {
                            readOnly: true
                            selectByMouse: true
                            text: loader.item && loader.item.codeSample ? loader.item.codeSample : ""
                            font.family: "monospace"
                            font.pixelSize: 11
                            color: "#CFD8DC"
                            wrapMode: TextArea.NoWrap
                            background: null
                        }
                    }
                }
            }

            // ---- 右：说明 + 改一改 ----
            ColumnLayout {
                Layout.preferredWidth: 246
                Layout.fillHeight: true
                spacing: 10

                Rectangle {
                    Layout.fillWidth: true
                    Layout.fillHeight: true
                    radius: 18
                    color: "#FFFFFF"

                    ColumnLayout {
                        anchors.fill: parent
                        anchors.margins: 14
                        spacing: 8

                        Text {
                            text: "怎么玩"
                            font.pixelSize: 13
                            font.bold: true
                            color: "#8D6E63"
                        }

                        Text {
                            Layout.fillWidth: true
                            text: loader.item && loader.item.howTo ? loader.item.howTo : ""
                            font.pixelSize: 12
                            color: "#5D4037"
                            wrapMode: Text.WordWrap
                            lineHeight: 1.25
                        }

                        Text {
                            Layout.topMargin: 4
                            text: "在教什么"
                            font.pixelSize: 13
                            font.bold: true
                            color: "#8D6E63"
                        }

                        Text {
                            Layout.fillWidth: true
                            text: loader.item && loader.item.teaches ? loader.item.teaches : ""
                            font.pixelSize: 12
                            color: "#6D4C41"
                            wrapMode: Text.WordWrap
                            lineHeight: 1.25
                        }

                        Item {
                            Layout.fillHeight: true
                        }
                    }
                }

                Rectangle {
                    Layout.fillWidth: true
                    Layout.preferredHeight: knobColumn.implicitHeight + 28
                    radius: 18
                    color: "#FFF8F0"

                    ColumnLayout {
                        id: knobColumn

                        anchors.fill: parent
                        anchors.margins: 14
                        spacing: 4

                        Text {
                            text: "改一改，看变化"
                            font.pixelSize: 13
                            font.bold: true
                            color: "#8D6E63"
                        }

                        Repeater {
                            model: knobsModel

                            ColumnLayout {
                                id: knobRow

                                required property int index
                                required property string key
                                required property string label
                                required property real minV
                                required property real maxV
                                required property real stepV
                                required property real val

                                Layout.fillWidth: true
                                spacing: 0

                                RowLayout {
                                    Layout.fillWidth: true

                                    Text {
                                        Layout.fillWidth: true
                                        text: knobRow.label
                                        font.pixelSize: 11
                                        color: "#8D6E63"
                                    }

                                    Text {
                                        text: page.knobText(knobRow.stepV, knobRow.val)
                                        font.pixelSize: 11
                                        font.bold: true
                                        color: "#E65100"
                                    }
                                }

                                Slider {
                                    Layout.fillWidth: true
                                    Layout.preferredHeight: 26
                                    from: knobRow.minV
                                    to: knobRow.maxV
                                    stepSize: knobRow.stepV
                                    value: knobRow.val

                                    onMoved: {
                                        knobsModel.setProperty(knobRow.index, "val", value)
                                        if (loader.item && loader.item.setKnob) {
                                            loader.item.setKnob(knobRow.key, value)
                                        }
                                    }
                                }
                            }
                        }

                        Text {
                            Layout.fillWidth: true
                            visible: knobsModel.count === 0
                            text: "这一款没有滑块参数 —— 想改就去改代码里的地图（levels 里的字符）。"
                            font.pixelSize: 11
                            color: "#A1887F"
                            wrapMode: Text.WordWrap
                        }
                    }
                }

                Rectangle {
                    Layout.fillWidth: true
                    Layout.preferredHeight: 46
                    radius: 23
                    color: restartArea.pressed ? "#E65100" : "#EF6C00"

                    Text {
                        anchors.centerIn: parent
                        text: "重新开始"
                        font.pixelSize: 14
                        font.bold: true
                        color: "#FFFFFF"
                    }

                    MouseArea {
                        id: restartArea
                        anchors.fill: parent
                        onClicked: {
                            // 加载失败时这个按钮兼作「重试」：Loader 的 source
                            // 没变就不会重新加载，得先清空再给回去。
                            if (loader.status === Loader.Error) {
                                const src = loader.source
                                loader.source = ""
                                loader.source = src
                                Qt.callLater(page.grabFocus)
                                return
                            }
                            if (loader.item && loader.item.restart) {
                                loader.item.restart()
                                loader.item.forceActiveFocus()
                            }
                        }
                    }
                }
            }
        }
    }
}
