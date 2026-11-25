import { store } from './Store';
import { settings } from './Settings';

function todayKey(): string {
  const d = new Date();
  const m = (d.getMonth() + 1).toString().padStart(2, '0');
  const day = d.getDate().toString().padStart(2, '0');
  return `${d.getFullYear()}-${m}-${day}`;
}

/**
 * 对应 C# 的 IScreenTimeService。
 *
 * 与 Windows 端最大的不同：鸿蒙应用切到后台会被系统挂起，拿不到「别的应用在前台多久」。
 * 所以这里的计时规则是：
 *   1. 本应用在前台时，按秒累计；
 *   2. 从本应用拉起外部应用时记一个时间戳，回到本应用时把这段时长补记进去
 *      （孩子拿着设备去玩了，这段时间理应算进使用时长）；
 *   3. 切到后台（按 Home 键）不计时——挂起后计时器本来就停了。
 *
 * 跨零点自动清零，对应 C# 端按日期切分的逻辑。
 */
@ObservedV2
export class ScreenTime {
  @Trace usedSeconds: number = 0;
  @Trace running: boolean = false;

  private timerId: number = -1;
  private lastFlushAt: number = 0;
  private dayKey: string = '';
  private externalStartAt: number = 0;
  private lockNotified: boolean = false;
  private lockListeners: Array<() => void> = [];

  load(): void {
    this.dayKey = todayKey();
    const savedDay = store.getString('stDay', '');
    if (savedDay === this.dayKey) {
      this.usedSeconds = store.getNumber('stUsed', 0);
    } else {
      this.usedSeconds = 0;
      store.setString('stDay', this.dayKey);
      store.setNumber('stUsed', 0);
    }
    this.lockNotified = this.remainingSeconds() <= 0;
  }

  start(): void {
    if (this.timerId >= 0) {
      return;
    }
    this.timerId = setInterval(() => {
      this.tick();
    }, 1000);
    this.running = true;
  }

  stop(): void {
    if (this.timerId >= 0) {
      clearInterval(this.timerId);
      this.timerId = -1;
    }
    this.running = false;
    this.flush();
  }

  /** 切到后台时调用。 */
  pause(): void {
    this.stop();
  }

  /** 回到前台时调用，顺便把「在外面待的时间」补记进来。 */
  resume(): void {
    if (this.externalStartAt > 0) {
      const elapsed = Math.floor((Date.now() - this.externalStartAt) / 1000);
      this.externalStartAt = 0;
      // 上限 4 小时：防止设备被关机/休眠后一次性扣掉一整天
      this.usedSeconds += Math.min(Math.max(elapsed, 0), 4 * 3600);
    }
    this.rolloverIfNeeded();
    this.start();
    this.flush();
  }

  /** 拉起外部应用时调用。 */
  markExternalStart(): void {
    this.externalStartAt = Date.now();
    this.stop();
  }

  remainingSeconds(): number {
    return Math.max(0, settings.dailyLimitMinutes * 60 - this.usedSeconds);
  }

  usedMinutes(): number {
    return Math.floor(this.usedSeconds / 60);
  }

  remainingMinutes(): number {
    return Math.ceil(this.remainingSeconds() / 60);
  }

  isLocked(): boolean {
    return this.remainingSeconds() <= 0;
  }

  /** 家长临时加时（分钟）。 */
  grantMinutes(minutes: number): void {
    this.usedSeconds = Math.max(0, this.usedSeconds - minutes * 60);
    this.lockNotified = false;
    this.flush();
  }

  /** 家长改每日时长后重置锁定标记。 */
  reevaluate(): void {
    this.lockNotified = this.remainingSeconds() <= 0;
    this.flush();
  }

  onLocked(listener: () => void): void {
    this.lockListeners.push(listener);
  }

  private tick(): void {
    this.rolloverIfNeeded();
    if (this.isLocked()) {
      if (!this.lockNotified) {
        this.lockNotified = true;
        for (let i = 0; i < this.lockListeners.length; i++) {
          this.lockListeners[i]();
        }
      }
      this.stop();
      return;
    }
    this.usedSeconds += 1;
    if (Date.now() - this.lastFlushAt > 15000) {
      this.flush();
    }
  }

  private rolloverIfNeeded(): void {
    const key = todayKey();
    if (key === this.dayKey) {
      return;
    }
    this.dayKey = key;
    this.usedSeconds = 0;
    this.lockNotified = false;
    this.flush();
  }

  private flush(): void {
    this.lastFlushAt = Date.now();
    store.setString('stDay', this.dayKey);
    store.setNumber('stUsed', this.usedSeconds);
  }
}

export const screenTime: ScreenTime = new ScreenTime();
