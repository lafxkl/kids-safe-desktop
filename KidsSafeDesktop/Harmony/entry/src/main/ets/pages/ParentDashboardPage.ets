import { common } from '@kit.AbilityKit';
import { promptAction } from '@kit.ArkUI';
import { Backdrop } from '../components/Backdrop';
import { LargeButton } from '../components/LargeButton';
import { AppPreset, appCatalog } from '../core/AppCatalog';
import { care } from '../core/Care';
import { kiosk } from '../core/Kiosk';
import { OrderItem, ShopItem, rewards, timeLabel } from '../core/Rewards';
import { PageId, router } from '../core/Router';
import { screenTime } from '../core/ScreenTime';
import { settings } from '../core/Settings';
import { storyService } from '../core/StoryService';
import { AppItem, makeApp } from '../model/Models';

/** 家长后台，对应 QML 的 ParentDashboardPage.qml。 */
@ComponentV2
export struct ParentDashboardPage {
  @Local serverDraft: string = '';
  @Local manualBundle: string = '';
  @Local manualName: string = '';
  @Local manualAbility: string = '';
  @Local hint: string = '';
  /** 新商品的草稿。价格用字符串存，输入框中途可以是空的。 */
  @Local shopTitle: string = '';
  @Local shopEmoji: string = '🎁';
  @Local shopCost: string = '30';
  @Local shopNote: string = '';
  /** 最近 7 天孩子记了几天心情。只取天数，不取内容 —— 见 careCard 的说明。 */
  @Local moodDays: number = 0;

  aboutToAppear(): void {
    this.serverDraft = settings.serverUrl;
    storyService.refreshOnlineState();
    rewards.load();
    this.moodDays = care.recordedIn(7);
  }

  build() {
    Stack() {
      Backdrop()

      Column() {
        Row({ space: 12 }) {
          Text('‹')
            .fontSize(30)
            .fontColor('#7C5CE0')
            .width(40)
            .onClick(() => {
              router.back();
            })

          Text('家长后台')
            .fontSize(24)
            .fontWeight(FontWeight.Bold)
            .fontColor('#4A3B32')

          Blank()

          Text('已用 ' + screenTime.usedMinutes() + ' / ' + settings.dailyLimitMinutes + ' 分钟')
            .fontSize(14)
            .fontColor('#9B8574')
        }
        .width('100%')
        .padding({ bottom: 14 })

        Scroll() {
          Column({ space: 16 }) {
            this.todayCard()
            this.limitCard()
            this.whitelistCard()
            this.starCard()
            this.onlineCard()
            this.eyeCareCard()
            this.careCard()
            this.makerCard()
            this.feedbackCard()
            this.exitCard()
          }
          .width('100%')
          .padding({ bottom: 28 })
        }
        .scrollBar(BarState.Off)
        .layoutWeight(1)
      }
      .width('100%')
      .height('100%')
      .padding({ left: 32, right: 32, top: 26 + kiosk.safeTopVp, bottom: 16 + kiosk.safeBottomVp })
      .constraintSize({ maxWidth: 900 })
    }
    .width('100%')
    .height('100%')
    .alignContent(Alignment.Center)
  }

  @Builder
  card(title: string) {
    Row() {
      Text(title)
        .fontSize(16)
        .fontWeight(FontWeight.Medium)
        .fontColor('#4A3B32')
    }
    .width('100%')
    .padding({ bottom: 10 })
  }

  @Builder
  todayCard() {
    Column() {
      this.card('今日使用')
      Progress({
        value: screenTime.usedMinutes(),
        total: Math.max(settings.dailyLimitMinutes, 1),
        type: ProgressType.Linear
      })
        .width('100%')
        .height(10)
        .color(screenTime.remainingMinutes() <= 5 ? '#E5484D' : '#FF8A5C')

      Row() {
        Text('已用 ' + screenTime.usedMinutes() + ' 分钟')
          .fontSize(13)
          .fontColor('#9B8574')
        Blank()
        Text('剩余 ' + screenTime.remainingMinutes() + ' 分钟')
          .fontSize(13)
          .fontColor('#9B8574')
      }
      .width('100%')
      .margin({ top: 8 })
    }
    .width('100%')
    .padding(18)
    .borderRadius(22)
    .backgroundColor('#FFFFFF')
  }

