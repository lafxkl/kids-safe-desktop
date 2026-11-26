import QtQuick
import QtQuick.Controls
import QtQuick.Layouts
import KidsSafe 1.0
import "HomeworkData.js" as HW

/*
 * 编程作业。
 *
 * 和「小小程序员」关卡的区别：
 *   关卡是动手搭积木、由 GCC 跑出来判题；作业是纸面题 —— 选择题、填空、
 *   排顺序，电脑上不用编译就能做。两者共用同一批概念（顺序 / 重复 / 变量 /
 *   判断 / 队列与栈），所以哪一章卡住了，可以抽对应那套作业复查。
 *
 * 三个交互上的取舍：
 *   1. 选择题点一下立刻判对错，并当场亮出「为什么」——等交卷再告诉孩子错在
 *      哪，中间隔了五道题，他早就忘了当时怎么想的了。
 *   2. 判断题只认「抹平空格和标点之后」的答案。孩子在平板上打个中文逗号就
 *      判错，只会让人不想再做题（见 HomeworkData.js 的 normalize）。
 *   3. 排序题点选而不是拖拽：1280×800 上拖拽命中率低，点一下排到队尾更好点，
 *      点错了用「撤销」退一格。
 *
 * 成绩由 C++ 侧（CodeLab.saveHomeworkScore）记进 kidssafe/codelab 配置，
 * 同一套作业反复做时保留最高分。
 */
