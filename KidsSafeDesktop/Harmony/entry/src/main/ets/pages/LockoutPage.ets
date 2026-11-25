import { LargeButton } from '../components/LargeButton';
import { GateIntent, PageId, router } from '../core/Router';
import { screenTime } from '../core/ScreenTime';
import { settings } from '../core/Settings';

/** 时间用完的锁屏页，对应 QML 的 LockoutPage.qml。 */
@ComponentV2
export struct LockoutPage {
  build() {
    Stack() {
      Column()
        .width('100%')
        .height('100%')
        .linearGradient({ angle: 160, colors: [['#3E3557', 0.0], ['#2A2440', 1.0]] })

      Column({ space: 18 }) {
        Text('🌙')
          .fontSize(68)

        Text('今天的时间用完啦')
          .fontSize(32)
          .fontWeight(FontWeight.Bold)
          .fontColor('#FFF4E8')

        Text(`已经玩了 ${screenTime.usedMinutes()} 分钟，休息一下眼睛吧`)
          .fontSize(16)
          .fontColor('#C3B8D9')

        LargeButton({
          text: '家长解锁并加 15 分钟',
          bg: '#F2C14E',
          fg: '#4A3B32',
          onTap: () => {
            router.gateIntent = GateIntent.Extend;
            router.push(PageId.Gate);
          }
        })
          .width(320)
          .margin({ top: 16 })

        Text('明天会自动重新计时')
          .fontSize(13)
          .fontColor('#8E86A3')
      }
      .width('100%')
      .height('100%')
      .padding(32)
      .justifyContent(FlexAlign.Center)
      .alignItems(HorizontalAlign.Center)
    }
    .width('100%')
    .height('100%')
  }
}
