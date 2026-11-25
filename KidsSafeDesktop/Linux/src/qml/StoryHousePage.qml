import QtQuick
import QtQuick.Controls
import QtQuick.Layouts
import QtQuick.Window
import KidsSafe 1.0

/*
 * 故事屋：分类 + 故事列表 + 搜索。对应 C# 的 StoryHousePage。
 *
 * 布局上有一条必须守住的规则：**列表和它的状态提示（空/加载中）要放在同一个
 * 普通 Item 里**，状态提示锚定这个 Item。
 *
 * 早先的写法是状态提示直接 `anchors.centerIn: list`，而 list 是 ColumnLayout
 * 的子项——坐标由布局器计算、不是按 anchors 摆的。实测（deepin / Qt 6.8）这种
 * 跨层级锚定不生效，提示会堆在左上角，正好压住「故事屋」标题，看起来像是
 * 标题和图标叠在一起。HomePage 的空状态踩过同一个坑，改法也一样。
 */
Item {
    id: page

    // TapHandler 不是 Item，`Window.window` 附加属性对它返回 null——附加属性
    // 是靠「item → 所在窗口」反查的，handler 没有窗口归属。所以统一在根 Item
    // 上取一次窗口，页面里所有跳转都用这个属性；MouseArea、TapHandler、定时器
    // 里表现一致。（deepin / Qt 6.8 实测：MouseArea 里能用、TapHandler 里必 null）
    readonly property var navWin: Window.window

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
                    onTapped: page.navWin.popPage()
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

        // ---- 故事列表（含空状态 / 加载指示）----
        Item {
            Layout.fillWidth: true
            Layout.fillHeight: true

            ListView {
                id: list

                anchors.fill: parent
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

                            GlyphIcon {
                                anchors.centerIn: parent
                                width: 28
                                height: 28
                                name: "book"
                                color: "#E08A3C"
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
                        onTapped: page.navWin.pushPage(Qt.resolvedUrl("StoryReaderPage.qml"), {
                            "storyId": modelData.id,
                            "storyTitle": modelData.title
                        })
                    }
                }
            }

            // ---- 状态提示：离线 / 加载中 / 无结果 ----
            ColumnLayout {
                anchors.centerIn: parent
                spacing: 14
                visible: Stories.items.length === 0 && !Stories.loading

                GlyphIcon {
                    Layout.alignment: Qt.AlignHCenter
                    Layout.preferredWidth: 66
                    Layout.preferredHeight: 66
                    name: !Stories.onlineEnabled
                          ? "plug"
                          : (Stories.errorText.length > 0 ? "moon" : "book")
                    color: "#C0AC98"
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
                anchors.centerIn: parent
                width: 54
                height: 54
                running: Stories.loading
                visible: running
            }
        }
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
