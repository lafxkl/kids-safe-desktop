import QtQuick
import QtQuick.Dialogs

/*
 * 工坊的文件选择器。
 *
 * 单独拆一个文件，是为了让 MakerPage.qml 能用 Loader 加载它：
 * `QtQuick.Dialogs` 在 Debian/deepin 上是独立包（qml6-module-qtquick-dialogs），
 * 精简安装里可能没有。把它隔离在这里，缺包时只是「选不了文件」，
 * 编辑器本身（画、试玩、导出到剪贴板以外的功能）照常可用。
 *
 * selectedFile 是 Qt 6.4 起的写法（旧的 file 已废弃）。deepin 25 带 6.8，够用。
 */
Item {
    id: picker

    signal imagePicked(url fileUrl)
    signal projectPicked(url fileUrl)

    function requestImage() { imageDialog.open(); }
    function requestProject() { projectDialog.open(); }

    FileDialog {
        id: imageDialog

        title: "选一张图片当素材"
        fileMode: FileDialog.OpenFile
        nameFilters: ["图片 (*.png *.jpg *.jpeg *.webp *.gif *.bmp)"]

        onAccepted: picker.imagePicked(selectedFile)
    }

    FileDialog {
        id: projectDialog

        title: "选一个作品文件"
        fileMode: FileDialog.OpenFile
        nameFilters: ["作品 (*.json)", "所有文件 (*)"]

        onAccepted: picker.projectPicked(selectedFile)
    }
}
