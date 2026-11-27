import QtQuick

/*
 * 轻量文本输入框（单行 / 多行），只给意见反馈页用。
 *
 * 为什么不用 QtQuick.Controls 的 TextField / TextArea：
 * 1) Breeze 样式的 TextArea 在 Qt 6 上一实例化就报
 *        Unable to assign TextArea_QMLTYPE_xxx to QQuickTextInput
 *    输入区拿不到文本（参见 CodeView.qml 里的同一段说明）；
 * 2) 控件自带一套跟随系统调色板的配色，放在本应用的暖米色背景上会变成
 *    深底白字或者淡到看不见——反馈页之前就是白字压在米色上。
 *
 * TextEdit 属于 QtQuick 基础类型，不经过控件样式，配色由本组件显式给，
 * 视觉上和 HomePage / ParentDashboardPage 的白卡 + 米色描边保持一致。
 *
 * 数据流：外部把值绑到 text 上，用户敲字通过 edited(content) 通知外面写回。
 * 这样「提交后清空」这类外部改动也能进到输入区，同时不会每敲一个字就把
 * 光标弹到末尾（值没变时 TextEdit 不会重设文本）。
 *
 * 占位提示单独用一个 Text 盖在输入区上方（TextEdit 自己没有 placeholder），
 * 并显式 enabled: false，避免它把点击吞掉。
 */
Item {
    id: box

    // 外部绑定进来的值
    property string text: ""
    // 用户改字时发出去，外面接着写回自己的属性
    signal edited(string content)

    property alias placeholder: hint.text
    // true = 多行，可上下滚动；false = 单行，文字垂直居中
    property bool multiline: false
    property int fontSize: 14

    property color textColor: "#4A3B32"
    property color placeholderColor: "#A9947F"
    property color fillColor: "#FFFFFF"
    property color borderColor: "#E8D8C6"
    property color focusBorderColor: "#E2603D"

    implicitHeight: multiline ? 110 : 48

    /*
     * 外部改值（例如提交成功后清空）要能落进编辑区。TextEdit 上那条
     * text: box.text 的绑定在用户敲字后不一定还活着，所以这里再补一次
     * 显式同步——值相同时不动，不会把光标弹回开头。
     */
    onTextChanged: if (edit.text !== text) edit.text = text
    Component.onCompleted: if (edit.text !== text) edit.text = text

    Rectangle {
        anchors.fill: parent
        radius: box.multiline ? 16 : 14
        color: box.fillColor
        border.width: 1
        border.color: edit.activeFocus ? box.focusBorderColor : box.borderColor

        Flickable {
            id: flick

            anchors.fill: parent
            anchors.margins: 14
            clip: true
            contentWidth: width
            contentHeight: Math.max(edit.height, height)
            boundsBehavior: Flickable.StopAtBounds
            flickableDirection: Flickable.VerticalFlick
            // 单行框没有可滚的内容，关掉拖动更不容易误触
            interactive: box.multiline

            TextEdit {
                id: edit

                width: flick.width
                height: box.multiline ? Math.max(implicitHeight, flick.height)
                                      : flick.height

                text: box.text
                color: box.textColor
                font.pixelSize: box.fontSize
                selectionColor: "#55E2603D"
                selectedTextColor: box.textColor
                wrapMode: box.multiline ? TextEdit.Wrap : TextEdit.NoWrap
                textFormat: TextEdit.PlainText
                selectByMouse: true
                verticalAlignment: box.multiline ? TextEdit.AlignTop
                                                 : TextEdit.AlignVCenter

                // 只上报「用户自己敲出来的差异」，外部赋值回填时不回声
                onTextChanged: if (text !== box.text) box.edited(text)
            }
        }

        Text {
            id: hint

            anchors.fill: flick
            enabled: false
            visible: edit.text.length === 0
            color: box.placeholderColor
            font.pixelSize: box.fontSize
            wrapMode: Text.Wrap
            verticalAlignment: box.multiline ? Text.AlignTop : Text.AlignVCenter
        }
    }
}
