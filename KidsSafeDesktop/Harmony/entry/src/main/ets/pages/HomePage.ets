import { common } from '@kit.AbilityKit';
import { AppTile } from '../components/AppTile';
import { Backdrop } from '../components/Backdrop';
import { appLauncher } from '../core/AppLauncher';
import { kiosk } from '../core/Kiosk';
import { GateIntent, PageId, router } from '../core/Router';
import { rewards } from '../core/Rewards';
import { screenTime } from '../core/ScreenTime';
import { settings } from '../core/Settings';
import { AppItem, makeApp } from '../model/Models';

/**
 * 儿童桌面。两段式：问候 → 我的应用（内置内容 + 家长允许的外部应用混排）。
 *
 * 所有入口都是同一种磁贴：内置内容靠 emoji + 纯色圆区分，外部应用靠真实 accent 色。
 * 不再给某个功能单独做横幅——混排时两套视觉会立刻露馅。
 */

/** 内置入口：一个磁贴数据 + 点击后去哪一页。 */
class BuiltIn {
  app: AppItem = new AppItem();
  page: PageId = PageId.Home;
  /** 只有游戏入口用得上：写进 router.gameKey 决定打开哪个离线包。 */
  gameKey: string = '';

  constructor(app: AppItem, page: PageId, gameKey: string = '') {
    this.app = app;
    this.page = page;
    this.gameKey = gameKey;
  }
}

@ComponentV2
export struct HomePage {
  /**
   * 内置内容入口。顺序即桌面上的先后顺序，家长允许的外部应用排在它们后面。
   * 走 AppTile 渲染而不是自绘，圆直径 / emoji 字号 / 下方名字才会和外部应用完全一致。
   */
  private builtIns: BuiltIn[] = [];

  aboutToAppear(): void {
    if (!settings.onboardingDone) {
      router.reset(PageId.Onboarding);
      return;
    }
    if (screenTime.isLocked()) {
      router.replace(PageId.Lockout);
      return;
    }
    this.seedBuiltIns();
    screenTime.start();
  }

