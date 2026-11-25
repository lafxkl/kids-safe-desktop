import { webview } from '@kit.ArkWeb';
import { kiosk } from '../core/Kiosk';
import { rewards } from '../core/Rewards';
import { router } from '../core/Router';

/**
 * 游戏乐园 —— 内置的几个小游戏，全部是「离线 H5 + Web 组件」。
 *
 * 为什么不原生重写：
 *   《猫国建设者》是十几万行 dojo + React 15 的老代码，原生化等于重写整个游戏；
 *   象棋、五子棋、数独、2048、植物大战僵尸、蛇蛇方块虽然是自己写的，
 *   但用 Canvas/DOM 实现一次，桌面端和鸿蒙端共用同一份代码，
 *   界面还能在电脑上用无头浏览器跑断言验证。
 *
 * 每个游戏的资源都在 resources/rawfile/playland/<key>/ 下，完全离线：
 *   xiangqi   中国象棋（棋盘棋子音效取自 Chess-Chess 工程，规则自研并 perft 校验）
 *   gomoku    五子棋（赢法数组与 AI 权重取自 Gomoku-master 工程，界面重写）
 *   sudoku    数独挑战（本地生成 + 唯一解校验）
 *   game2048  2048（配色取自参考图，4x4 / 5x5 / 排行榜）
 *   pvz       植物大战僵尸（植物/僵尸 GIF 取自素材包，玩法自研，横屏）
 *   snake     蛇蛇方块（Canvas 手绘，配色参考素材包 UI）
 *   kittens   猫国建设者（第三方，已本地化依赖与语言包）
 *   baobei36  宝贝三十六计（防拐教育，素材取自 Baby-s_36_Skills，
 *             联机模式配套 Baby36Server，家长出题孩子判断）
 *   virusfighter 病毒斗士（贪吃蛇 + 弹球，素材取自 Adapted-game-virus-fighter）
 *   chengyu   成语大挑战（420 道看图猜成语，素材取自 egret_cydtz，题库已转简体）
 *   game24    24 点（四则运算凑 24，出题保证有解）
 *   memory    记住它（移植 kidrememberit 的 Cocos2d-x 版，记忆还原 + 翻牌配对交替）
 *   threes    三三合成（移植 mr3，原工程依赖宿主 opengl 模块，这里改成纯 H5 才跑得起来）
 *   wordzh    词语组合（连连看：字块拼两字词 / 四字成语，绿色配色）
 *   worden    单词组合（同一套判定，单位换成字母，配色照概念图的白底彩块）
 *   mouse     老鼠吃奶酪（移植 mice_eat_cheese_android，原版是 Qt 的 LFEngine 工程，
 *             算法在 map.h/game.h 里可以直接读，界面和素材全部重写）
 *
 * 这个页面只负责三件事：
 *   1. 按 gameKey 把对应 rawfile 装进 Web 组件；
 *   2. 顶部给一条 34vp 的细栏放返回键 + 游戏名；
 *   3. 拦掉任何跳出本地包的外链（儿童端没有地址栏，跳走就回不来）。
 */

/** 一个内置游戏的静态描述。加新游戏只要往 GAMES 里加一项 + 一个 if 分支。 */
class GameSpec {
  key: string = '';
  title: string = '';
  emoji: string = '';
  loadingTip: string = '';
  accent: string = '#7C5CE0';

  constructor(key: string, title: string, emoji: string, tip: string, accent: string) {
    this.key = key;
    this.title = title;
    this.emoji = emoji;
    this.loadingTip = tip;
    this.accent = accent;
  }
}

