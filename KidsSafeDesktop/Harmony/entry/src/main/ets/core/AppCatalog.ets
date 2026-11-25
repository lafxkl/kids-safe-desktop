import { AppItem, makeApp, makeLinkApp } from '../model/Models';

export interface AppPreset {
  bundleName: string;
  /** 入口类名。系统应用各叫各的（有的 `MainAbility`，有的 `<包名>.MainAbility`），
   *  能确认的写死，确认不了的留空交给 AppLauncher 逐个试。 */
  abilityName: string;
  label: string;
  emoji: string;
  accent: string;
}

export interface LinkPreset {
  url: string;
  label: string;
  emoji: string;
  accent: string;
}

/**
 * 对应 C# 的 IAppCatalogService —— 但能力被系统砍掉了，这点必须讲明白。
 *
 * Windows 端能扫开始菜单、Linux 端能扫 /usr/share/applications，
 * 鸿蒙**不行**：枚举全部已安装应用需要 ohos.permission.GET_BUNDLE_INFO_PRIVILEGED，
 * 这是 system_basic 级权限，普通应用申请不到，运行时直接 201。
 * launcherBundleManager.getAllLauncherAbilityInfo 同样是系统接口。
 *
 * 所以这里的策略换成「预设 + 手填」：
 *   1. 家长后台给一份常见应用预设（包名可能随系统版本变化，以设备实测为准）；
 *   2. 家长也可以自己填 bundleName / abilityName —— 包名可以在
 *      「设置 → 应用和元服务 → 应用 → 应用信息」里看到。
 * 这样虽然多一步手填，但不依赖任何特权接口，上架审核也不会因为权限被拒。
 */
export class AppCatalog {
  /** 鸿蒙端恒为 false，UI 据此隐藏「扫描已安装应用」入口。 */
  readonly canEnumerate: boolean = false;

  /**
   * 预设清单：只放包名，不写入口类名。
   *
   * 1. 前四个在 HarmonyOS 模拟器上实测存在，包名无误；
   * 2. 相机 / 备忘录 / 计算器 属于「华为手机有、模拟器没有」的那一类，
   *    填了也能在真机用，点不开时会明确提示「这台设备上没有」；
   * 3. 刻意不收「华为视频 / 华为音乐」——这两个包名随机型/版本变化最大，
   *    而且它们本身就是家长想限制的对象，需要时手填包名更靠谱。
   */
  presets(): AppPreset[] {
    return [
      // 下面四个的包名 + 入口类名都在 HarmonyOS 模拟器上 bm dump 核对过
      { bundleName: 'com.huawei.hmos.photos', abilityName: 'com.huawei.hmos.photos.MainAbility',
        label: '图库', emoji: '🖼️', accent: '#4C8BF5' },
      { bundleName: 'com.huawei.hmos.calendar', abilityName: 'MainAbility',
        label: '日历', emoji: '📅', accent: '#FF7043' },
      { bundleName: 'com.huawei.hmos.browser', abilityName: 'MainAbility',
        label: '浏览器', emoji: '🌐', accent: '#2AA7A0' },
      { bundleName: 'com.huawei.hmos.filemanager', abilityName: 'MainAbility',
        label: '文件管理', emoji: '📁', accent: '#8E9AAF' },
      { bundleName: 'com.huawei.hmos.settings', abilityName: 'com.huawei.hmos.settings.MainAbility',
        label: '设置', emoji: '⚙️', accent: '#8E9AAF' },
      // 华为手机上有、模拟器没有。入口类名按 `<包名>.MainAbility` 惯例填，猜错也有候选表兜底
      { bundleName: 'com.huawei.hmos.camera', abilityName: '',
        label: '相机', emoji: '📷', accent: '#5AC8FA' },
      { bundleName: 'com.huawei.hmos.calculator', abilityName: '',
        label: '计算器', emoji: '🧮', accent: '#34C759' },
      { bundleName: 'com.huawei.hmos.notepad', abilityName: '',
        label: '备忘录', emoji: '📝', accent: '#FFB020' }
    ];
  }

  /** 网页白名单：没有对应鸿蒙应用、但内容适合孩子的站点。 */
  links(): LinkPreset[] {
    return [
      { url: 'https://basic.smartedu.cn', label: '智慧教育平台', emoji: '📚', accent: '#7C5CE0' },
      { url: 'https://www.kepu.net.cn', label: '中国科普博览', emoji: '🔬', accent: '#2AA7A0' }
    ];
  }

  toApp(preset: AppPreset): AppItem {
    return makeApp(preset.label, preset.bundleName, preset.emoji, preset.accent, preset.abilityName);
  }

  toLink(preset: LinkPreset): AppItem {
    return makeLinkApp(preset.label, preset.url, preset.emoji, preset.accent);
  }

}

export const appCatalog: AppCatalog = new AppCatalog();
