/**
 * 数字键盘，对应 QML 的 PinPad.qml。
 *
 * 输满位数自动提交，不需要「确认」键——4 位 PIN 的输入成本已经够低，
 * 多一步确认只会让孩子在旁边看着家长多点一次。
 * 校验失败由父组件把 errorText 置为非空，这里监听后清空并抖一下。
 */
@ComponentV2
export struct PinPad {
  @Param title: string = '请输入家长密码';
  @Param digitCount: number = 4;
  @Param errorText: string = '';
  @Event onComplete: (pin: string) => void = (pin: string) => {
  };

  @Local value: string = '';
  @Local shakeOffset: number = 0;

  private keys: string[] = ['1', '2', '3', '4', '5', '6', '7', '8', '9', '⌫', '0', ''];

  @Monitor('errorText')
  onErrorChanged(): void {
    if (this.errorText.length > 0) {
      this.value = '';
      this.shake();
    }
  }

  build() {
    Column({ space: 22 }) {
      Text(this.title)
        .fontSize(22)
        .fontWeight(FontWeight.Medium)
        .fontColor('#4A3B32')

      Row({ space: 18 }) {
        ForEach(this.dots(), (item: number, index: number) => {
          Column()
            .width(18)
            .height(18)
            .borderRadius(9)
            .backgroundColor(index < this.value.length ? '#FF8A5C' : '#E7D3BF')
        }, (item: number, index: number) => `dot-${index}-${this.value.length > index}`)
      }
      .offset({ x: this.shakeOffset })

      if (this.errorText.length > 0) {
        Text(this.errorText)
          .fontSize(15)
          .fontColor('#E5484D')
      }

      Grid() {
        ForEach(this.keys, (key: string) => {
          GridItem() {
            if (key.length > 0) {
              Text(key)
                .fontSize(key === '⌫' ? 24 : 28)
                .fontWeight(FontWeight.Medium)
                .fontColor('#4A3B32')
                .width('100%')
                .height('100%')
                .textAlign(TextAlign.Center)
                .align(Alignment.Center)
                .borderRadius(18)
                .backgroundColor(key === '⌫' ? '#F3E7DA' : '#FFFFFF')
                .onClick(() => {
                  this.press(key);
                })
            }
          }
          .height(70)
        }, (key: string, index: number) => `key-${index}`)
      }
      .columnsTemplate('1fr 1fr 1fr')
      .columnsGap(14)
      .rowsGap(14)
      .width(300)
      .height(70 * 4 + 14 * 3)
      .offset({ x: this.shakeOffset })
    }
    .alignItems(HorizontalAlign.Center)
  }

  private dots(): number[] {
    const out: number[] = [];
    for (let i = 0; i < this.digitCount; i++) {
      out.push(i);
    }
    return out;
  }

  private press(key: string): void {
    if (key === '⌫') {
      this.value = this.value.substring(0, Math.max(0, this.value.length - 1));
      return;
    }
    if (this.value.length >= this.digitCount) {
      return;
    }
    this.value += key;
    if (this.value.length === this.digitCount) {
      const pin = this.value;
      // 交给父组件判断对错；错了会把 errorText 顶回来触发清空
      setTimeout(() => {
        this.onComplete(pin);
      }, 60);
      // 无论对错都自动清空，家长重试时不用手动退格
      setTimeout(() => {
        if (this.value.length === this.digitCount) {
          this.value = '';
        }
      }, 420);
    }
  }

  private shake(): void {
    this.getUIContext().animateTo({
      duration: 70,
      iterations: 6,
      playMode: PlayMode.Alternate,
      onFinish: () => {
        this.shakeOffset = 0;
      }
    }, () => {
      this.shakeOffset = 12;
    });
  }
}