export const GAMES: GameSpec[] = [
  new GameSpec('xiangqi', '中国象棋', '♟️', '摆好棋盘…', '#8D6E63'),
  new GameSpec('gomoku', '五子棋', '⚫', '落第一颗子…', '#5D4037'),
  new GameSpec('sudoku', '数独挑战', '🔢', '正在出题…', '#3F6FB8'),
  new GameSpec('game2048', '2048', '🔸', '滑动合并…', '#C79A4E'),
  new GameSpec('pvz', '植物大战僵尸', '🌻', '种下第一颗豌豆…', '#7CB342'),
  new GameSpec('snake', '蛇蛇方块', '🐍', '小蛇出发…', '#FFD400'),
  new GameSpec('kittens', '猫国建设者', '🐱', '正在搭猫窝…', '#7C5CE0'),
  new GameSpec('baobei36', '宝贝三十六计', '🕵️', '正在翻开故事书…', '#F0862F'),
  new GameSpec('virusfighter', '病毒斗士', '🦠', '正在准备消毒…', '#27C66F'),
  new GameSpec('chengyu', '成语大挑战', '📜', '正在摊开画卷…', '#B8863C'),
  new GameSpec('game24', '24 点', '🎯', '正在发牌…', '#2AA7A0'),
  new GameSpec('memory', '记住它', '🧠', '正在洗牌…', '#E0629B'),
  new GameSpec('threes', '三三合成', '🧩', '正在摆方块…', '#3F8CFF'),
  new GameSpec('wordzh', '词语组合', '🌿', '正在摆字块…', '#3E9C5C'),
  new GameSpec('worden', '单词组合', '🎈', '正在摆字母…', '#4A7CFF'),
  new GameSpec('mouse', '老鼠吃奶酪', '🐭', '正在铺地图…', '#D89B2C')
];

/**
 * 暴露给 H5 的星星上报桥。H5 里直接调 `KidsSafe.award(key, 星数, 成绩, 说明)`。
 *
 * 为什么走 javaScriptProxy 而不是让 H5 自己写 localStorage：
 *   存档分两套会立刻不同步（游戏里显示 3 颗，排行榜上还是 0）。统一收到 ArkTS 侧，
 *   排行榜、商店余额、家长后台看到的是同一份数据。
 */
class AwardBridge {
  /** 页面注入真正的处理函数；桥对象本身不碰 UI 状态，方好测。 */
  handler: (key: string, stars: number, score: number, sub: string) => void =
    (key: string, stars: number, score: number, sub: string) => {
    };

  award(key: string, stars: number, score: number, sub: string): void {
    this.handler(key, stars, score, sub);
  }
}

export function gameSpecOf(key: string): GameSpec {
  for (let i = 0; i < GAMES.length; i++) {
    if (GAMES[i].key === key) {
      return GAMES[i];
    }
  }
  return GAMES[GAMES.length - 1];
}

@ComponentV2
export struct GamePage {
  /** 由首页磁贴写入 router.gameKey 后再跳过来。 */
  @Param gameKey: string = 'kittens';

  private controller: webview.WebviewController = new webview.WebviewController();

  /** 首帧到 onPageEnd 之间显示加载态，别让孩子对着白屏。 */
  @Local loading: boolean = true;

  /** 本局累计拿到的星星，显示在标题栏右侧。 */
  @Local sessionStars: number = 0;
  /** 「⭐ +3」浮层：比 toast 轻，不打断操作，孩子也能看见自己赚到了。 */
  @Local flash: string = '';
  @Local flashOn: boolean = false;

  private bridge: AwardBridge = new AwardBridge();
  private flashTimer: number = -1;
  private kittenTimer: number = -1;
  private kittenStart: number = 0;

  aboutToAppear(): void {
    this.bridge.handler = (key: string, stars: number, score: number, sub: string) => {
      this.onAward(key, stars, score, sub);
    };
    // 猫国没有「结束」这个概念（增量游戏），只能按时长结算。
    // 其余游戏自己在结束/得分点调 KidsSafe.award()。
    if (this.gameKey === 'kittens') {
      this.kittenStart = Date.now();
      this.kittenTimer = setInterval(() => {
        const mins: number = Math.floor((Date.now() - this.kittenStart) / 60000);
        if (mins > 0 && mins % 5 === 0) {
          this.onAward('kittens', 2, mins, `${mins} 分钟`);
        }
      }, 60000);
    }
  }

