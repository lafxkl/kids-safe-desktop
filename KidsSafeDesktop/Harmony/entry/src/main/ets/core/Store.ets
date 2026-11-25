import { preferences } from '@kit.ArkData';
import { common } from '@kit.AbilityKit';
import { BusinessError } from '@kit.BasicServicesKit';
import { hilog } from '@kit.PerformanceAnalysisKit';

const TAG: string = 'Store';
const DOMAIN: number = 0x0100;
const STORE_NAME: string = 'kidssafe';

/**
 * 配置文件封装，对应 Linux 端的 KConfig、Windows 端的 Preferences。
 *
 * 用 harmony 的 preferences（键值型，落盘到应用沙箱）而不是关系库：
 * 配置项就这么十几个，用 RDB 属于杀鸡用牛刀，而且 preferences 支持同步读，
 * 启动时不用等异步回调就能拿到昵称和时长。
 */
class Store {
  private pref: preferences.Preferences | null = null;

  init(context: common.Context): void {
    if (this.pref !== null) {
      return;
    }
    try {
      this.pref = preferences.getPreferencesSync(context, { name: STORE_NAME });
    } catch (err) {
      const e = err as BusinessError;
      hilog.error(DOMAIN, TAG, '打开配置失败 code=%{public}d msg=%{public}s', e.code, e.message);
    }
  }

  getString(key: string, def: string): string {
    if (this.pref === null) {
      return def;
    }
    try {
      return this.pref.getSync(key, def) as string;
    } catch (err) {
      const e = err as BusinessError;
      hilog.error(DOMAIN, TAG, '读取 %{public}s 失败 code=%{public}d', key, e.code);
      return def;
    }
  }

  getNumber(key: string, def: number): number {
    if (this.pref === null) {
      return def;
    }
    try {
      return this.pref.getSync(key, def) as number;
    } catch (err) {
      const e = err as BusinessError;
      hilog.error(DOMAIN, TAG, '读取 %{public}s 失败 code=%{public}d', key, e.code);
      return def;
    }
  }

  getBool(key: string, def: boolean): boolean {
    if (this.pref === null) {
      return def;
    }
    try {
      return this.pref.getSync(key, def) as boolean;
    } catch (err) {
      const e = err as BusinessError;
      hilog.error(DOMAIN, TAG, '读取 %{public}s 失败 code=%{public}d', key, e.code);
      return def;
    }
  }

  setString(key: string, value: string): void {
    this.put(key, value);
  }

  setNumber(key: string, value: number): void {
    this.put(key, value);
  }

  setBool(key: string, value: boolean): void {
    this.put(key, value);
  }

  private put(key: string, value: string | number | boolean): void {
    if (this.pref === null) {
      return;
    }
    try {
      this.pref.putSync(key, value);
      this.pref.flush().catch((err: BusinessError) => {
        hilog.warn(DOMAIN, TAG, '落盘失败 key=%{public}s code=%{public}d', key, err.code);
      });
    } catch (err) {
      const e = err as BusinessError;
      hilog.error(DOMAIN, TAG, '写入失败 key=%{public}s code=%{public}d', key, e.code);
    }
  }
}

export const store: Store = new Store();
