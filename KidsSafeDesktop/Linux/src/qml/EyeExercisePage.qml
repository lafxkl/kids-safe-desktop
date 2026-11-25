import QtQuick
import QtQuick.Layouts
import QtQuick.Controls
import KidsSafe 1.0

/*
 * 眼保健操（对应 Windows 端 EyeExercisePage / EyeExerciseData.cs）。
 *
 * 六节：放松 → 睛明穴 → 四白穴 → 太阳穴刮眼眶 → 转眼球 → 远眺。
 * 每节有固定时长，倒计时环走完自动进下一节；开始时用 speech-dispatcher
 * 念一遍口诀（没装 spd-say 时静默降级，界面照常走）。
 *
 * 为什么用环形进度而不是进度条：孩子看不懂「72%」，但能看懂「圈还剩多少」，
 * 而且中间那块正好放眼睛图标和剩余秒数，不用来回扫视。
 *
 * 计时严格用 1 秒 Timer 递减，不用动画驱动。动画在窗口失焦、显示器休眠后
 * 会被节流，而眼睛该做多久是**时间**问题，不是帧率问题。
 */
GameShell {
    id: page

    title: "眼保健操"
    glyph: "eye"
    accent: "#7E57C2"
    prompt: page.finished ? "全部做完啦，眼睛亮晶晶！" : page.steps[page.index].name
    scoreText: page.finished ? "" : ("第 " + (page.index + 1) + " / " + page.steps.length + " 节")

    // 节数、时长、口诀。文案与 Windows 端 EyeExerciseData 同一套，
    // 只是那边用 emoji 当图标，这里统一走 GlyphIcon（deepin 没有彩色 emoji 字体）。
    readonly property var steps: [
        {
            "name": "闭上眼睛，放松一下",
            "tip": "坐正，肩膀放平，轻轻闭上眼睛，慢慢吸气、呼气。",
            "seconds": 15
        },
        {
            "name": "按揉睛明穴",
            "tip": "用拇指和食指捏住鼻梁两侧的小凹陷，轻轻按揉，一下一下数着来。",
            "seconds": 32
        },
        {
            "name": "按揉四白穴",
            "tip": "两手食指按在眼眶下缘正中的位置，轻轻揉一揉，别用力压眼球。",
            "seconds": 32
        },
        {
            "name": "按揉太阳穴，刮刮上眼眶",
            "tip": "拇指按住太阳穴，食指弯起来，从眉头沿着上眼眶轻轻刮到眉尾。",
            "seconds": 32
        },
        {
            "name": "转动小眼球",
            "tip": "头不要动，眼球慢慢向上、向右、向下、向左转一圈，再反方向转一圈。",
            "seconds": 24
        },
        {
            "name": "看看远处",
            "tip": "望向窗外最远的地方，眨眨眼睛，让眼睛休息一下。",
            "seconds": 20
        }
    ]

    property int index: 0
    property int remaining: 0
    property bool running: false
    property bool finished: false

    /// 当前这一节的总秒数，用于算环的进度
    readonly property int total: page.steps[Math.min(page.index, page.steps.length - 1)].seconds

    function speakCurrent() {
        // 语音不可用时不调用（C++ 那边也会拦，但少一次跨语言调用、少一条日志）
        if (!Speech.available) {
            return;
        }
        const step = page.steps[page.index];
        Speech.say(step.name + "。" + step.tip);
    }

    function goToStep(i) {
        page.index = Math.max(0, Math.min(i, page.steps.length - 1));
        page.remaining = page.steps[page.index].seconds;
        page.speakCurrent();
    }

    function restart() {
        page.finished = false;
        page.running = false;
        page.goToStep(0);
    }

    function nextStep() {
        if (page.index >= page.steps.length - 1) {
            page.finished = true;
            page.running = false;
            Speech.say("全部做完了，眼睛真舒服。记得每天做一次眼保健操。");
            return;
        }
        page.goToStep(page.index + 1);
    }

    Component.onCompleted: {
        page.remaining = page.steps[0].seconds;
    }

    // 离开页面就别再念了，否则孩子已经回首页，耳朵里还在念口诀
    Component.onDestruction: Speech.stop()

    Timer {
        id: tick

        interval: 1000
        repeat: true
        running: page.running && !page.finished

        onTriggered: {
            if (page.remaining > 1) {
                page.remaining -= 1;
            } else {
                page.nextStep();
            }
        }
    }

    Item {
        anchors.fill: page.contentArea

        RowLayout {
            anchors.fill: parent
            anchors.margins: 8
            spacing: 44

            // ---- 左：倒计时环 ----
            Item {
                Layout.preferredWidth: 330
                Layout.preferredHeight: 330
                Layout.alignment: Qt.AlignVCenter

                Canvas {
                    id: ring

                    anchors.fill: parent

                    // 三个属性之一变化都要重画：秒数在走、节在换、刚做完
                    Connections {
                        target: page

                        function onRemainingChanged() { ring.requestPaint() }
                        function onIndexChanged() { ring.requestPaint() }
                        function onFinishedChanged() { ring.requestPaint() }
                    }

                    onPaint: {
                        const ctx = getContext("2d");
                        ctx.reset();

                        const cx = width / 2;
                        const cy = height / 2;
                        const r = Math.min(width, height) / 2 - 16;

                        // 底圈
                        ctx.beginPath();
                        ctx.arc(cx, cy, r, 0, 2 * Math.PI);
                        ctx.lineWidth = 18;
                        ctx.strokeStyle = "#EDE7F6";
                        ctx.stroke();

                        // 进度圈：从 12 点方向顺时针
                        const done = page.finished ? 1 : (1 - page.remaining / Math.max(1, page.total));
                        if (done > 0.001) {
                            ctx.beginPath();
                            ctx.arc(cx, cy, r, -Math.PI / 2, -Math.PI / 2 + 2 * Math.PI * done);
                            ctx.lineWidth = 18;
                            ctx.lineCap = "round";
                            ctx.strokeStyle = "#7E57C2";
                            ctx.stroke();
                        }
                    }
                }

                Column {
                    anchors.centerIn: parent
                    spacing: 4

                    GlyphIcon {
                        anchors.horizontalCenter: parent.horizontalCenter
                        width: 62
                        height: 62
                        name: "eye"
                        color: "#7E57C2"
                    }

                    Text {
                        anchors.horizontalCenter: parent.horizontalCenter
                        text: page.finished ? "完成" : page.remaining
                        font.pixelSize: page.finished ? 40 : 54
                        font.weight: Font.Bold
                        color: "#4A3B32"
                    }

                    Text {
                        anchors.horizontalCenter: parent.horizontalCenter
                        visible: !page.finished
                        text: "秒"
                        font.pixelSize: 16
                        color: "#9B8574"
                    }
                }
            }

            // ---- 右：说明 + 操作 ----
            ColumnLayout {
                Layout.fillWidth: true
                Layout.fillHeight: true
                spacing: 16

                Text {
                    Layout.fillWidth: true
                    text: page.finished ? "真棒，眼睛休息好啦！"
                                        : ("第 " + (page.index + 1) + " 节 · " + page.steps[page.index].name)
                    font.pixelSize: 30
                    font.weight: Font.Bold
                    color: "#4A3B32"
                    wrapMode: Text.Wrap
                }

                Text {
                    Layout.fillWidth: true
                    text: page.finished ? "每天做一次，眼睛不容易累。现在可以回去玩啦。"
                                        : page.steps[page.index].tip
                    font.pixelSize: 20
                    lineHeight: 1.35
                    color: "#6D5B4E"
                    wrapMode: Text.Wrap
                }

                Rectangle {
                    Layout.fillWidth: true
                    Layout.preferredHeight: 62
                    radius: 18
                    color: "#F6F1FF"
                    border.color: "#E3D8FA"
                    border.width: 1

                    RowLayout {
                        anchors.fill: parent
                        anchors.margins: 14
                        spacing: 12

                        GlyphIcon {
                            Layout.preferredWidth: 24
                            Layout.preferredHeight: 24
                            name: "star"
                            color: "#8E7CFF"
                        }

                        Text {
                            Layout.fillWidth: true
                            text: "做操时坐正、手要干净，动作轻一点，别压到眼球。"
                            font.pixelSize: 15
                            color: "#6D5B4E"
                            wrapMode: Text.Wrap
                        }
                    }
                }

                // 节次圆点：一眼看出做到第几节
                Row {
                    spacing: 9

                    Repeater {
                        model: page.steps.length

                        Rectangle {
                            required property int index

                            width: index === page.index && !page.finished ? 26 : 10
                            height: 10
                            radius: 5
                            color: index < page.index || page.finished ? "#C5B3E8"
                                                                       : (index === page.index ? "#7E57C2" : "#E6E0F0")

                            Behavior on width {
                                NumberAnimation { duration: 140 }
                            }
                        }
                    }
                }

                Item {
                    Layout.fillHeight: true
                }

                // 语音不可用时说清楚原因。不给提示的话，孩子会以为「重听口诀」
                // 这个按钮坏了，一直点。
                Text {
                    Layout.fillWidth: true
                    visible: !Speech.available
                    text: "这台电脑没有可用的语音组件，口诀只显示不朗读（家长可在「家长后台」里查看和重试）。"
                    font.pixelSize: 13
                    color: "#B8A492"
                    wrapMode: Text.Wrap
                }

                RowLayout {
                    Layout.fillWidth: true
                    spacing: 14

                    LargeButton {
                        Layout.preferredWidth: 170
                        Layout.preferredHeight: 64
                        text: page.finished ? "再做一遍" : (page.running ? "暂停" : "开始")
                        accentColor: "#7E57C2"
                        onClicked: {
                            if (page.finished) {
                                page.restart();
                                page.running = true;
                                return;
                            }
                            // 从「没开始」直接点开始时念一遍口诀；暂停后继续不重复念
                            if (!page.running && page.remaining === page.total) {
                                page.speakCurrent();
                            }
                            page.running = !page.running;
                        }
                    }

                    LargeButton {
                        Layout.preferredWidth: 150
                        Layout.preferredHeight: 64
                        text: "下一节"
                        accentColor: "#FFFFFF"
                        labelColor: "#7A6759"
                        enabled: !page.finished
                        onClicked: page.nextStep()
                    }

                    LargeButton {
                        Layout.preferredWidth: 150
                        Layout.preferredHeight: 64
                        text: Speech.available ? "重听口诀" : "无语音"
                        accentColor: "#FFFFFF"
                        labelColor: "#7A6759"
                        // 检测是异步的：刚进页面那一下可能还没结果，先按住按钮，
                        // 探测完成（Speech.available 变化）后自动解禁。
                        enabled: !page.finished && Speech.available
                        onClicked: page.speakCurrent()
                    }

                    Item {
                        Layout.fillWidth: true
                    }
                }
            }
        }
    }
}
