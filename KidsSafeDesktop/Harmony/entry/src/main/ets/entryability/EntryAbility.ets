import { AbilityConstant, UIAbility, Want } from '@kit.AbilityKit';
import { window } from '@kit.ArkUI';
import { BusinessError } from '@kit.BasicServicesKit';
import { hilog } from '@kit.PerformanceAnalysisKit';
import { store } from '../core/Store';
import { settings } from '../core/Settings';
import { rewards } from '../core/Rewards';
import { screenTime } from '../core/ScreenTime';
import { kiosk } from '../core/Kiosk';

const TAG: string = 'EntryAbility';
const DOMAIN: number = 0x0107;

export default class EntryAbility extends UIAbility {
  onCreate(want: Want, launchParam: AbilityConstant.LaunchParam): void {
    // 配置必须在 UI 起来之前读完，否则首页会先闪一下默认昵称
    store.init(this.context);
    settings.load();
    // 星星账本要在首页第一次画星星余额之前读出来
    rewards.load();
    screenTime.load();
    hilog.info(DOMAIN, TAG, '启动完成，白名单 %{public}d 个应用', settings.allowedApps.length);
  }

  onWindowStageCreate(windowStage: window.WindowStage): void {
    windowStage.loadContent('pages/Index', (err: BusinessError) => {
      if (err.code) {
        hilog.error(DOMAIN, TAG, '加载首页失败 code=%{public}d', err.code);
        return;
      }
      // 拿到窗口后再上沉浸式，否则 setWindowLayoutFullScreen 会作用在错误的窗口上
      kiosk.bindWindow(windowStage.getMainWindowSync());
    });
  }

  onForeground(): void {
    // 从别的应用回来：补记外部使用时长，并继续计时
    screenTime.resume();
  }

  onBackground(): void {
    // 系统即将挂起本应用，先把秒数落盘，避免丢进度
    screenTime.pause();
  }

  onDestroy(): void {
    screenTime.stop();
  }
}