  private seedBuiltIns(): void {
    if (this.builtIns.length > 0) {
      return;
    }
    this.builtIns = [
      this.builtIn('builtin-story', '故事屋', '🌙', '#3F51B5', PageId.StoryHouse),
      this.builtIn('builtin-poem', '古诗学习', '📜', '#8D6E63', PageId.PoemWorld),
      this.builtIn('builtin-rhyme', '口诀乐园', '📚', '#8E24AA', PageId.RhymeWorld),
      // 学习天排在知识区末尾：「读」完古诗口诀，接着就是「练」。
      // 它自己内部再分口算和模拟考，桌面上不占两格。
      this.builtIn('builtin-learn', '学习天地', '📖', '#0288D1', PageId.LearnWorld),
      this.builtIn('builtin-safety', '安全知识', '🛡️', '#E53935', PageId.SafetyWorld),
      // 安全问答紧跟安全知识：先读贴士再答题，是同一个主题的两种形态。
      this.builtIn('builtin-safequiz', '安全问答', '❓', '#EF5350', PageId.SafetyQuiz),
      // 关怀中心排在游戏前面：它不是学科内容，也不是游戏，
      // 而是「心里不舒服时找得到的地方」。孩子真想找它的时候，
      // 不该先翻过一屏游戏磁贴。
      this.builtIn('builtin-care', '关怀中心', '💗', '#EC6E93', PageId.CareCenter),
      // 内置游戏放最后：它们是唯一「停不下来」的入口，排前面孩子会直接开游戏，
      // 学习类内容永远轮不到。游戏之间按「动脑 → 综合 → 反应」排：
      // 棋类靠前，反应类靠后。
      this.builtIn('builtin-game-xiangqi', '中国象棋', '♟️', '#C62828', PageId.Game, 'xiangqi'),
      this.builtIn('builtin-game-gomoku', '五子棋', '⚫', '#5D4037', PageId.Game, 'gomoku'),
      this.builtIn('builtin-game-sudoku', '数独挑战', '🔢', '#3F6FB2', PageId.Game, 'sudoku'),
      this.builtIn('builtin-game-24', '24 点', '🎯', '#2AA7A0', PageId.Game, 'game24'),
      this.builtIn('builtin-game-chengyu', '成语大挑战', '📜', '#B8863C', PageId.Game, 'chengyu'),
      // 词语组合 / 单词组合紧挨成语大挑战：都是「认字组词」，语文的排在前面，
      // 英语的排在后面。两个游戏完全独立（各自的关卡进度和星星），
      // 因为语文是字块、英语是字母块，混在一起孩子会分不清规则。
      this.builtIn('builtin-game-wordzh', '词语组合', '🌿', '#3E9C5C', PageId.Game, 'wordzh'),
      this.builtIn('builtin-game-worden', '单词组合', '🎈', '#4A7CFF', PageId.Game, 'worden'),
      // 老鼠吃奶酪接在组词后面：都是「看清楚再动」的安静游戏，
      // 排在反应类前面，孩子先静下来再玩后面的。
      this.builtIn('builtin-game-mouse', '老鼠吃奶酪', '🐭', '#D89B2C', PageId.Game, 'mouse'),
      this.builtIn('builtin-game-2048', '2048', '🔸', '#C79A4E', PageId.Game, 'game2048'),
      // 三三合成紧挨 2048：同样是「滑一下、合一块」，孩子一上手就懂是同一类；
      // 区别在 1+2=3 这条规则，正好是 2048 之后想再动脑一档的难度。
      this.builtIn('builtin-game-threes', '三三合成', '🧩', '#3F8CFF', PageId.Game, 'threes'),
      // 记住它排在游戏区靠前的位置：它不靠手速，靠「看仔细」，
      // 玩完反应类游戏回来玩它，正好把注意力收一收。
      this.builtIn('builtin-game-memory', '记住它', '🧠', '#E0629B', PageId.Game, 'memory'),
      // 跟「安全知识」是一路的，但它是游戏形态，所以留在游戏区；
      // 排在这里也是刻意的：玩半小时反应类之后，孩子更容易静下来做判断。
      this.builtIn('builtin-game-baobei36', '宝贝三十六计', '🕵️', '#F0862F', PageId.Game, 'baobei36'),
      this.builtIn('builtin-game-pvz', '植物大战僵尸', '🌻', '#7CB342', PageId.Game, 'pvz'),
      this.builtIn('builtin-game-virusfighter', '病毒斗士', '🦠', '#27C66F', PageId.Game, 'virusfighter'),
      this.builtIn('builtin-game-snake', '蛇蛇方块', '🐍', '#F9A825', PageId.Game, 'snake'),
      this.builtIn('builtin-game-kittens', '猫国', '🐱', '#EF6C00', PageId.Game, 'kittens')
    ];
  }

  /**
   * 内置项没有包名，makeApp 会拿时间戳兜底当 id——同一毫秒建两个就会撞 key。
   * 所以这里显式给一个稳定的 id。
   */
  private builtIn(id: string, name: string, emoji: string, accent: string, page: PageId, gameKey: string = ''): BuiltIn {
    const app: AppItem = makeApp(name, '', emoji, accent);
    app.id = id;
    return new BuiltIn(app, page, gameKey);
  }

  build() {
    Stack() {
      Backdrop()

      Column({ space: 20 }) {
        this.header()
        this.appsSection()
      }
      .width('100%')
      .height('100%')
      // top 加上 kiosk.safeTopVp：沉浸式下隐藏了状态栏，默认安全区不含摄像头挖孔，
      // 不加这一项问候行会被挖孔压住。
      .padding({ left: 40, right: 40, top: 28 + kiosk.safeTopVp, bottom: 24 })
      .constraintSize({ maxWidth: 1200 })
    }
    .width('100%')
    .height('100%')
    .alignContent(Alignment.Center)
  }

