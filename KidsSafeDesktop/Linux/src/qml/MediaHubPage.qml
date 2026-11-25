import QtQuick
import QtQuick.Layouts
import QtQuick.Window
import KidsSafe 1.0

/*
 * 媒体中心（听一听）。
 *
 * 内容全部随包：儿歌与环境音是 assets/audio 里的 wav，国学启蒙是纯文本，
 * 由系统语音（Speech / spd-say）念 —— 和故事屋的朗读同一条链路。
 *
 * 三个状态：
 *   1. 分类浏览：顶部切换分类，下面是曲目卡片；
 *   2. 播放：点音频卡片，底部出播放条（进度、上一首/暂停/下一首）。
 *      离开页面自动停 —— 否则孩子点了「返回」，儿歌还在后台响。
 *   3. 朗读：点唐诗卡片，盖一层正文 + 「朗读 / 停止」。
 *
 * 播放逻辑在 C++ 的 Media 里（core/mediaplayer.h 说明了为什么不放 QML）。
 */
GameShell {
    id: page

    title: "听一听"
    glyph: "music"
    accent: "#EF6C8B"
    prompt: Media.sections.length > 0
            ? "点一下就开始，安睡摇篮会一直循环"
            : "内容包里没有音频，先看下面的提示"

    focus: true

    // 曲目表在这里做一次快照，分类过滤和「点第几张就播第几首」都用它。
    // 不能每次点卡片时重新读 Media.tracks：QVariantList 每次都会转成新的
    // JS 数组，对象身份变了，indexOf 永远返回 -1。
    property var allTracks: Media.tracks
    property int sectionIndex: 0
    property var section: (Media.sections.length > sectionIndex) ? Media.sections[sectionIndex] : null
    property string sectionId: section ? section.id : ""
    // 分类为空时（内容表没装、或刚切到空分类）所有绑定还会求值一次，
    // 直接写 page.section.accent 会在控制台刷 TypeError，所以统一走这个兜底色。
    readonly property color sectionAccent: section ? section.accent : "#EF6C8B"
    property var sectionTracks: allTracks.filter(function (track) {
        return track.section === page.sectionId
    })

    // 当前正在「读」的那首诗（null 表示没在读）。
    property var readingTrack: null

    function openTrack(track) {
        if (track.file !== "") {
            const index = page.allTracks.indexOf(track)
            if (!Media.play(index)) {
                // play 失败已经由 Media 把原因写进 message，这里不用再说话。
            }
            return
        }
        page.readingTrack = track
        if (Speech.available) {
            Speech.say(track.title + "。" + track.text)
        }
    }

    function closeReading() {
        Speech.stop()
        page.readingTrack = null
    }

    function timeText(ms) {
        const total = Math.max(0, Math.floor(ms / 1000))
        const minutes = Math.floor(total / 60)
        const seconds = total % 60
        return minutes + ":" + (seconds < 10 ? "0" + seconds : seconds)
    }

    Component.onCompleted: forceActiveFocus()

    // 离开页面就把声音收掉：媒体中心的音频不是「背景音乐」，没有理由
    // 在孩子回到桌面之后继续响。
    Component.onDestruction: {
        Media.stop()
        Speech.stop()
    }

    Keys.onPressed: function (event) {
        if (event.key !== Qt.Key_Escape) {
            return
        }
        event.accepted = true
        if (page.readingTrack !== null) {
            page.closeReading()
        } else {
            Media.stop()
            page.navWin.popPage()
        }
    }

    ColumnLayout {
        anchors.fill: page.contentArea
        spacing: 14

        // ---- 没有内容表 / 没有播放器：只说明，不摆装不了的按钮 ----
        Rectangle {
            Layout.fillWidth: true
            Layout.preferredHeight: 96
            radius: 22
            color: "#FFFFFF"
            border.color: "#F2E1CD"
            border.width: 1
            visible: Media.sections.length === 0 || !Media.available

            RowLayout {
                anchors.fill: parent
                anchors.margins: 18
                spacing: 16

                Rectangle {
                    Layout.preferredWidth: 56
                    Layout.preferredHeight: 56
                    radius: 18
                    color: "#FCE4EC"

                    GlyphIcon {
                        anchors.centerIn: parent
                        width: 30
                        height: 30
                        name: "speaker"
                        color: "#C2185B"
                    }
                }

                ColumnLayout {
                    Layout.fillWidth: true
                    spacing: 4

                    Text {
                        text: Media.sections.length === 0 ? "还没有内容" : "这个版本放不了音频"
                        font.pixelSize: 19
                        font.weight: Font.Bold
                        color: "#4A3B32"
                    }

                    Text {
                        Layout.fillWidth: true
                        text: Media.detail
                        font.pixelSize: 13
                        color: "#9B8574"
                        wrapMode: Text.Wrap
                    }
                }
            }
        }

        // ---- 分类切换 ----
        RowLayout {
            Layout.fillWidth: true
            spacing: 10
            visible: Media.sections.length > 0

            Repeater {
                model: Media.sections

                Rectangle {
                    required property var modelData
                    required property int index

                    Layout.preferredWidth: chipText.implicitWidth + 66
                    Layout.preferredHeight: 52
                    radius: 26
                    color: index === page.sectionIndex ? modelData.accent : "#FFFFFF"
                    border.color: index === page.sectionIndex ? modelData.accent : "#F2E1CD"
                    border.width: 1

                    RowLayout {
                        anchors.centerIn: parent
                        spacing: 8

                        GlyphIcon {
                            Layout.preferredWidth: 22
                            Layout.preferredHeight: 22
                            name: modelData.glyph
                            color: index === page.sectionIndex ? "#FFFFFF" : modelData.accent
                        }

                        Text {
                            id: chipText

                            text: modelData.title + " " + modelData.count
                            font.pixelSize: 16
                            font.weight: Font.DemiBold
                            color: index === page.sectionIndex ? "#FFFFFF" : "#7A6759"
                        }
                    }

                    TapHandler {
                        onTapped: {
                            page.sectionIndex = index
                            page.forceActiveFocus()
                        }
                    }
                }
            }

            Item { Layout.fillWidth: true }

            Text {
                text: page.section ? page.section.hint : ""
                font.pixelSize: 13
                color: "#9B8574"
                elide: Text.ElideRight
                Layout.maximumWidth: 420
            }
        }

        // ---- 曲目卡片 ----
        Flickable {
            Layout.fillWidth: true
            Layout.fillHeight: true
            contentWidth: width
            contentHeight: trackGrid.height
            clip: true
            boundsBehavior: Flickable.StopAtBounds
            visible: page.sectionTracks.length > 0

            Grid {
                id: trackGrid

                width: parent.width
                columns: Math.max(2, Math.min(5, Math.floor(width / 210)))
                spacing: 14
                topPadding: 2

                Repeater {
                    model: page.sectionTracks

                    Rectangle {
                        required property var modelData

                        readonly property bool active: Media.current >= 0
                                                       && page.allTracks.indexOf(modelData) === Media.current

                        width: Math.floor((trackGrid.width - (trackGrid.columns - 1) * trackGrid.spacing)
                                          / trackGrid.columns)
                        height: 112
                        radius: 24
                        color: "#FFFFFF"
                        border.color: active || cardTap.pressed ? page.sectionAccent : "#F2E1CD"
                        border.width: active || cardTap.pressed ? 2 : 1

                        scale: cardHover.hovered ? 1.02 : 1.0
                        Behavior on scale {
                            NumberAnimation { duration: 110; easing.type: Easing.OutCubic }
                        }

                        ColumnLayout {
                            anchors.fill: parent
                            anchors.margins: 14
                            spacing: 4

                            RowLayout {
                                Layout.fillWidth: true
                                spacing: 8

                                Rectangle {
                                    Layout.preferredWidth: 36
                                    Layout.preferredHeight: 36
                                    radius: 12
                                    color: Qt.lighter(page.sectionAccent, 1.75)

                                    GlyphIcon {
                                        anchors.centerIn: parent
                                        width: 20
                                        height: 20
                                        name: modelData.text !== "" ? "book" : (modelData.loop ? "moon" : "music")
                                        color: page.sectionAccent
                                    }
                                }

                                Item { Layout.fillWidth: true }
                            }

                            Item { Layout.fillHeight: true }

                            Text {
                                Layout.fillWidth: true
                                text: modelData.title
                                font.pixelSize: 16
                                font.weight: Font.DemiBold
                                color: "#4A3B32"
                                elide: Text.ElideRight
                                maximumLineCount: 1
                            }

                            Text {
                                Layout.fillWidth: true
                                text: modelData.subtitle
                                font.pixelSize: 12
                                color: "#B8A492"
                                elide: Text.ElideRight
                                maximumLineCount: 1
                            }
                        }

                        HoverHandler { id: cardHover }

                        TapHandler {
                            id: cardTap
                            onTapped: {
                                page.openTrack(modelData)
                                page.forceActiveFocus()
                            }
                        }
                    }
                }
            }
        }

        // 分类存在但里面是空的（家长删过 library.json 里的曲目）：
        // 给一句话，而不是让内容区一片白。
        Text {
            Layout.fillWidth: true
            Layout.fillHeight: true
            visible: Media.sections.length > 0 && page.sectionTracks.length === 0
            text: "这个分类还没有内容，可以让家长在 library.json 里加"
            font.pixelSize: 15
            color: "#9B8574"
            horizontalAlignment: Text.AlignHCenter
            verticalAlignment: Text.AlignVCenter
            wrapMode: Text.Wrap
        }

        // ---- 播放条 ----
        Rectangle {
            Layout.fillWidth: true
            Layout.preferredHeight: 78
            radius: 24
            color: "#FFFFFF"
            border.color: "#F2E1CD"
            border.width: 1
            visible: Media.current >= 0

            RowLayout {
                anchors.fill: parent
                anchors.margins: 12
                spacing: 14

                Rectangle {
                    Layout.preferredWidth: 52
                    Layout.preferredHeight: 52
                    radius: 17
                    color: Qt.lighter(page.sectionAccent, 1.75)

                    GlyphIcon {
                        anchors.centerIn: parent
                        width: 26
                        height: 26
                        name: "music"
                        color: page.sectionAccent
                    }
                }

                ColumnLayout {
                    Layout.preferredWidth: 190
                    spacing: 2

                    Text {
                        Layout.fillWidth: true
                        text: Media.title
                        font.pixelSize: 16
                        font.weight: Font.DemiBold
                        color: "#4A3B32"
                        elide: Text.ElideRight
                    }

                    Text {
                        Layout.fillWidth: true
                        text: Media.subtitle
                        font.pixelSize: 12
                        color: "#B8A492"
                        elide: Text.ElideRight
                    }
                }

                // 进度：点哪儿跳哪儿。只画不给点的话，孩子会一直戳它。
                Item {
                    id: progressArea

                    Layout.fillWidth: true
                    Layout.preferredHeight: 30

                    Rectangle {
                        anchors.verticalCenter: parent.verticalCenter
                        width: parent.width
                        height: 8
                        radius: 4
                        color: "#F1E4D5"

                        Rectangle {
                            width: Media.duration > 0
                                   ? parent.width * Math.min(1, Media.position / Media.duration)
                                   : 0
                            height: parent.height
                            radius: 4
                            color: page.sectionAccent
                        }
                    }

                    TapHandler {
                        onTapped: function (event) {
                            if (progressArea.width > 0) {
                                Media.seekTo(event.position.x / progressArea.width)
                            }
                        }
                    }
                }

                Text {
                    text: page.timeText(Media.position) + " / " + page.timeText(Media.duration)
                    font.pixelSize: 12
                    color: "#9B8574"
                }

                LargeButton {
                    Layout.preferredWidth: 64
                    Layout.preferredHeight: 46
                    text: "‹"
                    accentColor: "#EFE1D0"
                    labelColor: "#7A6759"
                    onClicked: Media.previous()
                }

                LargeButton {
                    Layout.preferredWidth: 92
                    Layout.preferredHeight: 46
                    text: Media.playing ? "暂停" : "播放"
                    accentColor: page.sectionAccent
                    onClicked: Media.toggle()
                }

                LargeButton {
                    Layout.preferredWidth: 64
                    Layout.preferredHeight: 46
                    text: "›"
                    accentColor: "#EFE1D0"
                    labelColor: "#7A6759"
                    onClicked: Media.next()
                }

                LargeButton {
                    Layout.preferredWidth: 84
                    Layout.preferredHeight: 46
                    text: "停止"
                    accentColor: "#FFFFFF"
                    labelColor: "#7A6759"
                    onClicked: Media.stop()
                }
            }
        }
    }

    // ================= 朗读（国学）=================
    Rectangle {
        anchors.fill: page
        z: 60
        visible: page.readingTrack !== null
        color: "#F2FFF8EE"

        MouseArea {
            anchors.fill: parent
            acceptedButtons: Qt.AllButtons
        }

        Rectangle {
            anchors.centerIn: parent
            width: Math.min(parent.width - 120, 620)
            height: Math.min(parent.height - 120, 460)
            radius: 28
            color: "#FFFFFF"
            border.color: "#F2E1CD"
            border.width: 1

            ColumnLayout {
                anchors.fill: parent
                anchors.margins: 28
                spacing: 14

                RowLayout {
                    Layout.fillWidth: true
                    spacing: 12

                    GlyphIcon {
                        Layout.preferredWidth: 34
                        Layout.preferredHeight: 34
                        name: "book"
                        color: "#8D6E63"
                    }

                    ColumnLayout {
                        Layout.fillWidth: true
                        spacing: 2

                        Text {
                            text: page.readingTrack ? page.readingTrack.title : ""
                            font.pixelSize: 24
                            font.weight: Font.Bold
                            color: "#4A3B32"
                        }

                        Text {
                            text: page.readingTrack ? page.readingTrack.subtitle : ""
                            font.pixelSize: 13
                            color: "#B8A492"
                        }
                    }
                }

                Rectangle {
                    Layout.fillWidth: true
                    Layout.fillHeight: true
                    radius: 20
                    color: "#FFF8EE"

                    Text {
                        anchors.centerIn: parent
                        width: parent.width - 56
                        text: page.readingTrack ? page.readingTrack.text : ""
                        font.pixelSize: 26
                        lineHeight: 1.6
                        color: "#4A3B32"
                        horizontalAlignment: Text.AlignHCenter
                        wrapMode: Text.Wrap
                    }
                }

                RowLayout {
                    Layout.fillWidth: true
                    spacing: 12

                    LargeButton {
                        Layout.fillWidth: true
                        Layout.preferredHeight: 54
                        text: Speech.available ? "朗读一遍" : "没有语音（家长可装语音包）"
                        enabled: Speech.available
                        accentColor: "#8D6E63"
                        onClicked: Speech.say(page.readingTrack.title + "。" + page.readingTrack.text)
                    }

                    LargeButton {
                        Layout.preferredWidth: 140
                        Layout.preferredHeight: 54
                        text: "停止朗读"
                        accentColor: "#EFE1D0"
                        labelColor: "#7A6759"
                        onClicked: Speech.stop()
                    }

                    LargeButton {
                        Layout.preferredWidth: 120
                        Layout.preferredHeight: 54
                        text: "关闭"
                        accentColor: "#FFFFFF"
                        labelColor: "#7A6759"
                        onClicked: page.closeReading()
                    }
                }
            }
        }
    }

    // ---- 一句话提示（放不了 / 播完了）----
    Rectangle {
        anchors.horizontalCenter: parent.horizontalCenter
        anchors.top: parent.top
        anchors.topMargin: 6
        z: 70
        width: Math.min(parent.width - 40, hintText.implicitWidth + 44)
        height: 44
        radius: 22
        color: "#C2185B"
        visible: Media.message.length > 0

        Text {
            id: hintText

            anchors.centerIn: parent
            text: Media.message
            font.pixelSize: 15
            font.weight: Font.DemiBold
            color: "white"
        }
    }
}