  aboutToDisappear(): void {
    if (this.flashTimer >= 0) {
      clearTimeout(this.flashTimer);
      this.flashTimer = -1;
    }
    if (this.kittenTimer >= 0) {
      clearInterval(this.kittenTimer);
      this.kittenTimer = -1;
    }
  }

  private spec(): GameSpec {
    return gameSpecOf(this.gameKey);
  }

  /**
   * 收一次上报。
   *
   * key 强制改成本页的 gameKey：万一某个游戏抄了别人的上报代码，或者有人手改 localStorage
   * 去刷别的模块，星星也只会落在「当前正在玩的这个游戏」上，不会串榜。
   */
  private onAward(key: string, stars: number, score: number, sub: string): void {
    const target: string = this.gameKey;
    const given: number = rewards.grant(target, stars, score, sub);
    if (given > 0) {
      this.sessionStars = this.sessionStars + given;
      this.flash = `⭐ +${given}`;
      this.flashOn = true;
      if (this.flashTimer >= 0) {
        clearTimeout(this.flashTimer);
      }
      this.flashTimer = setTimeout(() => {
        this.flashOn = false;
        this.flashTimer = -1;
      }, 1500);
    }
  }

  build() {
    Column() {
      // 顶部细栏：返回 + 游戏名。
      // 原来是浮在 Web 上的半透明圆钮，问题是它正好压在游戏自己的顶栏上
      // （阳光数、长度、难度、波次都在左上角），而且 ArkWeb 的图层顺序
      // 在不同设备上不一定压得住。改成独立一条 34vp 的栏，谁也不挡谁。
      this.titleBar()

      Stack({ alignContent: Alignment.TopStart }) {
        // 各游戏的入口写死字面量：$rawfile 需要编译期常量，传变量过不了编译。
        // 属性统一收在 gameWeb() 里，避免七份配置慢慢长歪。
        if (this.gameKey === 'xiangqi') {
          this.gameWeb($rawfile('playland/xiangqi/index.html'))
        } else if (this.gameKey === 'gomoku') {
          this.gameWeb($rawfile('playland/gomoku/index.html'))
        } else if (this.gameKey === 'sudoku') {
          this.gameWeb($rawfile('playland/sudoku/index.html'))
        } else if (this.gameKey === 'game2048') {
          this.gameWeb($rawfile('playland/game2048/index.html'))
        } else if (this.gameKey === 'pvz') {
          this.gameWeb($rawfile('playland/pvz/index.html'))
        } else if (this.gameKey === 'snake') {
          this.gameWeb($rawfile('playland/snake/index.html'))
        } else if (this.gameKey === 'baobei36') {
          this.gameWeb($rawfile('playland/baobei36/index.html'))
        } else if (this.gameKey === 'virusfighter') {
          this.gameWeb($rawfile('playland/virusfighter/index.html'))
        } else if (this.gameKey === 'chengyu') {
          this.gameWeb($rawfile('playland/chengyu/index.html'))
        } else if (this.gameKey === 'game24') {
          this.gameWeb($rawfile('playland/game24/index.html'))
        } else if (this.gameKey === 'memory') {
          this.gameWeb($rawfile('playland/memory/index.html'))
        } else if (this.gameKey === 'threes') {
          this.gameWeb($rawfile('playland/threes/index.html'))
        } else if (this.gameKey === 'wordzh') {
          this.gameWeb($rawfile('playland/wordzh/index.html'))
        } else if (this.gameKey === 'worden') {
          this.gameWeb($rawfile('playland/worden/index.html'))
        } else if (this.gameKey === 'mouse') {
          this.gameWeb($rawfile('playland/mouse/index.html'))
        } else {
          this.gameWeb($rawfile('playland/kittens/index.html'))
        }

        if (this.flashOn) {
          Column() {
            Text(this.flash)
              .fontSize(17)
              .fontWeight(FontWeight.Bold)
              .fontColor('#8A4B12')
              .padding({ left: 18, right: 18, top: 8, bottom: 8 })
              .borderRadius(18)
              .backgroundColor('#FFEBCF')
              .shadow({ radius: 14, color: '#40000000', offsetY: 4 })
          }
          .width('100%')
          .padding({ top: 12 })
          .hitTestBehavior(HitTestMode.None)
          .zIndex(9)
        }

        if (this.loading) {
          Column({ space: 14 }) {
            Text(this.spec().emoji).fontSize(54)
            Text(this.spec().title).fontSize(20).fontWeight(FontWeight.Bold).fontColor('#5D4037')
            Text(this.spec().loadingTip).fontSize(14).fontColor('#9B8574')
            LoadingProgress().width(36).height(36).color(this.spec().accent)
          }
          .width('100%')
          .height('100%')
          .justifyContent(FlexAlign.Center)
          .backgroundColor('#FFF7EF')
        }
      }
      .layoutWeight(1)
      .width('100%')
    }
    .width('100%')
    .height('100%')
    // 沉浸式下状态栏是隐藏的，摄像头挖孔不参与布局，得自己把高度让出来，
    // 否则「返回」会被挖孔压住（其余页面同样的处理，见 README「六、」）
    .padding({ top: kiosk.safeTopVp })
    .backgroundColor('#2B2119')
  }