GameShell {
    id: page

    title: "编程作业"
    glyph: "pencil"
    accent: "#00897B"
    prompt: "一题一判 · 每题都能马上知道对不对 · 做完交卷看总分"
    scoreText: questions.count > 0
               ? "已答 " + page.answeredCount + " / " + questions.count
               : ""

    property int setIndex: 0
    property var questionsData: []
    property int answeredCount: 0
    property int correctCount: 0
    property bool resultOpen: false

    readonly property var sets: HW.homeworks
    readonly property var current: (setIndex >= 0 && setIndex < sets.length)
                                   ? sets[setIndex] : null

    ListModel {
        id: questions
    }

    // ---------- 判题 ----------
    function loadSet(index) {
        if (sets.length === 0) {
            return
        }
        if (index < 0 || index >= sets.length) {
            index = 0
        }

        page.setIndex = index
        page.answeredCount = 0
        page.correctCount = 0
        page.resultOpen = false
        page.questionsData = []
        questions.clear()

        const list = sets[index].questions || []
        for (let i = 0; i < list.length; ++i) {
            const q = list[i]
            page.questionsData.push(q)
            questions.append({
                qType: q.type,
                qPrompt: q.prompt,
                qCode: q.code !== undefined ? q.code : "",
                qOptions: JSON.stringify(q.options !== undefined ? q.options : []),
                // 排序题：qChoices 是「还没被选走」的候选，qUser 是已排好的顺序。
                // 两个都用 JSON 字符串存在 ListModel 里，改一个角色就能让界面重排。
                qChoices: JSON.stringify(q.type === "order" ? HW.scrambled(q.items, i) : []),
                qAnswerIndex: q.type === "choice" ? q.answer : -1,
                qWhy: q.why !== undefined ? q.why : "",
                qUser: "",
                qJudged: false,
                qCorrect: false
            })
        }
    }

    function recount() {
        let answered = 0
        let right = 0
        for (let i = 0; i < questions.count; ++i) {
            const row = questions.get(i)
            if (row.qJudged) {
                answered += 1
                if (row.qCorrect) {
                    right += 1
                }
            }
        }
        page.answeredCount = answered
        page.correctCount = right
    }

    function finishRow(rowIndex, correct) {
        questions.setProperty(rowIndex, "qJudged", true)
        questions.setProperty(rowIndex, "qCorrect", correct)
        page.recount()
    }

    function answerChoice(rowIndex, optionIndex) {
        const row = questions.get(rowIndex)
        if (row.qJudged) {
            return          // 判过就锁住：改来改去会把「对/错」这个信息冲淡
        }
        questions.setProperty(rowIndex, "qUser", String(optionIndex))
        page.finishRow(rowIndex, HW.judge(page.questionsData[rowIndex], optionIndex))
    }

    function answerFill(rowIndex, text) {
        const row = questions.get(rowIndex)
        if (row.qJudged) {
            return
        }
        if (String(text).trim().length === 0) {
            return          // 空着不算作答，也不算错
        }
        questions.setProperty(rowIndex, "qUser", String(text))
        page.finishRow(rowIndex, HW.judge(page.questionsData[rowIndex], text))
    }

    function tapCandidate(rowIndex, item) {
        const row = questions.get(rowIndex)
        if (row.qJudged) {
            return
        }
        const chosen = JSON.parse(row.qUser.length > 0 ? row.qUser : "[]")
        const avail = JSON.parse(row.qChoices.length > 0 ? row.qChoices : "[]")
        const at = avail.indexOf(item)
        if (at < 0) {
            return
        }
        avail.splice(at, 1)
        chosen.push(item)
        questions.setProperty(rowIndex, "qUser", JSON.stringify(chosen))
        questions.setProperty(rowIndex, "qChoices", JSON.stringify(avail))
    }

    function undoOrder(rowIndex) {
        const row = questions.get(rowIndex)
        if (row.qJudged) {
            return
        }
        const chosen = JSON.parse(row.qUser.length > 0 ? row.qUser : "[]")
        const avail = JSON.parse(row.qChoices.length > 0 ? row.qChoices : "[]")
        if (chosen.length === 0) {
            return
        }
        avail.push(chosen.pop())
        questions.setProperty(rowIndex, "qUser", JSON.stringify(chosen))
        questions.setProperty(rowIndex, "qChoices", JSON.stringify(avail))
    }

    function submitOrder(rowIndex) {
        const row = questions.get(rowIndex)
        if (row.qJudged) {
            return
        }
        const chosen = JSON.parse(row.qUser.length > 0 ? row.qUser : "[]")
        if (chosen.length !== (page.questionsData[rowIndex].items || []).length) {
            return          // 没排完不让交，免得判成「错」其实是没做完
        }
        page.finishRow(rowIndex, HW.judge(page.questionsData[rowIndex], chosen))
    }

    function submitPaper() {
        page.recount()
        page.resultOpen = true
        if (page.current) {
            CodeLab.saveHomeworkScore(page.current.id, page.correctCount, questions.count,
                                      page.current.title)
        }
    }

    function bestOf(id) {
        const scores = CodeLab.homeworkScores
        const record = scores ? scores[id] : null
        if (!record) {
            return ""
        }
        return "最高 " + record.best + "/" + record.total
    }

    Component.onCompleted: page.loadSet(0)

    // ---------- 布局 ----------
    Item {
        anchors.fill: page.contentArea

        ColumnLayout {
            anchors.fill: parent
            spacing: 10

            // ---- 作业套选择 ----
            Rectangle {
                Layout.fillWidth: true
                Layout.preferredHeight: 62
                radius: 20
                color: "#FFFFFF"

                Flickable {
                    anchors.fill: parent
                    anchors.margins: 8
                    contentWidth: setRow.width
                    contentHeight: height
                    clip: true
                    boundsBehavior: Flickable.StopAtBounds

                    Row {
                        id: setRow

                        height: parent.height
                        spacing: 8

                        Repeater {
                            model: page.sets.length

                            Rectangle {
                                id: setChip

                                required property int index

                                readonly property var info: page.sets[index]
                                readonly property bool active: page.setIndex === index
                                readonly property string best: page.bestOf(info.id)

                                width: setLabel.implicitWidth + (best.length > 0 ? 96 : 34)
                                height: 46
                                radius: 23
                                color: active ? "#00897B" : "#F4EDE4"

                                Column {
                                    anchors.centerIn: parent
                                    anchors.horizontalCenterOffset: setChip.best.length > 0 ? -34 : 0
                                    spacing: 0

                                    Text {
                                        id: setLabel
                                        anchors.horizontalCenter: parent.horizontalCenter
                                        text: setChip.info.title
                                        font.pixelSize: 15
                                        font.weight: Font.DemiBold
                                        color: setChip.active ? "#FFFFFF" : "#7A6759"
                                    }
                                }

                                Text {
                                    visible: setChip.best.length > 0
                                    anchors.verticalCenter: parent.verticalCenter
                                    anchors.right: parent.right
                                    anchors.rightMargin: 12
                                    text: setChip.best
                                    font.pixelSize: 11
                                    color: setChip.active ? "#C8E6E3" : "#A1887F"
                                }

                                MouseArea {
                                    anchors.fill: parent
                                    onClicked: page.loadSet(setChip.index)
                                }
                            }
                        }
                    }
                }
            }

            // ---- 题目列表 ----
            Rectangle {
                Layout.fillWidth: true
                Layout.fillHeight: true
                radius: 20
                color: "#FFFFFF"
                clip: true

                Flickable {
                    id: board

                    anchors.fill: parent
                    anchors.margins: 12
                    contentWidth: width
                    contentHeight: qColumn.height
                    clip: true
                    boundsBehavior: Flickable.StopAtBounds

                    Column {
                        id: qColumn

                        width: board.width
                        spacing: 10

                        Repeater {
                            model: questions

                            Rectangle {
                                id: card

                                required property int index
                                required property string qType
                                required property string qPrompt
                                required property string qCode
                                required property string qOptions
                                required property string qChoices
                                required property int qAnswerIndex
                                required property string qWhy
                                required property string qUser
                                required property bool qJudged
                                required property bool qCorrect

                                width: qColumn.width
                                height: body.height + 30
                                radius: 16
                                color: card.qJudged
                                       ? (card.qCorrect ? "#F1F8F2" : "#FDF3F3")
                                       : "#FBF8F4"
                                border.width: 1
                                border.color: card.qJudged
                                              ? (card.qCorrect ? "#C8E6C9" : "#FFCDD2")
                                              : "#F0E4D6"

                                Column {
                                    id: body

                                    x: 14
                                    y: 15
                                    width: parent.width - 28
                                    spacing: 9

                                    // 题号 + 题干
                                    Row {
                                        width: parent.width
                                        spacing: 8

                                        Rectangle {
                                            width: 26
                                            height: 26
                                            radius: 13
                                            color: card.qJudged
                                                   ? (card.qCorrect ? "#43A047" : "#E53935")
                                                   : "#8D6E63"

                                            Text {
                                                anchors.centerIn: parent
                                                text: (card.index + 1)
                                                font.pixelSize: 13
                                                font.bold: true
                                                color: "#FFFFFF"
                                            }
                                        }

                                        Text {
                                            width: parent.width - 42
                                            text: card.qPrompt
                                            font.pixelSize: 15
                                            color: "#4A3B32"
                                            wrapMode: Text.WordWrap
                                            lineHeight: 1.15
                                        }
                                    }

                                    // 附带的代码片段
                                    Rectangle {
                                        width: parent.width
                                        height: card.qCode.length > 0 ? codeText.implicitHeight + 18 : 0
                                        visible: card.qCode.length > 0
                                        radius: 10
                                        color: "#263238"

                                        Text {
                                            id: codeText
                                            x: 10
                                            y: 9
                                            width: parent.width - 20
                                            text: card.qCode
                                            font.family: "monospace"
                                            font.pixelSize: 12
                                            color: "#E0F2F1"
                                            wrapMode: Text.Wrap
                                        }
                                    }

                                    // ---- 选择题 ----
                                    Column {
                                        width: parent.width
                                        spacing: 7
                                        visible: card.qType === "choice"

                                        Repeater {
                                            // 这里查 JSON 数组而不是 ListModel，所以用
                                            // 上下文属性（index / modelData），不写
                                            // required —— 数组模型的 required 注入依赖
                                            // Qt 版本，退一步更稳。
                                            model: JSON.parse(card.qOptions.length > 0 ? card.qOptions : "[]")

                                            Rectangle {
                                                id: optionRow

                                                readonly property bool picked: card.qJudged
                                                                              && card.qUser === String(index)
                                                readonly property bool rightOne: card.qJudged
                                                                                && index === card.qAnswerIndex
                                                readonly property bool wrongPick: picked && !rightOne

                                                width: parent.width
                                                height: 46
                                                radius: 13
                                                color: rightOne ? "#E8F5E9"
                                                       : (wrongPick ? "#FFEBEE" : "#FFFFFF")
                                                border.width: 1
                                                border.color: rightOne ? "#66BB6A"
                                                            : (wrongPick ? "#EF5350" : "#EFDCC6")

                                                Text {
                                                    anchors.verticalCenter: parent.verticalCenter
                                                    anchors.left: parent.left
                                                    anchors.leftMargin: 14
                                                    width: parent.width - 54
                                                    text: modelData
                                                    font.pixelSize: 14
                                                    color: "#4A3B32"
                                                    wrapMode: Text.Wrap
                                                    elide: Text.ElideRight
                                                    maximumLineCount: 2
                                                }

                                                Text {
                                                    anchors.verticalCenter: parent.verticalCenter
                                                    anchors.right: parent.right
                                                    anchors.rightMargin: 14
                                                    visible: rightOne || wrongPick
                                                    text: rightOne ? "✓" : "✗"
                                                    font.pixelSize: 16
                                                    font.bold: true
                                                    color: rightOne ? "#2E7D32" : "#C62828"
                                                }

                                                MouseArea {
                                                    anchors.fill: parent
                                                    enabled: !card.qJudged
                                                    onClicked: page.answerChoice(card.index, index)
                                                }
                                            }
                                        }
                                    }

                                    // ---- 填空题 ----
                                    Row {
                                        width: parent.width
                                        spacing: 8
                                        visible: card.qType === "fill"

                                        Rectangle {
                                            width: parent.width - 96
                                            height: 46
                                            radius: 13
                                            color: "#FFFFFF"
                                            border.width: 1
                                            border.color: card.qJudged
                                                          ? (card.qCorrect ? "#66BB6A" : "#EF5350")
                                                          : "#EFDCC6"

                                            TextField {
                                                id: fillField

                                                anchors.fill: parent
                                                anchors.leftMargin: 12
                                                anchors.rightMargin: 12
                                                placeholderText: card.qJudged ? "" : "在这里写答案"
                                                placeholderTextColor: "#C4AE99"
                                                text: card.qJudged ? card.qUser : ""
                                                readOnly: card.qJudged
                                                verticalAlignment: TextInput.AlignVCenter
                                                font.pixelSize: 15
                                                color: "#4A3B32"
                                                background: null
                                                onAccepted: page.answerFill(card.index, text)
                                            }
                                        }

                                        Rectangle {
                                            visible: !card.qJudged
                                            width: 88
                                            height: 46
                                            radius: 13
                                            color: fillTap.pressed ? "#00695C" : "#00897B"

                                            Text {
                                                anchors.centerIn: parent
                                                text: "确定"
                                                font.pixelSize: 14
                                                font.bold: true
                                                color: "#FFFFFF"
                                            }

                                            MouseArea {
                                                id: fillTap
                                                anchors.fill: parent
                                                onClicked: page.answerFill(card.index, fillField.text)
                                            }
                                        }
                                    }

                                    // ---- 排序题 ----
                                    Column {
                                        width: parent.width
                                        spacing: 7
                                        visible: card.qType === "order"

                                        Text {
                                            width: parent.width
                                            text: "你的答案（点下面的卡片按顺序排）"
                                            font.pixelSize: 11
                                            color: "#9B8574"
                                        }

                                        Flow {
                                            width: parent.width
                                            spacing: 6

                                            Repeater {
                                                model: JSON.parse(card.qUser.length > 0 ? card.qUser : "[]")

                                                Rectangle {
                                                    width: orderText.implicitWidth + 34
                                                    height: 34
                                                    radius: 17
                                                    color: "#FFF1E2"
                                                    border.width: 1
                                                    border.color: "#FFC79A"

                                                    Text {
                                                        id: orderText
                                                        anchors.centerIn: parent
                                                        text: (index + 1) + ". " + modelData
                                                        font.pixelSize: 13
                                                        color: "#6D4C41"
                                                    }
                                                }
                                            }
                                        }

                                        Flow {
                                            width: parent.width
                                            spacing: 6

                                            Repeater {
                                                model: JSON.parse(card.qChoices.length > 0 ? card.qChoices : "[]")

                                                Rectangle {
                                                    width: candText.implicitWidth + 26
                                                    height: 34
                                                    radius: 17
                                                    color: "#FFFFFF"
                                                    border.width: 1
                                                    border.color: "#EFDCC6"

                                                    Text {
                                                        id: candText
                                                        anchors.centerIn: parent
                                                        text: modelData
                                                        font.pixelSize: 13
                                                        color: "#6D4C41"
                                                    }

                                                    MouseArea {
                                                        anchors.fill: parent
                                                        enabled: !card.qJudged
                                                        onClicked: page.tapCandidate(card.index, modelData)
                                                    }
                                                }
                                            }
                                        }

                                        Row {
                                            spacing: 8
                                            visible: !card.qJudged

                                            Rectangle {
                                                width: 76
                                                height: 36
                                                radius: 18
                                                color: undoArea.pressed ? "#EFE1D0" : "#F5EDE3"

                                                Text {
                                                    anchors.centerIn: parent
                                                    text: "撤销"
                                                    font.pixelSize: 13
                                                    color: "#7A6759"
                                                }

                                                MouseArea {
                                                    id: undoArea
                                                    anchors.fill: parent
                                                    onClicked: page.undoOrder(card.index)
                                                }
                                            }

                                            Rectangle {
                                                width: 84
                                                height: 36
                                                radius: 18
                                                color: submitArea.pressed ? "#00695C" : "#00897B"

                                                Text {
                                                    anchors.centerIn: parent
                                                    text: "确定"
                                                    font.pixelSize: 13
                                                    font.bold: true
                                                    color: "#FFFFFF"
                                                }

                                                MouseArea {
                                                    id: submitArea
                                                    anchors.fill: parent
                                                    onClicked: page.submitOrder(card.index)
                                                }
                                            }
                                        }
                                    }

                                    // ---- 判完的说明 ----
                                    Rectangle {
                                        width: parent.width
                                        height: card.qJudged ? whyText.implicitHeight + 16 : 0
                                        visible: card.qJudged
                                        radius: 10
                                        color: card.qCorrect ? "#E8F5E9" : "#FFEBEE"

                                        Text {
                                            id: whyText
                                            x: 10
                                            y: 8
                                            width: parent.width - 20
                                            text: (card.qCorrect ? "✓ 答对了。" : "✗ 正确答案是「"
                                                                   + (card.qType === "choice"
                                                                      ? JSON.parse(card.qOptions)[card.qAnswerIndex]
                                                                      : (card.qType === "fill"
                                                                         ? page.questionsData[card.index].answer
                                                                         : (page.questionsData[card.index].items || []).join(" → ")))
                                                                   + "」。")
                                                  + (card.qWhy.length > 0 ? " " + card.qWhy : "")
                                            font.pixelSize: 12
                                            color: card.qCorrect ? "#2E7D32" : "#C62828"
                                            wrapMode: Text.Wrap
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // ---- 交卷条 ----
            Rectangle {
                Layout.fillWidth: true
                Layout.preferredHeight: 58
                radius: 18
                color: "#FFFFFF"

                RowLayout {
                    anchors.fill: parent
                    anchors.leftMargin: 16
                    anchors.rightMargin: 10
                    spacing: 10

                    Text {
                        Layout.fillWidth: true
                        elide: Text.ElideRight
                        text: page.current
                              ? (page.answeredCount < questions.count
                                 ? "还有 " + (questions.count - page.answeredCount) + " 题没做"
                                 : "全部做完啦，答对 " + page.correctCount + " 题")
                              : "还没有作业"
                        font.pixelSize: 14
                        color: "#6D4C41"
                    }

                    Rectangle {
                        Layout.preferredWidth: 108
                        Layout.preferredHeight: 42
                        radius: 21
                        color: page.answeredCount < questions.count ? "#CFD8DC"
                               : (paperArea.pressed ? "#00695C" : "#00897B")

                        Text {
                            anchors.centerIn: parent
                            text: "交卷"
                            font.pixelSize: 15
                            font.bold: true
                            color: "#FFFFFF"
                        }

                        MouseArea {
                            id: paperArea
                            anchors.fill: parent
                            enabled: page.answeredCount >= questions.count && questions.count > 0
                            onClicked: page.submitPaper()
                        }
                    }
                }
            }
        }

        // ---- 成绩单 ----
        Rectangle {
            anchors.fill: parent
            visible: page.resultOpen
            color: "#664A3B32"

            MouseArea {
                anchors.fill: parent
                // 只挡点击，不关闭：成绩单要孩子自己按按钮关掉
            }

            Rectangle {
                anchors.centerIn: parent
                width: 420
                height: resultColumn.height + 48
                radius: 26
                color: "#FFFFFF"
                border.width: 1
                border.color: "#F2E1CD"

                Column {
                    id: resultColumn

                    x: 24
                    y: 24
                    width: parent.width - 48
                    spacing: 12

                    Text {
                        width: parent.width
                        text: page.correctCount === questions.count ? "🎉 全对！" : "📄 交卷完成"
                        font.pixelSize: 26
                        font.bold: true
                        color: "#4A3B32"
                        horizontalAlignment: Text.AlignHCenter
                    }

                    Text {
                        width: parent.width
                        text: page.correctCount + " / " + questions.count
                        font.pixelSize: 46
                        font.bold: true
                        color: "#00897B"
                        horizontalAlignment: Text.AlignHCenter
                    }

                    Text {
                        width: parent.width
                        text: {
                            const ratio = questions.count > 0 ? page.correctCount / questions.count : 0
                            const stars = Math.round(ratio * 3)
                            let s = ""
                            for (let i = 0; i < 3; ++i) {
                                s += i < stars ? "⭐" : "☆"
                            }
                            return s
                        }
                        font.pixelSize: 24
                        horizontalAlignment: Text.AlignHCenter
                    }

                    Text {
                        width: parent.width
                        text: page.correctCount === questions.count
                              ? "这套作业全部答对，可以去做下一套了。"
                              : "错的题下面都写着正确答案和原因，回去看一眼再做一次。"
                        font.pixelSize: 13
                        color: "#7A6759"
                        wrapMode: Text.Wrap
                        horizontalAlignment: Text.AlignHCenter
                    }

                    Row {
                        anchors.horizontalCenter: parent.horizontalCenter
                        spacing: 10

                        Rectangle {
                            width: 108
                            height: 42
                            radius: 21
                            color: retryArea.pressed ? "#EFE1D0" : "#F5EDE3"

                            Text {
                                anchors.centerIn: parent
                                text: "再做一次"
                                font.pixelSize: 14
                                color: "#7A6759"
                            }

                            MouseArea {
                                id: retryArea
                                anchors.fill: parent
                                onClicked: page.loadSet(page.setIndex)
                            }
                        }

                        Rectangle {
                            width: 108
                            height: 42
                            radius: 21
                            color: closeArea.pressed ? "#00695C" : "#00897B"

                            Text {
                                anchors.centerIn: parent
                                text: "完成"
                                font.pixelSize: 14
                                font.bold: true
                                color: "#FFFFFF"
                            }

                            MouseArea {
                                id: closeArea
                                anchors.fill: parent
                                onClicked: page.resultOpen = false
                            }
                        }
                    }
                }
            }
        }
    }
}
