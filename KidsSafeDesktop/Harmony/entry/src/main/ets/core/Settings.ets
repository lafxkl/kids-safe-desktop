import { store } from './Store';
import { randomSalt, sha256Hex } from './Crypto';
import { AppItem } from '../model/Models';

/**
 * 对应 C# 的 ISettingsService / ChildSettings，以及 Linux 端的 core/settings。
 *
 * 状态用 ArkUI V2 的 @ObservedV2 + @Trace：任何页面读过的字段变化都会自动刷新，
 * 不需要像 V1 那样层层 @Link 往下传。这对「设置页改完，桌面立刻变」很关键。
 */
@ObservedV2
export class Settings {
  @Trace nickname: string = '小朋友';
  @Trace avatarEmoji: string = '🐯';
  @Trace dailyLimitMinutes: number = 60;
  @Trace gamesEnabled: boolean = true;
  @Trace onboardingDone: boolean = false;
  @Trace pinSet: boolean = false;
  @Trace allowedApps: AppItem[] = [];

  @Trace serverUrl: string = '';
  @Trace onlineContentEnabled: boolean = false;

  @Trace eyeCareEnabled: boolean = false;
  @Trace eyeCareIntensity: number = 0.22;

  private pinHash: string = '';
  private pinSalt: string = '';

  load(): void {
    this.nickname = store.getString('nickname', '小朋友');
    this.avatarEmoji = store.getString('avatarEmoji', '🐯');
    this.dailyLimitMinutes = store.getNumber('dailyLimitMinutes', 60);
    this.gamesEnabled = store.getBool('gamesEnabled', true);
    this.onboardingDone = store.getBool('onboardingDone', false);
    this.serverUrl = store.getString('serverUrl', '');
    this.onlineContentEnabled = store.getBool('onlineContentEnabled', false);
    this.eyeCareEnabled = store.getBool('eyeCareEnabled', false);
    this.eyeCareIntensity = store.getNumber('eyeCareIntensity', 0.22);

    this.pinHash = store.getString('pinHash', '');
    this.pinSalt = store.getString('pinSalt', '');
    this.pinSet = this.pinHash.length > 0;

    this.allowedApps = this.parseApps(store.getString('allowedApps', '[]'));
  }

  save(): void {
    store.setString('nickname', this.nickname);
    store.setString('avatarEmoji', this.avatarEmoji);
    store.setNumber('dailyLimitMinutes', this.dailyLimitMinutes);
    store.setBool('gamesEnabled', this.gamesEnabled);
    store.setBool('onboardingDone', this.onboardingDone);
    store.setString('serverUrl', this.serverUrl);
    store.setBool('onlineContentEnabled', this.onlineContentEnabled);
    store.setBool('eyeCareEnabled', this.eyeCareEnabled);
    store.setNumber('eyeCareIntensity', this.eyeCareIntensity);
    store.setString('allowedApps', JSON.stringify(this.allowedApps));
  }

  verifyPin(pin: string): boolean {
    if (this.pinHash.length === 0) {
      return false;
    }
    return sha256Hex(`${this.pinSalt}:${pin}`) === this.pinHash;
  }

  setPin(pin: string): void {
    this.pinSalt = randomSalt();
    this.pinHash = sha256Hex(`${this.pinSalt}:${pin}`);
    this.pinSet = this.pinHash.length > 0;
    store.setString('pinHash', this.pinHash);
    store.setString('pinSalt', this.pinSalt);
  }

  isAllowed(id: string): boolean {
    for (let i = 0; i < this.allowedApps.length; i++) {
      if (this.allowedApps[i].id === id) {
        return true;
      }
    }
    return false;
  }

  addApp(app: AppItem): void {
    if (this.isAllowed(app.id)) {
      return;
    }
    const next: AppItem[] = [];
    for (let i = 0; i < this.allowedApps.length; i++) {
      next.push(this.allowedApps[i]);
    }
    next.push(app);
    // 整体替换而不是 push：@Trace 对数组的赋值一定触发刷新，最稳。
    this.allowedApps = next;
    this.save();
  }

  removeApp(id: string): void {
    const next: AppItem[] = [];
    for (let i = 0; i < this.allowedApps.length; i++) {
      if (this.allowedApps[i].id !== id) {
        next.push(this.allowedApps[i]);
      }
    }
    this.allowedApps = next;
    this.save();
  }

  private parseApps(raw: string): AppItem[] {
    try {
      const parsed = JSON.parse(raw) as AppItem[];
      const list: AppItem[] = [];
      for (let i = 0; i < parsed.length; i++) {
        list.push(parsed[i]);
      }
      return list;
    } catch (err) {
      // 配置文件被改坏时宁可回到空列表，也不要让桌面起不来
      return [];
    }
  }
}

export const settings: Settings = new Settings();