  @Builder
  titleBar() {
    Row({ space: 6 }) {
      Text('‹')
        .fontSize(24)
        .fontColor('#FFFFFF')
        .fontWeight(FontWeight.Medium)
      Text('返回')
        .fontSize(13)
        .fontColor('#FFFFFF')
      Blank()
      if (this.sessionStars > 0) {
        Text(`⭐ +${this.sessionStars}`)
          .fontSize(12)
          .fontWeight(FontWeight.Bold)
          .fontColor('#FFD79A')
          .margin({ right: 10 })
      }
      Text(`${this.spec().emoji} ${this.spec().title}`)
        .fontSize(12)
        .fontColor('#FFF3E0')
        .opacity(0.9)
    }
    .width('100%')
    .height(34)
    .padding({ left: 12, right: 14 })
    .backgroundColor('#2B2119')
    .onClick(() => {
      router.back();
    })
  }

  @Builder
  gameWeb(src: Resource) {
    Web({ src: src, controller: this.controller })
      .width('100%')
      .height('100%')
      // H5 通过 window.KidsSafe.award(key, stars, score, sub) 上报成绩
      .javaScriptProxy({
        object: this.bridge,
        name: 'KidsSafe',
        methodList: ['award'],
        controller: this.controller
      })
      .backgroundColor('#00000000')
      // 猫国的存档在 localStorage，其余几个不用但也无害
      .domStorageAccess(true)
      .javaScriptAccess(true)
      .fileAccess(true)
      .imageAccess(true)
      // 双击缩放/双指缩放会让点格子变得很难，全部关掉
      .zoomAccess(false)
      .horizontalScrollBarAccess(false)
      // 几个游戏都是深色或浅色固定底，跟随系统深色会把画面反色
      .darkMode(WebDarkMode.Off)
      .onPageEnd(() => {
        this.loading = false;
      })
      .onErrorReceive(() => {
        this.loading = false;
      })
      .onLoadIntercept((event) => {
        // 只允许本地 rawfile。游戏里的外站链接（官网、汉化组、捐赠）
        // 在这里一律拦掉，否则会跳系统浏览器，孩子回不来。
        const url: string = event.data.getRequestUrl();
        if (url.startsWith('resource://rawfile/') || url.startsWith('about:') || url.startsWith('data:')) {
          return false;
        }
        return true;
      })
  }
}
