import QtQuick

/*
 * 只读的代码 / 输出显示框，可以上下左右拖动，也可以用鼠标选字。
 *
 * 为什么不用 QtQuick.Controls 的 TextArea：Breeze 样式那份实现在 Qt 6 上
 * 一实例化就报
 *     qrc:/qt/qml/org/kde/breeze/TextArea.qml:92:9:
 *     Unable to assign TextArea_QMLTYPE_xxx to QQuickTextInput
 * 内容区拿不到文本 —— 表现就是项目工坊的「关键代码」、小小程序员的代码预览
 * 和运行结果全是空框。这不是我们的 QML 写错，是控件样式自己的问题，绕开它
 * 比等上游修现实。TextEdit 属于 QtQuick 基础类型，不经过控件样式。
 *
 * 用法与属性都刻意保持最少：文本、要不要换行、字号、颜色。
 */
Item {
    id: view

    property string text: ""
    // true = 按容器宽度折行；false = 保持长行，可以左右拖
    property bool wrap: false
    property color textColor: "#CFD8DC"
    property int fontSize: 11
    property bool selectable: true
    property string monoFamily: "monospace"

    clip: true

    Flickable {
        id: flick

        anchors.fill: parent
        clip: true
        boundsBehavior: Flickable.StopAtBounds
        flickableDirection: Flickable.AutoFlickIfNeeded
        contentWidth: Math.max(edit.width, width)
        contentHeight: Math.max(edit.height, height)

        TextEdit {
            id: edit

            /*
             * 不换行时按内容自然宽度铺开（implicitWidth 是整行最长那一行的
             * 宽度，不依赖 width，所以不会和父项的 contentWidth 互相追着算），
             * 这样 Flickable 才有可拖的横向余量；换行时锁到可视宽度。
             */
            width: view.wrap ? flick.width : Math.max(flick.width, implicitWidth)
            height: Math.max(implicitHeight, flick.height)

            text: view.text
            readOnly: true
            selectByMouse: view.selectable
            color: view.textColor
            selectionColor: "#4D80CBC4"
            selectedTextColor: view.textColor
            font.family: view.monoFamily
            font.pixelSize: view.fontSize
            wrapMode: view.wrap ? TextEdit.Wrap : TextEdit.NoWrap
            textFormat: TextEdit.PlainText
        }
    }
}
