import { Backdrop } from '../components/Backdrop';
import { LargeButton } from '../components/LargeButton';
import { PinPad } from '../components/PinPad';
import { GateIntent, PageId, router } from '../core/Router';
import { screenTime } from '../core/ScreenTime';
import { settings } from '../core/Settings';

/**
 * 家长验证，对应 QML 的 ParentGatePage.qml。
 *
 * 两个入口共用这一个页面，靠 router.gateIntent 区分：
 *   Dashboard —— 进家长后台
 *   Extend    —— 孩子时间到了，家长验证后加时 15 分钟
 */
@ComponentV2
export struct ParentGatePage {
  @Local errorText: string = '';

  aboutToAppear(): void {
    // 还没设过密码的情况理论上不会出现（引导流程强制设），兜底直接放行
    if (!settings.pinSet) {
      this.enter();
    }
  }

  build() {
    Stack() {
      Backdrop()

      Column({ space: 20 }) {
        PinPad({
          title: router.gateIntent === GateIntent.Extend ? '家长验证后加时' : '请输入家长密码',
          errorText: this.errorText,
          onComplete: (pin: string) => {
            this.check(pin);
          }
        })

        LargeButton({
          text: '返回', outlined: true, onTap: () => {
            router.back();
          }
        })
      }
      .width('100%')
      .height('100%')
      .padding(32)
      .constraintSize({ maxWidth: 520 })
      .justifyContent(FlexAlign.Center)
    }
    .width('100%')
    .height('100%')
  }

  private check(pin: string): void {
    if (settings.verifyPin(pin)) {
      this.errorText = '';
      this.enter();
      return;
    }
    this.errorText = '密码不对，再试一次';
  }

  private enter(): void {
    if (router.gateIntent === GateIntent.Extend) {
      screenTime.grantMinutes(15);
      screenTime.reevaluate();
      router.reset(PageId.Home);
      return;
    }
    router.replace(PageId.Dashboard);
  }
}