  @Builder
  limitCard() {
    Column({ space: 12 }) {
      this.card('每天可以玩多久')

      Row({ space: 16 }) {
        Button('−5')
          .height(44)
          .width(64)
          .fontSize(16)
          .backgroundColor('#F3E7DA')
          .fontColor('#7A6555')
          .onClick(() => {
            this.changeLimit(-5);
          })

        Text(`${settings.dailyLimitMinutes} 分钟`)
          .fontSize(22)
          .fontWeight(FontWeight.Bold)
          .fontColor('#4A3B32')
          .layoutWeight(1)
          .textAlign(TextAlign.Center)

        Button('+5')
          .height(44)
          .width(64)
          .fontSize(16)
          .backgroundColor('#FFE0C2')
          .fontColor('#B4622B')
          .onClick(() => {
            this.changeLimit(5);
          })
      }
      .width('100%')

      Row({ space: 10 }) {
        ForEach([15, 30, 60], (mins: number) => {
          Button(`加 ${mins} 分钟`)
            .height(40)
            .layoutWeight(1)
            .fontSize(14)
            .backgroundColor('#F1E9FF')
            .fontColor('#6C4BD8')
            .onClick(() => {
              screenTime.grantMinutes(mins);
              screenTime.reevaluate();
              this.getUIContext().getPromptAction().showToast({ message: `已临时加 ${mins} 分钟`, duration: 1800 });
            })
        }, (mins: number) => `grant-${mins}`)
      }
      .width('100%')
    }
    .width('100%')
    .padding(18)
    .borderRadius(22)
    .backgroundColor('#FFFFFF')
  }

  @Builder
  whitelistCard() {
    Column({ space: 12 }) {
      this.card(`应用白名单（${settings.allowedApps.length}）`)

      if (settings.allowedApps.length === 0) {
        Text('还没添加任何应用')
          .fontSize(14)
          .fontColor('#B8A492')
      } else {
        ForEach(settings.allowedApps, (app: AppItem) => {
          Row({ space: 12 }) {
            Text(app.emoji).fontSize(24)
            Column({ space: 2 }) {
              Text(app.name).fontSize(16).fontColor('#4A3B32')
              Text(app.bundleName.length > 0 ? app.bundleName : app.launchUrl)
                .fontSize(12)
                .fontColor('#B8A492')
                .maxLines(1)
                .textOverflow({ overflow: TextOverflow.Ellipsis })
            }
            .alignItems(HorizontalAlign.Start)
            .layoutWeight(1)

            Text('移除')
              .fontSize(14)
              .fontColor('#E5484D')
              .padding({ left: 12, right: 4, top: 6, bottom: 6 })
              .onClick(() => {
                settings.removeApp(app.id);
              })
          }
          .width('100%')
        }, (app: AppItem) => app.id)
      }

      Divider().color('#F0E6DA')

      Text('从预设添加')
        .fontSize(13)
        .fontColor('#9B8574')

      Flex({ wrap: FlexWrap.Wrap }) {
        ForEach(this.availablePresets(), (preset: AppPreset) => {
          Row({ space: 6 }) {
            Text(preset.emoji).fontSize(16)
            Text(preset.label).fontSize(13).fontColor('#6C4BD8')
            Text('+').fontSize(14).fontColor('#6C4BD8')
          }
          .padding({ left: 12, right: 12, top: 8, bottom: 8 })
          .borderRadius(16)
          .backgroundColor('#F4EEFF')
          .margin({ right: 8, bottom: 8 })
          .onClick(() => {
            settings.addApp(appCatalog.toApp(preset));
          })
        }, (preset: AppPreset) => `add-${preset.bundleName}`)
      }
      .width('100%')

      // 手动列出来，比让家长一个个点过去才发现「打不开」要好
      Text('相机 / 计算器 / 备忘录 是华为手机自带的，模拟器上一般没有；点不开的请在上面的白名单里移除')
        .fontSize(12)
        .fontColor('#B8A492')

      Text('手动添加（只填包名即可，入口类名留空会自动试）')
        .fontSize(13)
        .fontColor('#9B8574')

      Row({ space: 8 }) {
        TextInput({ text: this.manualBundle, placeholder: 'com.example.app' })
          .layoutWeight(3)
          .height(48)
          .fontSize(14)
          .borderRadius(14)
          .backgroundColor('#FBF6F0')
          .onChange((value: string) => {
            this.manualBundle = value;
          })
        TextInput({ text: this.manualName, placeholder: '显示名' })
          .layoutWeight(2)
          .height(48)
          .fontSize(14)
          .borderRadius(14)
          .backgroundColor('#FBF6F0')
          .onChange((value: string) => {
            this.manualName = value;
          })
        Button('加')
          .height(48)
          .width(56)
          .fontSize(14)
          .backgroundColor('#7C5CE0')
          .onClick(() => {
            this.addManual();
          })
      }
      .width('100%')

      // 入口类名：鸿蒙拉起应用必须给 abilityName，而系统应用各叫各的。
      // 留空时按 MainAbility / <包名>.MainAbility / EntryAbility 依次试，
      // 只有三方应用用了自定义名字才需要家长填这里。
      TextInput({ text: this.manualAbility, placeholder: '入口类名（可空，如 MainAbility）' })
        .height(44)
        .width('100%')
        .fontSize(13)
        .borderRadius(14)
        .backgroundColor('#FBF6F0')
        .onChange((value: string) => {
          this.manualAbility = value;
        })

      if (this.hint.length > 0) {
        Text(this.hint).fontSize(12).fontColor('#2AA7A0')
      }
    }
    .width('100%')
    .padding(18)
    .borderRadius(22)
    .backgroundColor('#FFFFFF')
  }

