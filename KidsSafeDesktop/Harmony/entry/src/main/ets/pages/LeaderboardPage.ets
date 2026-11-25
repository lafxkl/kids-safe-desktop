import { Backdrop } from '../components/Backdrop';
import { kiosk } from '../core/Kiosk';
import { ModKind, ModRecord, moduleOf, rewards } from '../core/Rewards';
import { PageId, router } from '../core/Router';

/**
 * 总排行榜：所有小游戏 + 所有知识模块，按星星排。
 *
 * 排序用星星而不是「分数」，是因为分数没有可比性 —— 2048 的 2048 分和象棋的 1 胜
 * 放在一起排毫无意义。星星是统一货币：做完一件事就发，各模块只在右侧报自己的最佳成绩。
 * 这样孩子看到的是「我在哪些地方花的时间最多」，而不是「哪个游戏分数虚高」。
 */

class TabSpec {
  label: string = '';
  kind: ModKind | null = null;

  constructor(label: string, kind: ModKind | null) {
    this.label = label;
    this.kind = kind;
  }
}

const TABS: TabSpec[] = [
  new TabSpec('全部', null),
  new TabSpec('游戏', ModKind.Game),
  new TabSpec('学习', ModKind.Learn)
];

@ComponentV2
export struct LeaderboardPage {
  @Local tab: number = 0;

  aboutToAppear(): void {
    rewards.load();
  }

  build() {
    Stack() {
      Backdrop()

      Column({ space: 12 }) {
        this.header()
        this.summary()
        this.tabs()
        this.list()
      }
      .width('100%')
      .height('100%')
      .padding({ left: 26, right: 26, top: 22 + kiosk.safeTopVp, bottom: 16 + kiosk.safeBottomVp })
      .constraintSize({ maxWidth: 900 })
    }
    .width('100%')
    .height('100%')
    .alignContent(Alignment.Center)
  }

  @Builder
  header() {
    Row({ space: 12 }) {
      Text('‹')
        .fontSize(30)
        .fontColor('#7C5CE0')
        .width(36)
        .onClick(() => {
          router.back();
        })

      Text('总排行榜')
        .fontSize(24)
        .fontWeight(FontWeight.Bold)
        .fontColor('#4A3B32')

      Blank()

      Row({ space: 5 }) {
        Text('⭐').fontSize(15)
        Text(`${rewards.stars}`)
          .fontSize(16)
          .fontWeight(FontWeight.Bold)
          .fontColor('#B4622B')
      }
      .padding({ left: 12, right: 12, top: 7, bottom: 7 })
      .borderRadius(14)
      .backgroundColor('#FFEBCF')
      .onClick(() => {
        router.push(PageId.StarShop);
      })
    }
    .width('100%')
  }

  /** 三格汇总。放最上面是刻意的：先给「我一共赚了多少」，再看别人/别的模块。 */
  @Builder
  summary() {
    Row() {
      this.statCell('可用星星', `${rewards.stars}`, '⭐', '#B4622B')
      Divider().vertical(true).height(34).color('#F0E6DA')
      this.statCell('今天赚的', `${rewards.todayStars}`, '🌱', '#2AA7A0')
      Divider().vertical(true).height(34).color('#F0E6DA')
      this.statCell('一共赚过', `${rewards.total}`, '🏅', '#7C5CE0')
    }
    .width('100%')
    .padding({ top: 14, bottom: 14 })
    .borderRadius(22)
    .backgroundColor('#FFFFFF')
  }

  @Builder
  statCell(label: string, value: string, emoji: string, color: string) {
    Column({ space: 3 }) {
      Row({ space: 4 }) {
        Text(emoji).fontSize(14)
        Text(value).fontSize(22).fontWeight(FontWeight.Bold).fontColor(color)
      }

      Text(label).fontSize(12).fontColor('#B8A492')
    }
    .layoutWeight(1)
  }

  @Builder
  tabs() {
    Row({ space: 8 }) {
      ForEach(TABS, (item: TabSpec, idx: number) => {
        Text(item.label)
          .fontSize(14)
          .fontWeight(this.tab === idx ? FontWeight.Bold : FontWeight.Normal)
          .fontColor(this.tab === idx ? '#FFFFFF' : '#7A6555')
          .textAlign(TextAlign.Center)
          .height(36)
          .layoutWeight(1)
          .borderRadius(18)
          .backgroundColor(this.tab === idx ? '#7C5CE0' : '#FFFFFF')
          .onClick(() => {
            this.tab = idx;
          })
      }, (item: TabSpec) => item.label)
    }
    .width('100%')
  }

