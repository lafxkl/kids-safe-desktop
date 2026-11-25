import QtQuick
import QtQuick.Controls
import QtQuick.Layouts
import QtQuick.Window
import "SafetyQuiz.js" as SafetyQuiz

/*
 * 安全闯关：从 40 道题库里抽 10 道，选项顺序也打乱。
 *
 * 三个状态共用一个页面（intro / playing / result），用 visible 切换，
 * 不另开页面——闯关的「开始 → 做题 → 结果」是一个连续流程，拆页会让
 * 返回栈变得奇怪（做完题按一次返回应该回安全学堂，不是回上一题）。
 *
 * 页面属性刻意叫 questionIndex 而不是 index：Repeater 的 delegate 里
 * 也注入了 index，两者同名时 QML 会静默取 delegate 的那个，是个很难查的坑。
 */
Item {
    id: page

    readonly property var navWin: Window.window

    property string phase: "intro"      // intro | playing | result
    property var round: []
    property int questionIndex: 0
    property int score: 0
    property int picked: -1
    property bool revealed: false

    readonly property var current: (round.length > 0
                                    && questionIndex >= 0
                                    && questionIndex < round.length)
                                   ? round[questionIndex] : null

    function startRound() {
        page.round = SafetyQuiz.buildRound();
        page.questionIndex = 0;
        page.score = 0;
        page.picked = -1;
        page.revealed = false;
        page.phase = "playing";
    }

    function choose(optionIndex) {
        if (page.revealed || page.current === null) {
            return;
        }
        page.picked = optionIndex;
        page.revealed = true;
        if (optionIndex === page.current.correct) {
            page.score += 1;
        }
    }

    function next() {
        if (page.questionIndex + 1 < page.round.length) {
            page.questionIndex += 1;
            page.picked = -1;
            page.revealed = false;
        } else {
            page.phase = "result";
        }
    }

    function optionColor(optionIndex) {
        if (!page.revealed || page.current === null) {
            return "#FFFFFF";
        }
        if (optionIndex === page.current.correct) {
            return "#E8F7F1";
        }
        if (optionIndex === page.picked) {
            return "#FDECEA";
        }
        return "#FFFFFF";
    }

    function optionBorder(optionIndex) {
        if (!page.revealed || page.current === null) {
            return "#F2E1CD";
        }
        if (optionIndex === page.current.correct) {
            return "#1D9E75";
        }
        if (optionIndex === page.picked) {
            return "#C0392B";
        }
        return "#F2E1CD";
    }

    Backdrop {
        anchors.fill: parent
    }

    ColumnLayout {
        anchors.fill: parent
        anchors.margins: 28
        spacing: 16

        // ---- 顶栏 ----
        RowLayout {
            Layout.fillWidth: true
            spacing: 16

            Rectangle {
                implicitWidth: 56
                implicitHeight: 56
                radius: 28
                color: "#FFFFFF"
                border.color: "#F2E1CD"
                border.width: 1

                Text {
                    anchors.centerIn: parent
                    text: "‹"
                    font.pixelSize: 34
                    color: "#7A6759"
                }

                TapHandler {
                    onTapped: page.navWin.popPage()
                }
            }

            Text {
                text: "安全闯关"
                font.pixelSize: 30
                font.weight: Font.Bold
                color: "#4A3B32"
            }

            Item {
                Layout.fillWidth: true
            }

            Text {
                visible: page.phase === "playing"
                text: "第 " + (page.questionIndex + 1) + " / " + page.round.length + " 题 · 答对 "
                      + page.score
                font.pixelSize: 17
                color: "#9B8574"
            }
        }

        // ---- 内容 ----
        Flickable {
            id: flick

            Layout.fillWidth: true
            Layout.fillHeight: true
            clip: true
            contentWidth: width
            contentHeight: body.implicitHeight + 8
            boundsBehavior: Flickable.StopAtBounds

            ColumnLayout {
                id: body

                width: Math.min(flick.width, 900)
                x: (flick.width - width) / 2
                spacing: 18

                // ── 开始页 ──
                Item {
                    Layout.fillWidth: true
                    Layout.preferredHeight: page.phase === "intro" ? introCard.height : 0
                    visible: page.phase === "intro"

                    Rectangle {
                        id: introCard

                        width: parent.width
                        // 高度由内容算，不写成 anchors.fill 的关系：
                        // 「父高 ← 子 implicitHeight」和「子填满父」同时存在就是绑定循环，
                        // QML 会警告 Binding loop 并把高度算成 0，界面直接空白。
                        height: introBody.implicitHeight + 64
                        radius: 26
                        color: "#FFFFFF"
                        border.color: "#F2E1CD"
                        border.width: 1

                        ColumnLayout {
                            id: introBody

                            x: 32
                            y: 32
                            width: parent.width - 64
                            spacing: 14

                            GlyphIcon {
                                width: 60
                                height: 60
                                name: "shield"
                                color: "#E0544B"
                            }

                            Text {
                                text: "来做 10 道安全题"
                                font.pixelSize: 26
                                font.weight: Font.Bold
                                color: "#4A3B32"
                            }

                            Text {
                                text: "题目从 " + SafetyQuiz.QUESTIONS.length
                                      + " 道安全常识里随机抽，每次都不一样。\n选完会告诉你为什么，答错也没关系——记住比答对重要。"
                                font.pixelSize: 17
                                color: "#7A6759"
                                wrapMode: Text.Wrap
                                lineHeight: 1.5
                            }

                            Item {
                                Layout.preferredHeight: 6
                            }

                            LargeButton {
                                Layout.preferredWidth: 220
                                Layout.preferredHeight: 62
                                text: "开始闯关"
                                accentColor: "#FF8A5C"
                                onClicked: page.startRound()
                            }
                        }
                    }
                }

                // ── 答题 ──
                Item {
                    Layout.fillWidth: true
                    Layout.preferredHeight: page.phase === "playing" ? questionBody.implicitHeight : 0
                    visible: page.phase === "playing" && page.current !== null

                    ColumnLayout {
                        id: questionBody

                        width: parent.width
                        spacing: 16

                        Rectangle {
                            Layout.fillWidth: true
                            radius: 26
                            color: "#FFFFFF"
                            border.color: "#F2E1CD"
                            border.width: 1
                            implicitHeight: stem.implicitHeight + 48

                            ColumnLayout {
                                id: stem

                                x: 24
                                y: 24
                                width: parent.width - 48
                                spacing: 10

                                RowLayout {
                                    spacing: 10

                                    Text {
                                        text: page.current ? page.current.emoji : ""
                                        font.pixelSize: 30
                                    }

                                    Rectangle {
                                        radius: 12
                                        color: "#FFF3E6"
                                        implicitWidth: categoryText.implicitWidth + 22
                                        implicitHeight: 30

                                        Text {
                                            id: categoryText

                                            anchors.centerIn: parent
                                            text: page.current ? page.current.category : ""
                                            font.pixelSize: 14
                                            color: "#8A7259"
                                        }
                                    }
                                }

                                Text {
                                    Layout.fillWidth: true
                                    text: page.current ? page.current.question : ""
                                    font.pixelSize: 23
                                    font.weight: Font.DemiBold
                                    color: "#4A3B32"
                                    wrapMode: Text.Wrap
                                    lineHeight: 1.4
                                }
                            }
                        }

                        Repeater {
                            model: page.current ? page.current.options : []

                            delegate: Rectangle {
                                Layout.fillWidth: true
                                Layout.preferredHeight: Math.max(72, optionLabel.implicitHeight + 36)
                                radius: 18
                                color: page.optionColor(index)
                                border.color: page.optionBorder(index)
                                border.width: 2

                                Behavior on color {
                                    ColorAnimation { duration: 140 }
                                }

                                RowLayout {
                                    anchors.fill: parent
                                    anchors.margins: 18
                                    spacing: 14

                                    Text {
                                        text: ["A", "B", "C", "D"][index]
                                        font.pixelSize: 16
                                        font.weight: Font.Bold
                                        color: "#B8A492"
                                    }

                                    Text {
                                        id: optionLabel

                                        Layout.fillWidth: true
                                        text: modelData
                                        font.pixelSize: 19
                                        color: "#4A3B32"
                                        wrapMode: Text.Wrap
                                    }

                                    Text {
                                        visible: page.revealed && page.current !== null
                                                 && index === page.current.correct
                                        text: "✓"
                                        font.pixelSize: 24
                                        color: "#1D9E75"
                                    }

                                    Text {
                                        visible: page.revealed && index === page.picked
                                                 && page.current !== null
                                                 && index !== page.current.correct
                                        text: "✕"
                                        font.pixelSize: 24
                                        color: "#C0392B"
                                    }
                                }

                                TapHandler {
                                    onTapped: page.choose(index)
                                }
                            }
                        }

                        // 讲解 + 下一题
                        Rectangle {
                            Layout.fillWidth: true
                            visible: page.revealed
                            radius: 20
                            color: "#FFF8EE"
                            border.color: "#F2E1CD"
                            border.width: 1
                            implicitHeight: explainBody.implicitHeight + 40

                            ColumnLayout {
                                id: explainBody

                                x: 20
                                y: 20
                                width: parent.width - 40
                                spacing: 12

                                Text {
                                    Layout.fillWidth: true
                                    text: (page.current !== null && page.picked === page.current.correct)
                                          ? "答对啦！" : "这次没答对，记住就好："
                                    font.pixelSize: 19
                                    font.weight: Font.Bold
                                    color: (page.current !== null && page.picked === page.current.correct)
                                           ? "#1D9E75" : "#C0392B"
                                }

                                Text {
                                    Layout.fillWidth: true
                                    text: page.current ? page.current.explain : ""
                                    font.pixelSize: 17
                                    color: "#5B4A3E"
                                    wrapMode: Text.Wrap
                                    lineHeight: 1.5
                                }

                                LargeButton {
                                    Layout.preferredWidth: 200
                                    Layout.preferredHeight: 58
                                    text: (page.questionIndex + 1 < page.round.length) ? "下一题 ›" : "看结果 ›"
                                    accentColor: "#FF8A5C"
                                    onClicked: page.next()
                                }
                            }
                        }

                        Item {
                            Layout.preferredHeight: 8
                        }
                    }
                }

                // ── 结果页 ──
                Item {
                    Layout.fillWidth: true
                    Layout.preferredHeight: page.phase === "result" ? resultCard.height : 0
                    visible: page.phase === "result"

                    Rectangle {
                        id: resultCard

                        width: parent.width
                        height: resultBody.implicitHeight + 64
                        radius: 26
                        color: "#FFFFFF"
                        border.color: "#F2E1CD"
                        border.width: 1

                        ColumnLayout {
                            id: resultBody

                            x: 32
                            y: 32
                            width: parent.width - 64
                            spacing: 14

                            GlyphIcon {
                                width: 68
                                height: 68
                                name: page.score === page.round.length ? "trophy" : "star"
                                color: "#F2A03D"
                            }

                            Text {
                                text: "答对 " + page.score + " / " + page.round.length + " 题"
                                font.pixelSize: 28
                                font.weight: Font.Bold
                                color: "#4A3B32"
                            }

                            Text {
                                Layout.fillWidth: true
                                text: SafetyQuiz.verdict(page.score, page.round.length)
                                font.pixelSize: 18
                                color: "#7A6759"
                                wrapMode: Text.Wrap
                            }

                            Item {
                                Layout.preferredHeight: 8
                            }

                            RowLayout {
                                spacing: 14

                                LargeButton {
                                    Layout.preferredWidth: 200
                                    Layout.preferredHeight: 60
                                    text: "再来一轮"
                                    accentColor: "#FF8A5C"
                                    onClicked: page.startRound()
                                }

                                LargeButton {
                                    Layout.preferredWidth: 200
                                    Layout.preferredHeight: 60
                                    text: "回安全学堂"
                                    accentColor: "#EFE1D0"
                                    labelColor: "#7A6759"
                                    onClicked: page.navWin.popPage()
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
