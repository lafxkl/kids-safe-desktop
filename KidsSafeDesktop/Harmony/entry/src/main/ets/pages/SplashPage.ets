import { Backdrop } from '../components/Backdrop';
import { PageId, router } from '../core/Router';
import { settings } from '../core/Settings';

@ComponentV2
export struct SplashPage {
  /** 不能叫 scale：scale 是 ArkUI 通用属性（scale(value: ScaleOptions)），同名字段会编译报错。 */
  @Local pulse: number = 1.0;

  aboutToAppear(): void {
    this.getUIContext().animateTo({
      duration: 900,
      iterations: -1,
      playMode: PlayMode.Alternate,
      curve: Curve.EaseInOut
    }, () => {
      this.pulse = 1.06;
    });

    setTimeout(() => {
      router.reset(settings.onboardingDone ? PageId.Home : PageId.Onboarding);
    }, 1400);
  }

  build() {
    Stack() {
      Backdrop()

      Column({ space: 16 }) {
        Stack() {
          Column()
            .width(132)
            .height(132)
            .borderRadius(66)
            .backgroundColor('#B388FF')
            .opacity(0.16)
            .blur(18)
            .scale({ x: this.pulse * 1.25, y: this.pulse * 1.25 })

          Column()
            .width(132)
            .height(132)
            .borderRadius(66)
            .linearGradient({ angle: 160, colors: [['#B388FF', 0.0], ['#7C5CE0', 1.0]] })

          Text('🚀')
            .fontSize(62)
        }
        .width(190)
        .height(190)

        Text('儿童安全桌面')
          .fontSize(40)
          .fontWeight(FontWeight.Bold)
          .fontColor('#4A3B32')

        Text('给小朋友的专属空间')
          .fontSize(18)
          .fontColor('#9B8574')
      }
      .width('100%')
      .height('100%')
      .justifyContent(FlexAlign.Center)
      .alignItems(HorizontalAlign.Center)
    }
    .width('100%')
    .height('100%')
  }
}