  /**
   * 星星商店。兑换物完全由家长定 —— 这是这个功能的关键：
   * 「看 20 分钟动画片」和「周末去公园」对不同家庭价值完全不同，
   * 预设一套商品必然有一半家庭用不上。
   */
  @Builder
  starCard() {
    Column({ space: 12 }) {
      Row() {
        Text(`星星商店（${rewards.shop.length}）`)
          .fontSize(16)
          .fontWeight(FontWeight.Medium)
          .fontColor('#4A3B32')
        Blank()
        Text(`孩子手上 ${rewards.stars} 颗`)
          .fontSize(12)
          .fontColor('#B4622B')
      }
      .width('100%')

      Text('孩子赚到的星星，在这里换东西。关掉的商品不会出现在孩子的商店里；兑换后你要在这里点「已兑现」。')
        .fontSize(12)
        .fontColor('#B8A492')

      if (rewards.shop.length === 0) {
        Text('还没放商品，孩子有星星也没处花')
          .fontSize(13)
          .fontColor('#C4B3A2')
      } else {
        ForEach(rewards.shop, (item: ShopItem) => {
          Row({ space: 10 }) {
            Text(item.emoji).fontSize(22)
            Column({ space: 2 }) {
              Text(item.title).fontSize(15).fontColor('#4A3B32')
              Text(`⭐ ${item.cost}${item.note.length > 0 ? ' · ' + item.note : ''}`)
                .fontSize(11)
                .fontColor('#B8A492')
                .maxLines(1)
                .textOverflow({ overflow: TextOverflow.Ellipsis })
            }
            .alignItems(HorizontalAlign.Start)
            .layoutWeight(1)

            Toggle({ type: ToggleType.Switch, isOn: item.enabled })
              .onChange(() => {
                rewards.toggleShopItem(item.id);
              })

            Text('删')
              .fontSize(13)
              .fontColor('#E5484D')
              .padding({ left: 8, right: 2, top: 6, bottom: 6 })
              .onClick(() => {
                rewards.removeShopItem(item.id);
              })
          }
          .width('100%')
        }, (item: ShopItem) => item.id)
      }

      Divider().color('#F0E6DA')

      Text('加一件新奖品')
        .fontSize(13)
        .fontColor('#9B8574')

      Row({ space: 8 }) {
        TextInput({ text: this.shopEmoji, placeholder: '🎁' })
          .width(56)
          .height(46)
          .fontSize(16)
          .textAlign(TextAlign.Center)
          .borderRadius(13)
          .backgroundColor('#FBF6F0')
          .onChange((v: string) => {
            this.shopEmoji = v;
          })

        TextInput({ text: this.shopTitle, placeholder: '奖品名，如 看动画片 20 分钟' })
          .layoutWeight(3)
          .height(46)
          .fontSize(13)
          .borderRadius(13)
          .backgroundColor('#FBF6F0')
          .onChange((v: string) => {
            this.shopTitle = v;
          })

        TextInput({ text: this.shopCost, placeholder: '星星' })
          .layoutWeight(1)
          .height(46)
          .fontSize(13)
          .type(InputType.Number)
          .borderRadius(13)
          .backgroundColor('#FBF6F0')
          .onChange((v: string) => {
            this.shopCost = v;
          })
      }
      .width('100%')

      Row({ space: 8 }) {
        TextInput({ text: this.shopNote, placeholder: '备注（可空），如 家长陪着看' })
          .layoutWeight(1)
          .height(44)
          .fontSize(12)
          .borderRadius(13)
          .backgroundColor('#FBF6F0')
          .onChange((v: string) => {
            this.shopNote = v;
          })

        Button('加进商店')
          .height(44)
          .fontSize(13)
          .backgroundColor('#7C5CE0')
          .onClick(() => {
            this.addShopItem();
          })
      }
      .width('100%')

      if (rewards.pendingOrders() > 0) {
        Divider().color('#F0E6DA')
        Text(`等待兑现（${rewards.pendingOrders()}）`)
          .fontSize(13)
          .fontColor('#E08A2E')

        ForEach(this.pendingList(), (o: OrderItem) => {
          Row({ space: 8 }) {
            Text(o.emoji).fontSize(18)
            Column({ space: 1 }) {
              Text(o.title).fontSize(14).fontColor('#4A3B32')
              Text(`${timeLabel(o.at)} · 花了 ${o.cost} 颗`)
                .fontSize(10)
                .fontColor('#B8A492')
            }
            .alignItems(HorizontalAlign.Start)
            .layoutWeight(1)

            Text('已兑现')
              .fontSize(13)
              .fontColor('#2AA7A0')
              .padding({ left: 10, right: 10, top: 6, bottom: 6 })
              .borderRadius(12)
              .backgroundColor('#E4F5F4')
              .onClick(() => {
                rewards.markDone(o.id);
              })
          }
          .width('100%')
        }, (o: OrderItem) => o.id)
      }

      Divider().color('#F0E6DA')

      Row({ space: 8 }) {
        Text('手动加减')
          .fontSize(13)
          .fontColor('#9B8574')
          .layoutWeight(1)
        Button('−10')
          .height(38)
          .fontSize(13)
          .backgroundColor('#F3E7DA')
          .fontColor('#7A6555')
          .onClick(() => {
            rewards.adjust(-10, '家长扣减');
          })
        Button('+10')
          .height(38)
          .fontSize(13)
          .backgroundColor('#FFE0C2')
          .fontColor('#B4622B')
          .onClick(() => {
            rewards.adjust(10, '家长奖励');
          })
        Button('+50')
          .height(38)
          .fontSize(13)
          .backgroundColor('#FFE0C2')
          .fontColor('#B4622B')
          .onClick(() => {
            rewards.adjust(50, '家长奖励');
          })
      }
      .width('100%')
    }
    .width('100%')
    .padding(18)
    .borderRadius(22)
    .backgroundColor('#FFFFFF')
  }

