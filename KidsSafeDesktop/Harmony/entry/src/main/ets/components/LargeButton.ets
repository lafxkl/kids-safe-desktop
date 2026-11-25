/** 大按钮，对应 QML 的 LargeButton.qml。儿童界面的点击目标一律不小于 64vp。 */
@ComponentV2
export struct LargeButton {
  @Param text: string = '';
  @Param emoji: string = '';
  @Param bg: string = '#FF8A5C';
  @Param fg: string = '#FFFFFF';
  /**
   * 注意：不能叫 enabled。enabled 是 ArkUI 通用属性（CustomComponent 上的
   * `enabled(value: boolean): CommonAttribute`），同名字段会报
   * 「Property 'enabled' is not assignable to the same property in base type」。
   * 同理 scale / opacity / visibility / width 这些也都不能用。
   */
  @Param isEnabled: boolean = true;
  @Param outlined: boolean = false;
  @Event onTap: () => void = () => {
  };

  build() {
    Row({ space: 8 }) {
      if (this.emoji.length > 0) {
        Text(this.emoji).fontSize(22)
      }
      Text(this.text)
        .fontSize(19)
        .fontWeight(FontWeight.Medium)
        .fontColor(this.outlined ? '#7C5CE0' : this.fg)
    }
    .justifyContent(FlexAlign.Center)
    .width('100%')
    .height(62)
    .borderRadius(22)
    .backgroundColor(this.outlined ? '#FFFFFF' : (this.isEnabled ? this.bg : '#D9CFC7'))
    .border({ width: this.outlined ? 1.5 : 0, color: '#E3D6F7' })
    .shadow({ radius: this.outlined ? 0 : 12, color: '#1A000000', offsetY: 4 })
    .onClick(() => {
      if (this.isEnabled) {
        this.onTap();
      }
    })
  }
}
