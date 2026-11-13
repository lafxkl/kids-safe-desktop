import QtQuick

/*
 * 页面背景：暖色渐变 + 三颗半透明装饰圆。
 * 单独抽出来是因为 6 个页面都要用，改配色时只改这一处。
 */
Rectangle {
    id: root

    gradient: Gradient {
        GradientStop { position: 0.0; color: "#FFF9F0" }
        GradientStop { position: 0.55; color: "#FFF1DE" }
        GradientStop { position: 1.0; color: "#FFE3C4" }
    }

    Rectangle {
        width: 420
        height: 420
        radius: 210
        x: -140
        y: -160
        color: "#FFD5A5"
        opacity: 0.45
    }

    Rectangle {
        width: 300
        height: 300
        radius: 150
        x: root.width - 200
        y: root.height - 260
        color: "#FFC7C0"
        opacity: 0.35
    }

    Rectangle {
        width: 180
        height: 180
        radius: 90
        x: root.width * 0.72
        y: -70
        color: "#D6C4FF"
        opacity: 0.30
    }
}