  @Builder
  onlineCard() {
    Column({ space: 12 }) {
      this.card('在线故事与内容')

      Row() {
        Text('启用家长服务器')
          .fontSize(15)
          .fontColor('#4A3B32')
          .layoutWeight(1)
        Toggle({ type: ToggleType.Switch, isOn: settings.onlineContentEnabled })
          .onChange((on: boolean) => {
            settings.onlineContentEnabled = on;
            settings.save();
            storyService.refreshOnlineState();
          })
      }
      .width('100%')

      TextInput({ text: this.serverDraft, placeholder: 'http://192.168.1.10:8080' })
        .height(48)
        .fontSize(14)
        .borderRadius(14)
        .backgroundColor('#FBF6F0')
        .onChange((value: string) => {
          this.serverDraft = value;
        })

      Row({ space: 10 }) {
        Text(storyService.onlineEnabled ? '已连接' : '未开启')
          .fontSize(13)
          .fontColor(storyService.onlineEnabled ? '#2AA7A0' : '#B8A492')
          .layoutWeight(1)

        Button('保存地址')
          .height(40)
          .fontSize(14)
          .backgroundColor('#F1E9FF')
          .fontColor('#6C4BD8')
          .onClick(() => {
            settings.serverUrl = this.serverDraft.trim();
            settings.save();
            storyService.refreshOnlineState();
            this.getUIContext().getPromptAction().showToast({ message: '已保存', duration: 1500 });
          })
      }
      .width('100%')
    }
    .width('100%')
    .padding(18)
    .borderRadius(22)
    .backgroundColor('#FFFFFF')
  }

