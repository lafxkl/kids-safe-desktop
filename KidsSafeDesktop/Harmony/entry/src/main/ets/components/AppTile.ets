import { AppItem } from '../model/Models';

/** 应用磁贴，对应 QML 的 AppTile.qml。鸿蒙拿不到别的应用的图标，统一用色块 + emoji。 */
@ComponentV2
export struct AppTile {
  @Param app: AppItem = new AppItem();
  @Event onTap: () => void = () => {
  };

  build() {
    Column({ space: 8 }) {
      // 尺寸全部相对格子（width 100% + aspectRatio 1），不再写死 96vp。
      // 写死会导致 4 列时每格只有约 62vp，磁贴溢出、互相重叠、最右一列被裁掉。
      Stack() {
        // 用圆角 Column 代替 Circle+fill：本 SDK 里 shape 的 fill 标了 @since 26，
        // 而工程最低兼容 API 12，换成 backgroundColor 才在各版本都稳。
        Column()
          .width('100%')
          .height('100%')
          .borderRadius('50%')
          .backgroundColor(this.app.accent)
          .opacity(0.92)

        Text(this.app.emoji)
          .fontSize(34)
      }
      .width('100%')
      .aspectRatio(1)

      Text(this.app.name)
        .fontSize(14)
        .fontColor('#4A3B32')
        .maxLines(1)
        .textOverflow({ overflow: TextOverflow.Ellipsis })
        .width('100%')
        .textAlign(TextAlign.Center)
    }
    .width('100%')
    .onClick(() => {
      this.onTap();
    })
  }
}
