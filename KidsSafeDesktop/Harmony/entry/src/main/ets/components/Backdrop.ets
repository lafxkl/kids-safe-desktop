/**
 * 暖色渐变背景，四个主页面共用。
 * 对应 Linux 端 QML 的 Backdrop.qml。
 */
@ComponentV2
export struct Backdrop {
  build() {
    Stack() {
      Column()
        .width('100%')
        .height('100%')
        .linearGradient({
          angle: 135,
          colors: [['#FFF7EF', 0.0], ['#FFE9D6', 0.5], ['#F1E6FF', 1.0]]
        })

      // 装饰光斑：圆角 Column + backgroundColor（shape 的 fill 在本 SDK 标了 @since 26）
      Column()
        .width(340)
        .height(340)
        .borderRadius(170)
        .backgroundColor('#FFD3B0')
        .opacity(0.45)
        .blur(70)
        .position({ x: '-8%', y: '-14%' })

      Column()
        .width(280)
        .height(280)
        .borderRadius(140)
        .backgroundColor('#D6C4FF')
        .opacity(0.38)
        .blur(70)
        .position({ x: '70%', y: '64%' })
    }
    .width('100%')
    .height('100%')
  }
}