  @Builder
  header() {
    Column({ space: 10 }) {
      Row() {
        Row({ space: 12 }) {
          Text(settings.avatarEmoji)
            .fontSize(34)
            .width(52)
            .height(52)
            .textAlign(TextAlign.Center)
            .borderRadius(18)
            .backgroundColor('#FFFFFF')

          Column({ space: 2 }) {
            Text(`你好，${settings.nickname}`)
              .fontSize(24)
              .fontWeight(FontWeight.Bold)
              .fontColor('#4A3B32')

            Text(`今天还能玩 ${screenTime.remainingMinutes()} 分钟`)
              .fontSize(15)
              .fontColor(screenTime.remainingMinutes() <= 5 ? '#E5484D' : '#9B8574')
          }
          .alignItems(HorizontalAlign.Start)
        }

        Blank()

        Row({ space: 6 }) {
          Text('🔒').fontSize(15)
          Text('家长').fontSize(16).fontColor('#7C5CE0')
        }
        .padding({ left: 16, right: 16, top: 10, bottom: 10 })
        .borderRadius(20)
        .backgroundColor('#F1E9FF')
        .onClick(() => {
          router.gateIntent = GateIntent.Dashboard;
          router.push(PageId.Gate);
        })
      }
      .width('100%')

      // 星星和排行榜单独一行。塞进上面那行在 360vp 屏上会把问候语挤到换行，
      // 而且这两个入口孩子每天都要点，值得占一条独立的位置。
      Row({ space: 10 }) {
        Row({ space: 6 }) {
          Text('⭐').fontSize(16)
          Text(`${rewards.stars}`)
            .fontSize(17)
            .fontWeight(FontWeight.Bold)
            .fontColor('#B4622B')
          Text('星星').fontSize(13).fontColor('#B4622B').opacity(0.8)
        }
        .padding({ left: 14, right: 14, top: 8, bottom: 8 })
        .borderRadius(16)
        .backgroundColor('#FFEBCF')
        .onClick(() => {
          router.push(PageId.StarShop);
        })

        Row({ space: 6 }) {
          Text('🏆').fontSize(16)
          Text('总排行榜').fontSize(14).fontColor('#6C4BD8')
        }
        .padding({ left: 14, right: 14, top: 8, bottom: 8 })
        .borderRadius(16)
        .backgroundColor('#F1E9FF')
        .onClick(() => {
          router.push(PageId.Leaderboard);
        })

        Blank()
      }
      .width('100%')
    }
    .width('100%')
  }

  @Builder
  appsSection() {
    Column({ space: 12 }) {
      Row() {
        Text('我的应用')
          .fontSize(18)
          .fontWeight(FontWeight.Medium)
          .fontColor('#4A3B32')
        Blank()
        // 内置入口也占格子，所以两边都要算，否则数出来的个数和屏幕上的磁贴对不上。
        Text(`${this.builtIns.length + settings.allowedApps.length} 个`)
          .fontSize(13)
          .fontColor('#B8A492')
      }
      .width('100%')

      if (settings.allowedApps.length === 0) {
        Text('还没挑应用，请家长点右上角「家长」进后台添加')
          .fontSize(13)
          .fontColor('#C4B3A2')
          .width('100%')
          .textAlign(TextAlign.Start)
      }

      Grid() {
        // 内置内容固定排在外部应用前面：孩子最常点的是这几个，不该排到第二屏去。
        ForEach(this.builtIns, (item: BuiltIn) => {
          GridItem() {
            AppTile({
              app: item.app,
              onTap: () => {
                // 游戏入口先记下要开哪一个，游戏页按 gameKey 选 rawfile
                if (item.gameKey.length > 0) {
                  router.gameKey = item.gameKey;
                }
                router.push(item.page);
              }
            })
          }
        }, (item: BuiltIn) => item.app.id)

        ForEach(settings.allowedApps, (app: AppItem) => {
          GridItem() {
            AppTile({
              app: app,
              onTap: () => {
                this.open(app);
              }
            })
          }
        }, (app: AppItem) => app.id)
      }
      // 3 列：360vp 屏减去左右边距后每格约 88vp，磁贴才放得下大图标和名字。
      // 4 列时每格仅约 62vp，磁贴会互相重叠。
      .columnsTemplate('1fr 1fr 1fr')
      .columnsGap(20)
      .rowsGap(22)
      .layoutWeight(1)
      .scrollBar(BarState.Off)
    }
    .width('100%')
    .layoutWeight(1)
  }

  private async open(app: AppItem): Promise<void> {
    const context = this.getUIContext().getHostContext() as common.UIAbilityContext;
    const ok = await appLauncher.launch(context, app);
    if (!ok) {
      this.getUIContext().getPromptAction().showToast({ message: appLauncher.errorText, duration: 2600 });
    }
  }
}
