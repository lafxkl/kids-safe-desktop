import QtQuick
import QtQuick.Controls
import QtQuick.Layouts
import KidsSafe 1.0

/*
 * 小小程序员：搭积木 → 生成 C 语言 → 用系统 GCC 编译运行。
 *
 * 三道交互上的取舍：
 *   1. 积木不拖拽，用「点选 + 插入」。1280×800 上拖拽的手感不稳，
 *      点一下调色板、积木就插在选中块后面，既好点也表达了「插到这里」。
 *   2. 「重复」「如果」自动带一个配对的「结束」，孩子永远见不到落单的花括号。
 *      要取消循环就删循环块本身，口袋里的内容会留在原地。
 *   3. 判题比对输出，不比对积木 —— 同一种结果允许多种搭法。require 只用来
 *      保证「该用循环的地方真用了循环」，否则直接打印答案也能过。
 */
GameShell {
    id: page

    title: "小小程序员"
    glyph: "laptop"
    accent: "#5C6BC0"
    prompt: "挑一关 · 点积木搭程序 · 运行看结果"
    scoreText: page.passedCount + " / " + page.lessons.length

    // ---------- 状态 ----------
    readonly property var lessons: CodeLab.lessons
    property int lessonIndex: -1
    property var lesson: (lessonIndex >= 0 && lessonIndex < lessons.length)
                         ? lessons[lessonIndex] : null
    readonly property int passedCount: CodeLab.passedIds.length

    property int selected: -1
    property int rev: 0          // 每次改动 +1，让依赖它的绑定重新求值

    readonly property int leftWidth: 178
    readonly property int rightWidth: Math.min(380, Math.max(272, page.width * 0.29))

    ListModel {
        id: program
    }

    // 调色板：kind 与 C++ 的 generate() 一一对应
    ListModel {
        id: palette

        ListElement { kind: "print";    label: "打印一句话"; sub: "写一行字"; accent: "#26A69A" }
        ListElement { kind: "varset";   label: "变量设值";   sub: "盒子里放个数"; accent: "#42A5F5" }
        ListElement { kind: "varadd";   label: "变量加";     sub: "盒子里的数加几"; accent: "#5C6BC0" }
        ListElement { kind: "printvar"; label: "打印变量";   sub: "看看盒子里是几"; accent: "#7E57C2" }
        ListElement { kind: "loop";     label: "重复 N 次";  sub: "里面的做 N 遍"; accent: "#FB8C00" }
        ListElement { kind: "if";       label: "如果…那么";  sub: "条件对了才做"; accent: "#E53935" }
    }

    // ---------- 与 C++ 的数据交换 ----------
    function push() {
        page.rev += 1
        const blocks = []
        for (let i = 0; i < program.count; ++i) {
            const b = program.get(i)
            blocks.push({
                kind: b.kind,
                text: b.text,
                slot: b.slot,
                value: b.value,
                times: b.times,
                op: b.op,
                forKind: b.forKind
            })
        }
        CodeLab.setProgram(blocks)
    }

    // 给每个「结束」标上它收口的是「重复」还是「如果」，好显示成
    // 「结束重复」而不是含糊的「结束」。
    function normalize() {
        const stack = []
        for (let i = 0; i < program.count; ++i) {
            const kind = program.get(i).kind
            if (kind === "loop" || kind === "if") {
                stack.push(kind)
            } else if (kind === "end") {
                program.setProperty(i, "forKind", stack.length > 0 ? stack.pop() : "")
            }
        }
    }

    function depthAt(index) {
        const revision = page.rev      // 依赖：程序一变，缩进就重算
        let depth = 0
        for (let i = 0; i < index; ++i) {
            const kind = program.get(i).kind
            if (kind === "end") {
                depth = Math.max(0, depth - 1)
            } else if (kind === "loop" || kind === "if") {
                depth += 1
            }
        }
        if (program.get(index).kind === "end") {
            return Math.max(0, depth - 1)
        }
        return depth
    }

    function matchEnd(index) {
        let depth = 0
        for (let i = index + 1; i < program.count; ++i) {
            const kind = program.get(i).kind
            if (kind === "end") {
                if (depth === 0) {
                    return i
                }
                depth -= 1
            } else if (kind === "loop" || kind === "if") {
                depth += 1
            }
        }
        return -1
    }

    function addBlock(kind) {
        // 有选中块就插在它后面，否则加在最外层末尾
        let at = program.count
        if (page.selected >= 0 && page.selected < program.count) {
            at = page.selected + 1
        }

        let text = ""
        let value = 0
        let times = 3
        let op = ">"
        if (kind === "print") {
            text = "你好"
        } else if (kind === "varset" || kind === "varadd") {
            value = 1
        } else if (kind === "if") {
            value = 5
        }

        program.insert(at, {
            kind: kind, text: text, slot: 0, value: value,
            times: times, op: op, forKind: ""
        })

        if (kind === "loop" || kind === "if") {
            // 自动配一个「结束」：孩子不必理解花括号，也就不会漏掉它。
            // 新块夹在两者之间，下一步点的积木自然落进口袋里。
            program.insert(at + 1, {
                kind: "end", text: "", slot: 0, value: 0,
                times: 0, op: "", forKind: kind
            })
        }

        page.selected = at
        page.normalize()
        page.push()
    }

    function removeBlock(index) {
        if (index < 0 || index >= program.count) {
            return
        }
        const kind = program.get(index).kind
        if (kind === "end") {
            return     // 「结束」跟着起始块走，界面上也不给删除按钮
        }
        if (kind === "loop" || kind === "if") {
            const end = page.matchEnd(index)
            program.remove(index, 1)
            if (end > index) {
                program.remove(end - 1, 1)    // 前面少了一个，下标减一
            }
        } else {
            program.remove(index, 1)
        }
        if (page.selected >= program.count) {
            page.selected = program.count - 1
        }
        page.normalize()
        page.push()
    }

    function moveBlock(index, delta) {
        const target = index + delta
        if (target < 0 || target >= program.count) {
            return
        }
        program.move(index, target, 1)
        page.selected = target
        page.push()
    }

    // 只允许普通块在同一层里换位置：跨过「重复」或「结束」会把夹心结构弄坏，
    // 那种情况按钮直接置灰，比让人点了没反应好。
    function canMoveUp(index) {
        if (index <= 0) {
            return false
        }
        const kind = program.get(index).kind
        if (kind === "end" || kind === "loop" || kind === "if") {
            return false
        }
        return page.depthAt(index - 1) === page.depthAt(index)
    }

    function canMoveDown(index) {
        if (index >= program.count - 1) {
            return false
        }
        const kind = program.get(index).kind
        if (kind === "end" || kind === "loop" || kind === "if") {
            return false
        }
        return page.depthAt(index + 1) === page.depthAt(index)
    }

    function setText(index, value) {
        if (index < 0 || index >= program.count) {
            return
        }
        program.setProperty(index, "text", value)
        page.push()
    }

    function setSlot(index, value) {
        program.setProperty(index, "slot", value)
        page.push()
    }

    function setValue(index, raw) {
        let value = parseInt(raw)
        if (isNaN(value)) {
            value = 0
        }
        value = Math.max(-999, Math.min(999, value))
        program.setProperty(index, "value", value)
        page.push()
        return value
    }

    function setTimes(index, raw) {
        let value = parseInt(raw)
        if (isNaN(value)) {
            value = 3
        }
        value = Math.max(1, Math.min(20, value))
        program.setProperty(index, "times", value)
        page.push()
        return value
    }

    function setOp(index, value) {
        program.setProperty(index, "op", value)
        page.push()
    }

    function loadLesson(index) {
        program.clear()
        page.selected = -1
        page.lessonIndex = index

        const blocks = (index >= 0 && lessons[index] && lessons[index].starter)
                       ? lessons[index].starter : []
        for (let i = 0; i < blocks.length; ++i) {
            const b = blocks[i]
            program.append({
                kind: b.kind !== undefined ? b.kind : "print",
                text: b.text !== undefined ? b.text : "",
                slot: b.slot !== undefined ? b.slot : 0,
                value: b.value !== undefined ? b.value : 0,
                times: b.times !== undefined ? b.times : 3,
                op: b.op !== undefined ? b.op : ">",
                forKind: b.forKind !== undefined ? b.forKind : ""
            })
        }

        page.normalize()
        // 教学关不判题：这一关是「看演示」，随便改随便试
        CodeLab.setTask((index >= 0 && lessons[index] && lessons[index].type === "task")
                        ? lessons[index] : ({}))
        CodeLab.clearOutput()
        page.push()
    }

    function nextLesson() {
        if (lessons.length === 0) {
            return
        }
        let next = page.lessonIndex + 1
        if (next >= lessons.length) {
            next = 0
        }
        page.loadLesson(next)
    }

    function freeMode() {
        program.clear()
        page.selected = -1
        page.lessonIndex = -1
        CodeLab.setTask({})
        CodeLab.clearOutput()
        page.push()
    }

    function kindColor(kind) {
        switch (kind) {
        case "print":    return "#26A69A"
        case "varset":   return "#42A5F5"
        case "varadd":   return "#5C6BC0"
        case "printvar": return "#7E57C2"
        case "loop":     return "#FB8C00"
        case "if":       return "#E53935"
        case "end":      return "#A1887F"
        default:         return "#90A4AE"
        }
    }

    function kindIcon(kind) {
        switch (kind) {
        case "print":    return "pencil"
        case "varset":   return "grid"
        case "varadd":   return "hammer"
        case "printvar": return "eye"
        case "loop":     return "hourglass"
        case "if":       return "traffic"
        default:         return "star"
        }
    }

    function kindLabel(kind) {
        switch (kind) {
        case "print":    return "打印"
        case "varset":   return "变量设值"
        case "varadd":   return "变量加"
        case "printvar": return "打印变量"
        case "loop":     return "重复"
        case "if":       return "如果"
        case "end":      return "结束"
        default:         return kind
        }
    }

    Component.onCompleted: {
        if (lessons.length > 0) {
            page.loadLesson(0)
        }
    }

    // 离开页面时别让子进程在后台继续跑：CodeLab 是单例，页面销毁不会带走它。
    Component.onDestruction: CodeLab.stop()

    // ---------- 布局 ----------
    Item {
        anchors.fill: page.contentArea

        ColumnLayout {
            anchors.fill: parent
            spacing: 10

            // ---- 关卡条 ----
            Rectangle {
                Layout.fillWidth: true
                Layout.preferredHeight: 62
                radius: 20
                color: "#FFFFFF"

                Flickable {
                    anchors.fill: parent
                    // 右边给「项目工坊」留位：那个按钮不参与横向滚动，
                    // 否则关卡一多就被挤出去，孩子找不到。
                    anchors.margins: 8
                    anchors.rightMargin: labButton.width + 16
                    contentWidth: chips.width
                    contentHeight: height
                    clip: true
                    boundsBehavior: Flickable.StopAtBounds

                    Row {
                        id: chips

                        spacing: 8
                        height: parent.height

                        Rectangle {
                            width: freeText.implicitWidth + 34
                            height: 46
                            radius: 23
                            color: page.lessonIndex < 0 ? "#7E57C2" : "#F4EDE4"

                            Text {
                                id: freeText
                                anchors.centerIn: parent
                                text: "自由创作"
                                font.pixelSize: 15
                                font.weight: Font.DemiBold
                                color: page.lessonIndex < 0 ? "#FFFFFF" : "#7A6759"
                            }

                            MouseArea {
                                anchors.fill: parent
                                onClicked: page.freeMode()
                            }
                        }

                        Repeater {
                            // 用长度当 model、在 delegate 里按 index 取数据，
                            // 而不是把整个数组交给 Repeater —— 后者要依赖
                            // modelData 的上下文注入，前者只用 index，最稳。
                            model: page.lessons.length

                            Rectangle {
                                id: chip

                                required property int index

                                readonly property var item: page.lessons[index]
                                readonly property bool active: page.lessonIndex === index
                                readonly property bool passed: CodeLab.passedIds.indexOf(item.id) >= 0
                                readonly property color tint: item.type === "task"
                                                              ? "#FB8C00" : "#5C6BC0"

                                width: chipText.implicitWidth + (passed ? 58 : 34)
                                height: 46
                                radius: 23
                                color: active ? tint : "#F4EDE4"

                                Text {
                                    visible: chip.passed
                                    anchors.verticalCenter: parent.verticalCenter
                                    anchors.left: parent.left
                                    anchors.leftMargin: 12
                                    text: "✓"
                                    font.pixelSize: 16
                                    font.bold: true
                                    color: chip.active ? "#FFFFFF" : "#43A047"
                                }

                                Text {
                                    id: chipText
                                    anchors.centerIn: parent
                                    anchors.horizontalCenterOffset: chip.passed ? 8 : 0
                                    text: chip.item.title
                                    font.pixelSize: 15
                                    font.weight: Font.DemiBold
                                    color: chip.active ? "#FFFFFF" : "#7A6759"
                                }

                                MouseArea {
                                    anchors.fill: parent
                                    onClicked: page.loadLesson(chip.index)
                                }
                            }
                        }
                    }
                }

                // 「项目工坊」入口：这里是从零搭，那边是拆三个写完的作品。
                // 放在关卡条右端而不是塞进横向滚动的 chip 里 —— 后者关卡一多
                // 就被滚出视野，等于没有入口。
                Rectangle {
                    id: labButton

                    anchors.right: parent.right
                    anchors.rightMargin: 8
                    anchors.verticalCenter: parent.verticalCenter
                    width: labText.implicitWidth + 30
                    height: 46
                    radius: 23
                    color: labTap.pressed ? "#E65100" : "#EF6C00"

                    Text {
                        id: labText
                        anchors.centerIn: parent
                        text: "项目工坊 →"
                        font.pixelSize: 15
                        font.weight: Font.DemiBold
                        color: "#FFFFFF"
                    }

                    MouseArea {
                        id: labTap
                        anchors.fill: parent
                        onClicked: page.navWin.pushPage(Qt.resolvedUrl("ProjectLabPage.qml"))
                    }
                }
            }

            RowLayout {
                Layout.fillWidth: true
                Layout.fillHeight: true
                spacing: 12

                // ---- 积木箱 ----
                Rectangle {
                    Layout.preferredWidth: page.leftWidth
                    Layout.fillHeight: true
                    radius: 20
                    color: "#FFFFFF"

                    ColumnLayout {
                        anchors.fill: parent
                        anchors.margins: 12
                        spacing: 7

                        Text {
                            text: "积木箱"
                            font.pixelSize: 14
                            font.bold: true
                            color: "#7A6759"
                        }

                        Repeater {
                            model: palette

                            Rectangle {
                                id: paletteItem

                                required property string kind
                                required property string label
                                required property string sub
                                required property color accent

                                Layout.fillWidth: true
                                Layout.preferredHeight: 52
                                radius: 14
                                color: paletteArea.pressed ? Qt.darker(paletteItem.accent, 1.12)
                                                           : paletteItem.accent

                                Rectangle {
                                    anchors.left: parent.left
                                    anchors.top: parent.top
                                    anchors.bottom: parent.bottom
                                    anchors.margins: 6
                                    width: 5
                                    radius: 3
                                    color: "#FFFFFF66"
                                }

                                Column {
                                    anchors.verticalCenter: parent.verticalCenter
                                    anchors.left: parent.left
                                    anchors.leftMargin: 17
                                    spacing: 0

                                    Text {
                                        text: paletteItem.label
                                        font.pixelSize: 14
                                        font.bold: true
                                        color: "#FFFFFF"
                                    }

                                    Text {
                                        text: paletteItem.sub
                                        font.pixelSize: 10
                                        color: "#FFFFFFC8"
                                    }
                                }

                                MouseArea {
                                    id: paletteArea
                                    anchors.fill: parent
                                    onClicked: page.addBlock(paletteItem.kind)
                                }
                            }
                        }

                        Item { Layout.fillHeight: true }

                        Rectangle {
                            Layout.fillWidth: true
                            Layout.preferredHeight: 36
                            radius: 12
                            color: clearArea.pressed ? "#EFE3D6" : "#F7F1E9"

                            Text {
                                anchors.centerIn: parent
                                text: "清空重搭"
                                font.pixelSize: 13
                                color: "#8D6E63"
                            }

                            MouseArea {
                                id: clearArea
                                anchors.fill: parent
                                onClicked: {
                                    program.clear()
                                    page.selected = -1
                                    page.push()
                                    CodeLab.clearOutput()
                                }
                            }
                        }
                    }
                }

                // ---- 程序区 ----
                Rectangle {
                    Layout.fillWidth: true
                    Layout.fillHeight: true
                    Layout.minimumWidth: 320
                    radius: 20
                    color: "#FFFFFF"
                    clip: true

                    ColumnLayout {
                        anchors.fill: parent
                        anchors.margins: 12
                        spacing: 9

                        // 当前关卡的说明
                        Rectangle {
                            Layout.fillWidth: true
                            Layout.preferredHeight: page.lesson ? 100 : 56
                            radius: 14
                            color: page.lesson
                                   ? (page.lesson.type === "task" ? "#FFF3E0" : "#EDE7F6")
                                   : "#F7F1E9"

                            ColumnLayout {
                                anchors.fill: parent
                                anchors.margins: 11
                                spacing: 3

                                RowLayout {
                                    Layout.fillWidth: true
                                    spacing: 8

                                    Text {
                                        Layout.fillWidth: true
                                        text: page.lesson
                                              ? (page.lesson.chapter + " · " + page.lesson.title)
                                              : "自由创作"
                                        font.pixelSize: 14
                                        font.bold: true
                                        color: "#5D4037"
                                        elide: Text.ElideRight
                                    }

                                    Text {
                                        visible: page.lesson && page.lesson.type === "task"
                                        text: "题目"
                                        font.pixelSize: 11
                                        color: "#EF6C00"
                                    }

                                    Text {
                                        visible: page.lesson && page.lesson.type === "teach"
                                        text: "教学"
                                        font.pixelSize: 11
                                        color: "#5E35B1"
                                    }
                                }

                                Text {
                                    Layout.fillWidth: true
                                    text: page.lesson ? (page.lesson.goal ? page.lesson.goal
                                                                         : page.lesson.story)
                                                      : "随便搭，搭完点「运行」看看结果"
                                    font.pixelSize: 12
                                    color: "#6D4C41"
                                    wrapMode: Text.WordWrap
                                    maximumLineCount: 2
                                    elide: Text.ElideRight
                                }

                                Text {
                                    Layout.fillWidth: true
                                    visible: page.lesson && page.lesson.hint
                                    text: page.lesson ? ("小提示：" + page.lesson.hint) : ""
                                    font.pixelSize: 11
                                    color: "#9B8574"
                                    wrapMode: Text.WordWrap
                                    maximumLineCount: 1
                                    elide: Text.ElideRight
                                }
                            }
                        }

                        // 积木链
                        Flickable {
                            Layout.fillWidth: true
                            Layout.fillHeight: true
                            contentWidth: width
                            contentHeight: blocksColumn.height
                            clip: true
                            boundsBehavior: Flickable.StopAtBounds

                            Column {
                                id: blocksColumn

                                width: parent.width
                                spacing: 6

                                Repeater {
                                    model: program

                                    Item {
                                        id: blockRow

                                        required property int index
                                        required property string kind
                                        required property string text
                                        required property int slot
                                        required property int value
                                        required property int times
                                        required property string op
                                        required property string forKind

                                        readonly property bool isEnd: kind === "end"
                                        readonly property int indent: page.depthAt(index)
                                        readonly property bool active: page.selected === index

                                        width: blocksColumn.width
                                        height: isEnd ? 26 : 60

                                        // ---- 结束块：一根收口横条 ----
                                        // 点它可以把「下一块积木」放到循环外面 ——
                                        // 这是孩子唯一需要的「跳出循环」操作。
                                        Item {
                                            visible: blockRow.isEnd
                                            anchors.fill: parent
                                            anchors.leftMargin: 12 + blockRow.indent * 22
                                            anchors.rightMargin: 4

                                            Rectangle {
                                                anchors.left: parent.left
                                                anchors.right: parent.right
                                                anchors.verticalCenter: parent.verticalCenter
                                                height: blockRow.active ? 14 : 9
                                                radius: 5
                                                color: blockRow.active ? "#8D6E63" : "#D7CCC8"
                                            }

                                            Text {
                                                anchors.left: parent.left
                                                anchors.leftMargin: 14
                                                anchors.verticalCenter: parent.verticalCenter
                                                text: blockRow.forKind === "if" ? "结束如果" : "结束重复"
                                                font.pixelSize: 11
                                                color: blockRow.active ? "#FFFFFF" : "#8D6E63"
                                            }

                                            MouseArea {
                                                anchors.fill: parent
                                                onClicked: page.selected = blockRow.index
                                            }
                                        }

                                        // ---- 普通积木 ----
                                        Rectangle {
                                            id: blockBody

                                            visible: !blockRow.isEnd

                                            anchors.left: parent.left
                                            anchors.leftMargin: 12 + blockRow.indent * 22
                                            anchors.right: parent.right
                                            anchors.rightMargin: 4
                                            height: 54

                                            radius: 14
                                            color: page.kindColor(blockRow.kind)
                                            border.width: blockRow.active ? 3 : 0
                                            border.color: "#FFFFFF"

                                            // 顶部凸起的小卡口，让积木看着能插在一起
                                            Rectangle {
                                                x: 12
                                                y: -4
                                                width: 24
                                                height: 8
                                                radius: 4
                                                color: Qt.lighter(page.kindColor(blockRow.kind), 1.28)
                                            }

                                            RowLayout {
                                                anchors.fill: parent
                                                anchors.leftMargin: 11
                                                anchors.rightMargin: 8
                                                spacing: 7

                                                GlyphIcon {
                                                    Layout.preferredWidth: 22
                                                    Layout.preferredHeight: 22
                                                    name: page.kindIcon(blockRow.kind)
                                                    color: "#FFFFFF"
                                                }

                                                Text {
                                                    Layout.preferredWidth: 62
                                                    text: page.kindLabel(blockRow.kind)
                                                    font.pixelSize: 13
                                                    font.bold: true
                                                    color: "#FFFFFF"
                                                    elide: Text.ElideRight
                                                }

                                                // 文字输入（打印）
                                                TextField {
                                                    visible: blockRow.kind === "print"
                                                    Layout.fillWidth: true
                                                    Layout.maximumWidth: 190
                                                    Layout.minimumWidth: 70
                                                    Layout.preferredHeight: 36
                                                    font.pixelSize: 13
                                                    color: "#4E342E"
                                                    text: blockRow.text
                                                    placeholderText: "想说的话"
                                                    selectByMouse: true
                                                    background: Rectangle {
                                                        radius: 9
                                                        color: "#FFF8F0"
                                                        border.width: 1
                                                        border.color: parent.activeFocus ? "#FFB74D" : "#00000014"
                                                    }
                                                    onTextChanged: {
                                                        if (text !== blockRow.text) {
                                                            page.setText(blockRow.index, text)
                                                        }
                                                    }
                                                }

                                                // 变量选择（变量1 / 2 / 3）
                                                Row {
                                                    visible: blockRow.kind === "varset"
                                                             || blockRow.kind === "varadd"
                                                             || blockRow.kind === "printvar"
                                                             || blockRow.kind === "if"
                                                    spacing: 3

                                                    Repeater {
                                                        model: 3

                                                        Rectangle {
                                                            width: 27
                                                            height: 27
                                                            radius: 14
                                                            color: blockRow.slot === index ? "#FFFFFF" : "#FFFFFF33"

                                                            Text {
                                                                anchors.centerIn: parent
                                                                text: index + 1
                                                                font.pixelSize: 12
                                                                font.bold: true
                                                                color: blockRow.slot === index ? "#5D4037" : "#FFFFFF"
                                                            }

                                                            MouseArea {
                                                                anchors.fill: parent
                                                                onClicked: page.setSlot(blockRow.index, index)
                                                            }
                                                        }
                                                    }
                                                }

                                                // 数字（设值 / 加 / 次数 / 比较值）
                                                TextField {
                                                    visible: blockRow.kind !== "print"
                                                             && blockRow.kind !== "printvar"
                                                    Layout.preferredWidth: 58
                                                    Layout.preferredHeight: 32
                                                    horizontalAlignment: TextInput.AlignHCenter
                                                    font.pixelSize: 13
                                                    color: "#4E342E"
                                                    text: blockRow.kind === "loop" ? blockRow.times
                                                                                   : blockRow.value
                                                    selectByMouse: true
                                                    validator: IntValidator { bottom: -999; top: 999 }
                                                    background: Rectangle {
                                                        radius: 8
                                                        color: "#FFF8F0"
                                                    }
                                                    onEditingFinished: {
                                                        if (blockRow.kind === "loop") {
                                                            text = page.setTimes(blockRow.index, text)
                                                        } else {
                                                            text = page.setValue(blockRow.index, text)
                                                        }
                                                    }
                                                }

                                                Text {
                                                    visible: blockRow.kind === "loop"
                                                    text: "次"
                                                    font.pixelSize: 12
                                                    color: "#FFFFFF"
                                                }

                                                // 比较符号（如果）
                                                Row {
                                                    visible: blockRow.kind === "if"
                                                    spacing: 3

                                                    Rectangle {
                                                        width: 28
                                                        height: 27
                                                        radius: 8
                                                        color: blockRow.op === ">" ? "#FFFFFF" : "#FFFFFF33"
                                                        Text {
                                                            anchors.centerIn: parent
                                                            text: ">"
                                                            font.pixelSize: 13
                                                            font.bold: true
                                                            color: blockRow.op === ">" ? "#5D4037" : "#FFFFFF"
                                                        }
                                                        MouseArea {
                                                            anchors.fill: parent
                                                            onClicked: page.setOp(blockRow.index, ">")
                                                        }
                                                    }

                                                    Rectangle {
                                                        width: 28
                                                        height: 27
                                                        radius: 8
                                                        color: blockRow.op === "<" ? "#FFFFFF" : "#FFFFFF33"
                                                        Text {
                                                            anchors.centerIn: parent
                                                            text: "<"
                                                            font.pixelSize: 13
                                                            font.bold: true
                                                            color: blockRow.op === "<" ? "#5D4037" : "#FFFFFF"
                                                        }
                                                        MouseArea {
                                                            anchors.fill: parent
                                                            onClicked: page.setOp(blockRow.index, "<")
                                                        }
                                                    }

                                                    Rectangle {
                                                        width: 28
                                                        height: 27
                                                        radius: 8
                                                        color: blockRow.op === "==" ? "#FFFFFF" : "#FFFFFF33"
                                                        Text {
                                                            anchors.centerIn: parent
                                                            text: "="
                                                            font.pixelSize: 13
                                                            font.bold: true
                                                            color: blockRow.op === "==" ? "#5D4037" : "#FFFFFF"
                                                        }
                                                        MouseArea {
                                                            anchors.fill: parent
                                                            onClicked: page.setOp(blockRow.index, "==")
                                                        }
                                                    }
                                                }

                                                Item { Layout.fillWidth: true }

                                                // 上移 / 下移 / 删除
                                                Rectangle {
                                                    width: 26
                                                    height: 26
                                                    radius: 9
                                                    color: page.canMoveUp(blockRow.index) ? "#FFFFFF26" : "#FFFFFF12"
                                                    Text {
                                                        anchors.centerIn: parent
                                                        text: "↑"
                                                        font.pixelSize: 12
                                                        font.bold: true
                                                        color: page.canMoveUp(blockRow.index) ? "#FFFFFF" : "#FFFFFF55"
                                                    }
                                                    MouseArea {
                                                        anchors.fill: parent
                                                        enabled: page.canMoveUp(blockRow.index)
                                                        onClicked: page.moveBlock(blockRow.index, -1)
                                                    }
                                                }

                                                Rectangle {
                                                    width: 26
                                                    height: 26
                                                    radius: 9
                                                    color: page.canMoveDown(blockRow.index) ? "#FFFFFF26" : "#FFFFFF12"
                                                    Text {
                                                        anchors.centerIn: parent
                                                        text: "↓"
                                                        font.pixelSize: 12
                                                        font.bold: true
                                                        color: page.canMoveDown(blockRow.index) ? "#FFFFFF" : "#FFFFFF55"
                                                    }
                                                    MouseArea {
                                                        anchors.fill: parent
                                                        enabled: page.canMoveDown(blockRow.index)
                                                        onClicked: page.moveBlock(blockRow.index, 1)
                                                    }
                                                }

                                                Rectangle {
                                                    width: 26
                                                    height: 26
                                                    radius: 9
                                                    color: "#FFFFFF26"
                                                    Text {
                                                        anchors.centerIn: parent
                                                        text: "✕"
                                                        font.pixelSize: 12
                                                        font.bold: true
                                                        color: "#FFFFFF"
                                                    }
                                                    MouseArea {
                                                        anchors.fill: parent
                                                        onClicked: page.removeBlock(blockRow.index)
                                                    }
                                                }
                                            }

                                            // 点积木本体 = 选中，之后的积木插在它后面
                                            MouseArea {
                                                anchors.fill: parent
                                                z: -1
                                                onClicked: page.selected = blockRow.index
                                            }
                                        }
                                    }
                                }

                                // 空程序时的引导
                                Text {
                                    visible: program.count === 0
                                    width: blocksColumn.width
                                    horizontalAlignment: Text.AlignHCenter
                                    topPadding: 26
                                    text: "点左边的积木，搭出你的第一个程序吧"
                                    font.pixelSize: 14
                                    color: "#B8A492"
                                }
                            }
                        }
                    }
                }

                // ---- 代码 + 输出 ----
                ColumnLayout {
                    Layout.preferredWidth: page.rightWidth
                    Layout.fillHeight: true
                    spacing: 9

                    // 运行 / 交出答案
                    Rectangle {
                        Layout.fillWidth: true
                        Layout.preferredHeight: 52
                        radius: 17
                        color: CodeLab.running ? "#9E9E9E"
                                               : (runArea.pressed ? "#455A64" : "#37474F")

                        RowLayout {
                            anchors.centerIn: parent
                            spacing: 9

                            Text {
                                text: CodeLab.running ? "…" : "▶"
                                font.pixelSize: 17
                                color: "#FFFFFF"
                            }

                            Text {
                                text: CodeLab.running
                                      ? CodeLab.status
                                      : (page.lesson && page.lesson.type === "task"
                                         ? "交出答案" : "运行")
                                font.pixelSize: 17
                                font.bold: true
                                color: "#FFFFFF"
                            }
                        }

                        MouseArea {
                            id: runArea
                            anchors.fill: parent
                            enabled: !CodeLab.running
                            onClicked: CodeLab.run()
                        }
                    }

                    // 判题结果 / 缺少编译器的提示
                    Rectangle {
                        Layout.fillWidth: true
                        Layout.preferredHeight: visible ? 42 : 0
                        visible: CodeLab.verdict !== "" || !CodeLab.compilerAvailable
                        radius: 12
                        color: !CodeLab.compilerAvailable ? "#FFF3E0"
                               : (CodeLab.verdict === "pass" ? "#E8F5E9" : "#FFEBEE")

                        RowLayout {
                            anchors.fill: parent
                            anchors.leftMargin: 10
                            anchors.rightMargin: 6
                            spacing: 6

                            Text {
                                Layout.fillWidth: true
                                elide: Text.ElideRight
                                font.pixelSize: 13
                                font.bold: true
                                color: !CodeLab.compilerAvailable ? "#E65100"
                                       : (CodeLab.verdict === "pass" ? "#2E7D32" : "#C62828")
                                text: !CodeLab.compilerAvailable
                                      ? "没找到 gcc：家长执行 sudo apt install gcc"
                                      : CodeLab.verdictText
                            }

                            Rectangle {
                                Layout.preferredWidth: 74
                                Layout.preferredHeight: 30
                                radius: 15
                                visible: CodeLab.verdict === "pass"
                                color: nextArea.pressed ? "#1B5E20" : "#2E7D32"

                                Text {
                                    anchors.centerIn: parent
                                    text: "下一关 →"
                                    font.pixelSize: 12
                                    font.bold: true
                                    color: "#FFFFFF"
                                }

                                MouseArea {
                                    id: nextArea
                                    anchors.fill: parent
                                    onClicked: page.nextLesson()
                                }
                            }
                        }
                    }

                    // 代码预览
                    Rectangle {
                        Layout.fillWidth: true
                        Layout.fillHeight: true
                        radius: 16
                        color: "#263238"
                        clip: true

                        Text {
                            anchors.left: parent.left
                            anchors.top: parent.top
                            anchors.margins: 11
                            text: "C 语言 · GCC 编译"
                            font.pixelSize: 11
                            color: "#90A4AE"
                        }

                        ScrollView {
                            anchors.fill: parent
                            anchors.margins: 6
                            anchors.topMargin: 30
                            clip: true
                            background: null

                            TextArea {
                                readOnly: true
                                selectByMouse: true
                                text: CodeLab.code
                                font.family: "monospace"
                                font.pixelSize: 11
                                color: "#E0F2F1"
                                wrapMode: TextArea.NoWrap
                                background: null
                            }
                        }
                    }

                    // 运行输出
                    Rectangle {
                        Layout.fillWidth: true
                        Layout.fillHeight: true
                        radius: 16
                        color: "#FFFDF8"
                        clip: true

                        Text {
                            anchors.left: parent.left
                            anchors.top: parent.top
                            anchors.margins: 11
                            text: "运行结果"
                            font.pixelSize: 11
                            color: "#9B8574"
                        }

                        ScrollView {
                            anchors.fill: parent
                            anchors.margins: 6
                            anchors.topMargin: 30
                            clip: true
                            background: null

                            TextArea {
                                readOnly: true
                                selectByMouse: true
                                text: CodeLab.output
                                font.family: "monospace"
                                font.pixelSize: 12
                                color: "#4E342E"
                                wrapMode: TextArea.Wrap
                                background: null
                            }
                        }
                    }
                }
            }
        }
    }
}
