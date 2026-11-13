import QtQuick
import QtQuick.Controls
import QtQuick.Layouts
import KidsSafe 1.0

Item {
    id: page

    property int currentTypeId: -1

    function pickCategory(id) {
        page.currentTypeId = id;
        Stories.loadList(id, 1);
    }

    Backdrop {
        anchors.fill: parent
    }

    ColumnLayout {
        anchors.fill: parent
        anchors.margins: 28
        spacing: 14

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
                    onTapped: StackView.view.pop()
                }
            }

            Text {
                text: "故事屋"
                font.pixelSize: 30
                font.weight: Font.Bold
                color: "#4A3B32"
            }

            Item {
                Layout.fillWidth: true
            }

            TextField {
                id: searchField

                Layout.preferredWidth: 300
                Layout.preferredHeight: 52
                placeholderText: "找一找故事…"
                font.pixelSize: 17
                onAccepted: {
                    if (text.trim().length > 0) {
                        page.currentTypeId = -1;
                        Stories.search(text.trim(), 1);
                    }
                }
            }
        }

        // ---- 分类 ----
        ListView {
            id: chips

            Layout.fillWidth: true
            Layout.preferredHeight: 50
            orientation: ListView.Horizontal
            spacing: 10
            clip: true
            model: Stories.categories

            delegate: Rectangle {
                width: chipText.implicitWidth + 40
                height: 46
                radius: 23
                color: page.currentTypeId === modelData.id ? "#FF8A5C" : "#FFFFFF"
                border.color: page.currentTypeId === modelData.id ? "#FF8A5C" : "#F2E1CD"
                border.width: 1

                Behavior on color {
                    ColorAnimation { duration: 140 }
                }

                Text {
                    id: chipText

                    anchors.centerIn: parent
                    text: modelData.emoji + " " + modelData.name
                    font.pixelSize: 16
                    color: page.currentTypeId === modelData.id ? "white" : "#4A3B32"
                }

                TapHandler {
                    onTapped: page.pickCategory(modelData.id)
                }
            }
        }

        // ---- 故事列表 ----
        ListView {
            id: list

            Layout.fillWidth: true
            Layout.fillHeight: true
            clip: true
            spacing: 12
            model: Stories.items

            // 滚到底自动加载下一页。用 movementEnded 而不是 contentYChanged，
            // 避免滑动过程中反复发请求。
            onMovementEnded: {
                if (atYEnd && Stories.hasMore && !Stories.loading && page.currentTypeId > 0) {
                    Stories.loadMore();
                }
            }

            delegate: Rectangle {
                width: list.width
                height: 96
                radius: 20
                color: "#FFFFFF"
                border.color: "#F2E1CD"
                border.width: 1

                RowLayout {
                    anchors.fill: parent
                    anchors.margins: 16
                    spacing: 16

                    Rectangle {
                        Layout.preferredWidth: 60
                        Layout.preferredHeight: 60
                        radius: 18
                        color: "#FFE7CE"

                        Text {
                            anchors.centerIn: parent
                            text: "📖"
                            font.pixelSize: 28
                        }
                    }

                    ColumnLayout {
                        Layout.fillWidth: true
                        spacing: 4

                        Text {
                            Layout.fillWidth: true
                            text: modelData.title || "（无标题）"
                            font.pixelSize: 19
                            font.weight: Font.DemiBold
                            color: "#4A3B32"
                            elide: Text.ElideRight
                        }

                        Text {
                            Layout.fillWidth: true
                            text: [modelData.type, modelData.readTime].filter(function (s) {
                                return s && s.length > 0;
                            }).join(" · ")
                            font.pixelSize: 13
                            color: "#9B8574"
                            elide: Text.ElideRight
                        }
                    }

                    Text {
                        text: "›"
                        font.pixelSize: 30
                        color: "#C8B4A2"
                    }
                }

                TapHandler {
                    onTapped: StackView.view.push(Qt.resolvedUrl("StoryReaderPage.qml"), {
                        "storyId": modelData.id,
                        "storyTitle": modelData.title
                    })
                }
            }
        }
    }

    // ---- 状态提示：离线 / 加载中 / 无结果 ----
    ColumnLayout {
        anchors.centerIn: list
        spacing: 14
        visible: Stories.items.length === 0 && !Stories.loading

        Text {
            Layout.alignment: Qt.AlignHCenter
            text: Stories.onlineEnabled ? (Stories.errorText.length > 0 ? "😴" : "📚") : "🔌"
            font.pixelSize: 56
        }

        Text {
            Layout.alignment: Qt.AlignHCenter
            text: {
                if (!Stories.onlineEnabled)
                    return "家长还没开启在线故事";
                if (Stories.errorText.length > 0)
                    return Stories.errorText;
                return "选一个分类，开始听故事吧";
            }
            font.pixelSize: 19
            color: "#B8A492"
        }

        Text {
            Layout.alignment: Qt.AlignHCenter
            visible: !Stories.onlineEnabled
            text: "需要家长在后台填写服务器地址并打开「在线内容」"
            font.pixelSize: 15
            color: "#C8B4A2"
        }
    }

    BusyIndicator {
        anchors.centerIn: list
        width: 54
        height: 54
        running: Stories.loading
        visible: running
    }

    Connections {
        target: Stories

        function onCategoriesChanged() {
            // 分类到货后自动选中第一个，孩子不用先做一次选择
            if (page.currentTypeId < 0 && Stories.categories.length > 0) {
                page.pickCategory(Stories.categories[0].id);
            }
        }
    }

    Component.onCompleted: {
        Stories.refreshOnlineState();
        if (page.currentTypeId < 0) {
            Stories.loadCategories();
        }
    }
}