  @Builder
  list() {
    if (this.rows().length === 0) {
      Column({ space: 10 }) {
        Text('🏆').fontSize(46)
        Text('还没有成绩')
          .fontSize(17)
          .fontColor('#9B8574')
        Text('去玩一局、读一首诗，这里就会记下来')
          .fontSize(13)
          .fontColor('#C4B3A2')
      }
      .width('100%')
      .layoutWeight(1)
      .justifyContent(FlexAlign.Center)
    } else {
      List({ space: 9 }) {
        ForEach(this.rows(), (rec: ModRecord, idx: number) => {
          ListItem() {
            this.row(rec, idx)
          }
        }, (rec: ModRecord) => rec.key)
      }
      .width('100%')
      .layoutWeight(1)
      .scrollBar(BarState.Off)
      .edgeEffect(EdgeEffect.Spring)
    }
  }

  @Builder
  row(rec: ModRecord, idx: number) {
    Row({ space: 10 }) {
      // 前三名给奖牌，其余给序号 —— 第十名也拿个金牌就没有排行榜的意思了
      if (idx < 3) {
        Text(idx === 0 ? '🥇' : (idx === 1 ? '🥈' : '🥉'))
          .fontSize(20)
          .width(28)
          .textAlign(TextAlign.Center)
      } else {
        Text(`${idx + 1}`)
          .fontSize(15)
          .fontWeight(FontWeight.Medium)
          .fontColor('#B8A492')
          .width(28)
          .textAlign(TextAlign.Center)
      }

      Text(moduleOf(rec.key).emoji)
        .fontSize(20)
        .width(40)
        .height(40)
        .textAlign(TextAlign.Center)
        .borderRadius(13)
        .backgroundColor('#FBF2E7')

      Column({ space: 4 }) {
        Row({ space: 6 }) {
          Text(moduleOf(rec.key).name)
            .fontSize(15)
            .fontWeight(FontWeight.Medium)
            .fontColor('#4A3B32')
          Text(moduleOf(rec.key).kind === ModKind.Learn ? '学习' : '游戏')
            .fontSize(10)
            .fontColor(moduleOf(rec.key).kind === ModKind.Learn ? '#2AA7A0' : '#C77A2E')
            .padding({ left: 6, right: 6, top: 1, bottom: 1 })
            .borderRadius(7)
            .backgroundColor(moduleOf(rec.key).kind === ModKind.Learn ? '#E4F5F4' : '#FDF0E2')
        }

        Text(this.subLine(rec))
          .fontSize(12)
          .fontColor('#A1907F')
          .maxLines(1)
          .textOverflow({ overflow: TextOverflow.Ellipsis })
      }
      .alignItems(HorizontalAlign.Start)
      .layoutWeight(1)

      Column({ space: 2 }) {
        Text(`⭐ ${rec.stars}`)
          .fontSize(15)
          .fontWeight(FontWeight.Bold)
          .fontColor('#B4622B')

        Text(`${rec.times} 次`)
          .fontSize(11)
          .fontColor('#C4B3A2')
      }
      .alignItems(HorizontalAlign.End)
    }
    .width('100%')
    .padding({ left: 12, right: 14, top: 11, bottom: 11 })
    .borderRadius(18)
    .backgroundColor('#FFFFFF')
  }

  private rows(): ModRecord[] {
    const kind: ModKind | null = TABS[this.tab].kind;
    return rewards.ranked(kind);
  }

  /**
   * 「最佳 12 首 · 静夜思」这类说明。
   *
   * 成绩和单位必须自己拼：rec.sub 里存的是一次具体表现（哪首诗、哪一波、什么难度），
   * 单看它孩子不知道「12」是 12 什么。所以统一写成「最佳 N 单位 · 细节」，
   * 细节超过 20 字就掐掉 —— 一行放不下，右侧会给省略号，看起来像没写完。
   */
  private subLine(rec: ModRecord): string {
    const spec = moduleOf(rec.key);
    const head: string = rec.score > 0 ? `最佳 ${rec.score} ${spec.unit}` : `还没有成绩`;
    if (rec.sub.length > 0 && rec.sub.length <= 20) {
      return `${head} · ${rec.sub}`;
    }
    return head;
  }
}
