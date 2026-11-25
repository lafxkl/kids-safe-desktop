import { common, Want } from '@kit.AbilityKit';
import { BusinessError } from '@kit.BasicServicesKit';
import { hilog } from '@kit.PerformanceAnalysisKit';
import { AppItem } from '../model/Models';
import { screenTime } from './ScreenTime';

const TAG: string = 'Launcher';
const DOMAIN: number = 0x0103;

/**
 * 对应 C# 的 IAppLauncherService。
 *
 * 鸿蒙拉起别的应用有两条路：
 *   1. 显式 Want：bundleName + abilityName，精确但要事先知道入口类名；
 *   2. 隐式 Want：bundleName + action.system.home + entity.system.home，
 *      由系统在该包内匹配「声明了桌面入口」的 ability。
 *
 * 第 2 条更重要。鸿蒙系统应用的入口类名毫无规律：图库是
 * `com.huawei.hmos.photos.MainAbility`，日历/浏览器/文件管理只叫 `MainAbility`，
 * contacts 叫 `com.ohos.contacts.MainAbility`，还有一堆叫 `EntryAbility` 的。
 * 靠 preset 里写死 abilityName 基本必错，所以这里以隐式 Want 为主，
 * 显式 Want 只作为「家长确实手填了入口类名」或兜底猜测时的补充。
 */
@ObservedV2
export class AppLauncher {
  @Trace launching: boolean = false;
  @Trace runningName: string = '';
  @Trace errorText: string = '';

  async launch(context: common.UIAbilityContext, app: AppItem): Promise<boolean> {
    this.errorText = '';
    this.runningName = app.name;
    this.launching = true;
    try {
      let lastCode = 0;
      for (const want of this.buildWants(app)) {
        try {
          await context.startAbility(want);
          // 记账：从这一刻起孩子不在我们这边，回来时补算时长
          screenTime.markExternalStart();
          return true;
        } catch (err) {
          const e = err as BusinessError;
          lastCode = e.code;
          hilog.warn(DOMAIN, TAG, '尝试失败 %{public}s code=%{public}d want=%{public}s',
            app.name, e.code, JSON.stringify(want));
        }
      }
      this.errorText = this.explain(lastCode, app.name);
      return false;
    } finally {
      this.launching = false;
    }
  }

  /**
   * 按「把握从大到小」排出的尝试序列，前一个失败就试下一个。
   * 全部失败才向孩子报错，错误码取最后一次的结果。
   *
   * 为什么不用隐式 Want（bundleName + action.system.home）做主路径：
   * 实测在 HarmonyOS 模拟器上 `aa start -b com.huawei.hmos.photos -A action.system.home
   * -e entity.system.home` 直接返回 10103101「找不到匹配的应用」，
   * 而 `-a com.huawei.hmos.photos.MainAbility` 立即成功。
   * 也就是说系统不允许普通应用靠 home 语义去「猜」入口，只能给显式 abilityName。
   * 隐式那一条仍留在末尾，成本只是一次快速失败，在别的 OEM 上也许可用。
   */
  private buildWants(app: AppItem): Want[] {
    const list: Want[] = [];

    if (app.launchUrl.length > 0) {
      list.push({
        action: 'ohos.want.action.viewData',
        entities: ['entity.system.browsable'],
        uri: app.launchUrl
      });
      return list;
    }

    if (app.bundleName.length === 0) {
      return list;
    }

    const tried: string[] = [];

    // 1. 已知入口类名（预设里核对过的，或家长手填的）
    if (app.abilityName.length > 0) {
      tried.push(app.abilityName);
      list.push({ bundleName: app.bundleName, abilityName: app.abilityName });
    }

    // 2. 候选入口名。系统应用基本落在这四种写法里：
    //    日历/浏览器/文件管理是 `MainAbility`；图库/设置是 `<包名>.MainAbility`；
    //    自家应用和不少三方应用是 `EntryAbility`。重复的不再试。
    const candidates: string[] = [
      'MainAbility',
      `${app.bundleName}.MainAbility`,
      'EntryAbility',
      `${app.bundleName}.EntryAbility`
    ];
    for (const name of candidates) {
      if (tried.indexOf(name) >= 0) {
        continue;
      }
      tried.push(name);
      list.push({ bundleName: app.bundleName, abilityName: name });
    }

    // 3. 最后才试隐式入口（见上面的实测结论，成功率低但没副作用）
    list.push({
      bundleName: app.bundleName,
      action: 'action.system.home',
      entities: ['entity.system.home']
    });

    return list;
  }

  private explain(code: number, name: string): string {
    if (code === 16000001 || code === 16000019) {
      // 两种含义合并了：包不存在，或包里没有这个入口类名。
      // 鸿蒙不开放跨包查询，运行时区分不出来，所以提示里把两种可能都写清楚。
      return `打不开「${name}」：设备上可能没装，或包名/入口类名不对`;
    }
    if (code === 201) {
      return `没有权限打开「${name}」`;
    }
    if (code === 0) {
      return `「${name}」没有填包名，先移除再重新添加`;
    }
    return `打开「${name}」失败（${code}）`;
  }
}

export const appLauncher: AppLauncher = new AppLauncher();