  @Builder
  eyeCareCard() {
    Column({ space: 12 }) {
      this.card('护眼模式')

      Row() {
        Text('暖色滤镜')
          .fontSize(15)
          .fontColor('#4A3B32')
          .layoutWeight(1)
        Toggle({ type: ToggleType.Switch, isOn: settings.eyeCareEnabled })
          .onChange((on: boolean) => {
            settings.eyeCareEnabled = on;
            settings.save();
          })
      }
      .width('100%')

      Row({ space: 12 }) {
        Text('强度').fontSize(13).fontColor('#9B8574')
        Slider({
          value: settings.eyeCareIntensity * 100,
          min: 5,
          max: 55,
          step: 1
        })
          .layoutWeight(1)
          .onChange((value: number, mode: SliderChangeMode) => {
            settings.eyeCareIntensity = value / 100;
            if (mode === SliderChangeMode.End) {
              settings.save();
            }
          })
      }
      .width('100%')
    }
    .width('100%')
    .padding(18)
    .borderRadius(22)
    .backgroundColor('#FFFFFF')
  }

  @Builder
  careCard() {
    Column({ space: 10 }) {
      this.card('心理关怀')

      Text(this.moodDays > 0
        ? `最近 7 天，孩子记了 ${this.moodDays} 天心情。`
        : '最近 7 天，孩子还没有记过心情。')
        .fontSize(14)
        .fontColor('#4A3B32')

      Text('心情记录和「想对爸妈说的话」只有孩子自己能看，这里不显示内容 —— 那是他的空间。' +
        '孩子知道家长能翻，就不会写真话了。')
        .fontSize(12)
        .lineHeight(19)
        .fontColor('#B8A492')

      LargeButton({
        text: '给家长的心理课（10 节）',
        bg: '#7E57C2',
        onTap: () => {
          // 直接从家长端进来时定位到「给家长」那一套，不用再切一次
          router.careAudience = 'parents';
          router.push(PageId.CareLesson);
        }
      })
    }
    .width('100%')
    .padding(18)
    .borderRadius(22)
    .backgroundColor('#FFFFFF')
  }

  /**
   * 小游戏工坊：家长自己做游戏。
   *
   * 编辑器是一份四端共用的 H5，孩子那张「游戏乐园」卡片管的是玩，
   * 这里是「做」——分开两块，是因为进入前者的机会远多于后者。
   */
  @Builder
  makerCard() {
    Column({ space: 10 }) {
      this.card('小游戏工坊')

      Text('不写代码就能做游戏：选一个玩法（接住 / 躲避 / 点一点），' +
        '把主角和道具换成内置表情或自己的照片，就能试玩、导出。')
        .fontSize(12)
        .lineHeight(19)
        .fontColor('#B8A492')

      LargeButton({
        text: '打开工坊',
        bg: '#7E57C2',
        onTap: () => {
          router.push(PageId.Maker);
        }
      })
    }
    .width('100%')
    .padding(18)
    .borderRadius(22)
    .backgroundColor('#FFFFFF')
  }

