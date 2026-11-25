import QtQuick
import QtQuick.Layouts
import KidsSafe 1.0

/*
 * 首页天气条。对应 Windows 首页「时钟下面那张淡蓝色卡片」：
 *
 *   ☀️ 19°  达州 · 阴        [现在 19°] [21:00 19°] [22:00 19°] [23:00 19°]   更新于 20:10
 *
 * 三点说明：
 *   · 图标不用 emoji。deepin 上没有彩色 emoji 字体，🌤 会变成黑白线稿，
 *     这里用 GlyphIcon 的 sun / partly / cloud / rain / snow / thunder / fog，
 *     颜色按天气类型给（太阳橙、雨蓝、雪浅蓝…），孩子一眼能认出。
 *   · 数据全由 C++ 的 Weather 单例负责（联网、缓存、超时都在那边），
 *     QML 只负责画。没有数据时整条隐藏，不留空白。
 *   · 家长在后台关掉天气时 Settings.weatherEnabled 为 false，同样整条隐藏。
 */
Rectangle {
    id: card

    // 首次取数时露一下「正在获取天气…」；一旦失败过就整条收起。
    // 不能只看 Weather.loading —— 失败会按 30s/60s/2min… 退避重试，
    // 每次重试都让卡片弹出来再收回去，孩子会盯着那块地方一直闪。
    visible: Settings.weatherEnabled
             && (Weather.available
                 || (Weather.loading && Weather.problemText.length === 0))
    implicitHeight: 86
    radius: 20
    color: "#E3F2FD"

    // 按天气类型给图标上色，比统一一种灰色更容易分辨
    function tintOf(name) {
        switch (name) {
        case "sun":     return "#FFB300";
        case "partly":  return "#FFA726";
        case "rain":    return "#29B6F6";
        case "snow":    return "#4FC3F7";
        case "thunder": return "#FFB300";
        case "fog":     return "#90A4AE";
        default:        return "#78909C";
        }
    }

    RowLayout {
        anchors.fill: parent
        anchors.leftMargin: 18
        anchors.rightMargin: 16
        spacing: 12

        GlyphIcon {
            Layout.preferredWidth: 40
            Layout.preferredHeight: 40
            name: Weather.glyph
            color: card.tintOf(Weather.glyph)
        }

        // 实况：温度大、城市和天气小，一眼先看到几度
        RowLayout {
            spacing: 10

            Text {
                text: Weather.available ? Weather.tempText : "--"
                font.pixelSize: 30
                font.weight: Font.Bold
                color: "#01579B"
            }

            ColumnLayout {
                spacing: 0
                // 给个上限，城市名特别长时才会触发省略号，而不是把逐小时挤出卡片
                Layout.maximumWidth: 300
                Layout.fillWidth: true

                Text {
                    text: Weather.available
                          ? (Weather.city + (Weather.descText.length > 0 ? " · " + Weather.descText : ""))
                          : "正在获取天气…"
                    font.pixelSize: 15
                    font.weight: Font.DemiBold
                    color: "#0277BD"
                    elide: Text.ElideRight
                }

                Text {
                    text: Weather.statusText
                    visible: text.length > 0
                    font.pixelSize: 11
                    color: "#64B5F6"
                }
            }
        }

        Item {
            Layout.fillWidth: true
        }

        // 逐小时：白底小卡片，与 Windows 端 48×66 / 圆角 14 的比例一致
        RowLayout {
            spacing: 7
            visible: Weather.hours.length > 0

            Repeater {
                model: Weather.hours

                Rectangle {
                    Layout.preferredWidth: 52
                    Layout.preferredHeight: 62
                    radius: 14
                    color: "#FFFFFF"

                    Column {
                        anchors.centerIn: parent
                        spacing: 1

                        Text {
                            anchors.horizontalCenter: parent.horizontalCenter
                            text: modelData.label
                            font.pixelSize: 10
                            color: "#0277BD"
                        }

                        GlyphIcon {
                            anchors.horizontalCenter: parent.horizontalCenter
                            width: 18
                            height: 18
                            name: modelData.glyph
                            color: card.tintOf(modelData.glyph)
                        }

                        Text {
                            anchors.horizontalCenter: parent.horizontalCenter
                            text: modelData.temp
                            font.pixelSize: 12
                            font.weight: Font.Bold
                            color: "#01579B"
                        }
                    }
                }
            }
        }
    }
}
