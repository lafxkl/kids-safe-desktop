import { display, window } from '@kit.ArkUI';
import { common } from '@kit.AbilityKit';
import { BusinessError } from '@kit.BasicServicesKit';
import { hilog } from '@kit.PerformanceAnalysisKit';

const TAG: string = 'Kiosk';
const DOMAIN: number = 0x0102;

/**
 * 儿童模式锁定。
 *
 * 这是鸿蒙端和 Windows / Linux 端差距最大的一块，必须说清楚：
 *
 * ── 消费级设备（普通手机 / 平板 / 2in1）──
 * 任何第三方应用都无法阻止用户按 Home 键、上滑回桌面、下拉控制中心。
 * 系统没有给普通应用这种权力，这不是 API 没调对。
 * 本类能做的只有「沉浸式」：全屏、隐藏状态栏与导航栏、常亮、拦截返回键。
 * 孩子仍然可以退出，只是退得不那么顺手；退出后再进来会立刻回到锁屏状态。
 *
 * ── 企业设备（鸿蒙商用终端 + 企业开发者）──
 * API 26 起提供 kioskManager（@kit.AbilityKit），可真正单应用锁定，
 * 但要求：企业实名认证 + AGC 申请受控权限 ohos.permission.ENTERPRISE_SET_KIOSK
 * + 企业正式签名 Profile + 商用固件。缺少任何一项，调用直接报权限拒绝。
 * 因为该 API 在 API 20 的 SDK 里不存在，直接 import 会导致整个工程编译不过，
 * 所以这里不引入，而是把接入代码放在 KioskEnterprise.ets.sample 里，
 * 企业版打包时按说明粘贴即可。
 */
@ObservedV2
export class Kiosk {
  @Trace immersiveApplied: boolean = false;
  /** 系统级锁定是否真的生效。消费级设备恒为 false。 */
  @Trace systemLocked: boolean = false;

  /**
   * 顶部 / 底部安全区（vp）。页面靠它让开摄像头挖孔和手势条，不能当 0 用。
   *
   * 为什么必须自己算：applyImmersive() 里开了 setWindowLayoutFullScreen(true) 又隐藏了状态栏，
   * 这两步之后 ArkUI 的默认安全区只剩 TYPE_SYSTEM，而状态栏一旦隐藏它的高度就是 0，
   * 摄像头挖孔（TYPE_CUTOUT）不参与布局——于是页面第一行内容会被挖孔直接压住。
   * 所以这里显式读一次窗口避让区，换算成 vp 暴露给页面。
   */
  @Trace safeTopVp: number = 0;
  @Trace safeBottomVp: number = 0;

  private win: window.Window | null = null;

  bindWindow(win: window.Window): void {
    this.win = win;
    // 转屏、折叠展开、分屏切换都会改避让区，跟着重新算
    try {
      win.on('avoidAreaChange', (_info: window.AvoidAreaOptions) => {
        this.refreshSafeArea();
      });
    } catch (err) {
      this.warn('避让区监听失败', err);
    }
    this.applyImmersive();
  }

  /** 读取窗口避让区，换算成 vp。挖孔与系统栏取较大者，取不到就保持原值。 */
  refreshSafeArea(): void {
    const win = this.win;
    if (win === null) {
      return;
    }
    let topPx = 0;
    let bottomPx = 0;
    try {
      const cutout = win.getWindowAvoidArea(window.AvoidAreaType.TYPE_CUTOUT);
      topPx = Math.max(topPx, cutout.topRect.height);
      bottomPx = Math.max(bottomPx, cutout.bottomRect.height);
    } catch (err) {
      this.warn('读取挖孔避让区失败', err);
    }
    try {
      const system = win.getWindowAvoidArea(window.AvoidAreaType.TYPE_SYSTEM);
      topPx = Math.max(topPx, system.topRect.height);
      bottomPx = Math.max(bottomPx, system.bottomRect.height);
    } catch (err) {
      this.warn('读取系统栏避让区失败', err);
    }
    const density = this.densityPixels();
    this.safeTopVp = Math.round(topPx / density);
    this.safeBottomVp = Math.round(bottomPx / density);
  }

  /** px -> vp 用的屏幕密度。接口异常时按 1:1 处理，宁可少让 1 像素也不要崩。 */
  private densityPixels(): number {
    try {
      const value = display.getDefaultDisplaySync().densityPixels;
      return value > 0 ? value : 1;
    } catch (err) {
      this.warn('读取屏幕密度失败', err);
      return 1;
    }
  }

  async applyImmersive(): Promise<void> {
    if (this.win === null) {
      return;
    }
    try {
      await this.win.setWindowLayoutFullScreen(true);
    } catch (err) {
      this.warn('全屏设置失败', err);
    }
    try {
      // 传空数组 = 状态栏和导航栏都隐藏
      await this.win.setWindowSystemBarEnable([]);
    } catch (err) {
      this.warn('隐藏系统栏失败', err);
    }
    try {
      await this.win.setWindowKeepScreenOn(true);
    } catch (err) {
      this.warn('常亮设置失败', err);
    }
    this.immersiveApplied = true;
    // 隐藏状态栏会改变避让区，布局生效后再量一次
    this.refreshSafeArea();
  }

  async restore(): Promise<void> {
    if (this.win === null) {
      return;
    }
    try {
      await this.win.setWindowSystemBarEnable(['status', 'navigation']);
      await this.win.setWindowLayoutFullScreen(false);
      await this.win.setWindowKeepScreenOn(false);
    } catch (err) {
      this.warn('恢复系统栏失败', err);
    }
    this.immersiveApplied = false;
  }

  /** 家长点「退出」时调用：先还原系统 UI，再结束自己。 */
  async exitApp(context: common.UIAbilityContext): Promise<void> {
    await this.restore();
    try {
      await context.terminateSelf();
    } catch (err) {
      this.warn('退出失败', err);
    }
  }

  private warn(what: string, err: Object): void {
    const e = err as BusinessError;
    hilog.warn(DOMAIN, TAG, '%{public}s code=%{public}d msg=%{public}s', what, e.code, e.message);
  }
}

export const kiosk: Kiosk = new Kiosk();