  /**
   * 意见反馈与问卷：与 Windows / 安卓 / Linux 的同一张卡片对应。
   * 数据进官网，不跟家长自建服务器走 —— 那台机器开发者看不到。
   */
  @Builder
  feedbackCard() {
    Column({ space: 10 }) {
      this.card('意见反馈与问卷')

      Text('遇到问题、想要新功能都可以直接说；满意度调查与使用问卷会同步到官网统计。' +
        '断网时先存本机，联网后自动补传。')
        .fontSize(12)
        .lineHeight(19)
        .fontColor('#B8A492')

      LargeButton({
        text: '去反馈',
        bg: '#E2603D',
        onTap: () => {
          router.push(PageId.Feedback);
        }
      })
    }
    .width('100%')
    .padding(18)
    .borderRadius(22)
    .backgroundColor('#FFFFFF')
  }

  @Builder
  exitCard() {
    Column({ space: 10 }) {
      this.card('退出儿童桌面')

      Text('退出后设备会回到系统桌面，锁定状态解除。系统级锁定需要企业版鸿蒙 + MDM 授权，普通设备做不到，详见 README。')
        .fontSize(12)
        .fontColor('#B8A492')

      LargeButton({
        text: '退出儿童桌面',
        bg: '#E5484D',
        onTap: () => {
          this.confirmExit();
        }
      })
    }
    .width('100%')
    .padding(18)
    .borderRadius(22)
    .backgroundColor('#FFFFFF')
  }

  private addShopItem(): void {
    const title: string = this.shopTitle.trim();
    if (title.length === 0) {
      this.hint = '先填奖品名字';
      return;
    }
    const cost: number = parseInt(this.shopCost, 10);
    rewards.addShopItem(title, this.shopEmoji.trim(), isNaN(cost) ? 30 : cost, this.shopNote.trim());
    this.hint = `已加入「${title}」`;
    this.shopTitle = '';
    this.shopNote = '';
  }

  private pendingList(): OrderItem[] {
    const out: OrderItem[] = [];
    for (let i = 0; i < rewards.orders.length && out.length < 10; i++) {
      if (!rewards.orders[i].done) {
        out.push(rewards.orders[i]);
      }
    }
    return out;
  }

  private changeLimit(delta: number): void {
    const next = Math.min(600, Math.max(10, settings.dailyLimitMinutes + delta));
    settings.dailyLimitMinutes = next;
    settings.save();
    screenTime.reevaluate();
  }

  private availablePresets(): AppPreset[] {
    const out: AppPreset[] = [];
    const all = appCatalog.presets();
    for (let i = 0; i < all.length; i++) {
      if (!settings.isAllowed(all[i].bundleName)) {
        out.push(all[i]);
      }
    }
    return out;
  }

  private addManual(): void {
    const bundle = this.manualBundle.trim();
    if (bundle.length === 0) {
      this.hint = '先填包名';
      return;
    }
    const name = this.manualName.trim().length > 0 ? this.manualName.trim() : bundle;
    settings.addApp(makeApp(name, bundle, '📦', '#8E9AAF', this.manualAbility.trim()));
    this.manualBundle = '';
    this.manualName = '';
    this.manualAbility = '';
    // 鸿蒙不开放跨应用包名查询（getApplicationInfo 已在 API 26 移除，
    // 替代的 getBundleInfo 需要 API 14+ 且要申请 GET_BUNDLE_INFO），
    // 所以这里不做预校验，能否打开以孩子实际点击为准。
    this.hint = `已添加「${name}」，点一下磁贴试试能不能打开`;

  }

  private confirmExit(): void {
    this.getUIContext().getPromptAction().showDialog({
      title: '退出儿童桌面？',
      message: '退出后会回到系统桌面，孩子可以直接使用设备。',
      buttons: [
        { text: '取消', color: '#9B8574' },
        { text: '退出', color: '#E5484D' }
      ]
    })
      .then((result: promptAction.ShowDialogSuccessResponse) => {
        if (result.index === 1) {
          const context = this.getUIContext().getHostContext() as common.UIAbilityContext;
          kiosk.exitApp(context);
        }
      })
      .catch(() => {
      });
  }
}
