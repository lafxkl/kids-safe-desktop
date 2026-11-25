/**
 * 内置内容页统一顶栏：返回箭头 + 标题 + 副标题 + 右侧小字。
 *
 * 抽出来的原因：故事屋、古诗、口诀、安全知识这几个页面顶栏一模一样，
 * 各写一遍的结果是字号和左边距慢慢跑偏（之前首页就是这样走形的）。
 */
@ComponentV2
export struct PageHeader {
  @Param title: string = '';
  @Param subtitle: string = '';
  /** 右侧小字，例如「第 3 / 26 首」。空串则不显示。 */
  @Param trailing: string = '';
  @Event onBack: () => void = () => {
  };

  build() {
    Row({ space: 12 }) {
      // 返回箭头做成 36vp 的点击热区，孩子手指按得中
      Text('‹')
        .fontSize(30)
        .fontColor('#7C5CE0')
        .width(36)
        .textAlign(TextAlign.Center)
        .onClick(() => {
          this.onBack();
        })

      Column({ space: 2 }) {
        Text(this.title)
          .fontSize(24)
          .fontWeight(FontWeight.Bold)
          .fontColor('#4A3B32')
          .maxLines(1)
          .textOverflow({ overflow: TextOverflow.Ellipsis })

        if (this.subtitle.length > 0) {
          Text(this.subtitle)
            .fontSize(12)
            .fontColor('#9B8574')
            .maxLines(1)
            .textOverflow({ overflow: TextOverflow.Ellipsis })
        }
      }
      .alignItems(HorizontalAlign.Start)
      .layoutWeight(1)

      if (this.trailing.length > 0) {
        Text(this.trailing)
          .fontSize(14)
          .fontColor('#9B8574')
      }
    }
    .width('100%')
    .alignItems(VerticalAlign.Center)
  }
}
