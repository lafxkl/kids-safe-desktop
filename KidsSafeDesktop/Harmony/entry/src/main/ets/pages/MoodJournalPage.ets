import { Backdrop } from '../components/Backdrop';
import { PageHeader } from '../components/PageHeader';
import { care, CareStore, MoodEntry, shortDay } from '../core/Care';
import { kiosk } from '../core/Kiosk';
import { router } from '../core/Router';
import { MoodKind, moodOf } from '../data/CareData';

/**
 * 心情日记。对应 C# 的 MoodJournalPage。
 *
 * 14 天趋势 + 三个数字 + 全部记录。
 *
 * 这里刻意没有「评价」：不写「你很乐观」「你最近很消极」，
 * 只写「最近 14 天你记录了 8 天」这种事实句。
 * 唯一会多说一句的情况是「想人 / 难过」出现 3 次以上 ——
 * 那时给出「试试呼吸放松，或者打 12355」，指向动作而不是诊断。
 */

class TrendCell {
  emoji: string = '·';
  color: string = '#EFEAF0';
  has: boolean = false;
  height: number = 14;
  label: string = '';
  isToday: boolean = false;
}

/** 列表里的一条：比原始记录多带了展示用的颜色与文案。 */
class HistoryRow {
  emoji: string = '';
  color: string = '#EFEAF0';
  label: string = '';
  note: string = '';
  day: string = '';
}

@ComponentV2
export struct MoodJournalPage {
  private repo: CareStore = care;

  @Local trend: TrendCell[] = [];
  @Local days: number = 0;
  @Local streak: number = 0;
  @Local most: string = '—';
  @Local insight: string = '';
  @Local rows: HistoryRow[] = [];

  aboutToAppear(): void {
    this.render();
  }

  private render(): void {
    const recent: (MoodEntry | null)[] = this.repo.recent(14);
    const cells: TrendCell[] = [];
    for (let i = 0; i < recent.length; i++) {
      const e: MoodEntry | null = recent[i];
      const c: TrendCell = new TrendCell();
      const isLast: boolean = i === recent.length - 1;
      c.isToday = isLast;
      if (e === null) {
        c.emoji = '·';
        c.color = '#EFEAF0';
        c.has = false;
        c.height = 14;
        c.label = isLast ? '今' : '';
      } else {
        const m: MoodKind = moodOf(e.moodId);
        c.emoji = m.emoji;
        c.color = m.hex;
        c.has = true;
        c.height = 44;
        c.label = isLast ? '今' : String(Number(e.date.substring(8, 10)));
      }
      cells.push(c);
    }
    this.trend = cells;

    const all: MoodEntry[] = this.repo.all();
    this.days = all.length;
    this.streak = this.repo.streak();

    const top: string = this.repo.mostMoodId();
    this.most = top.length > 0 ? moodOf(top).emoji : '—';

    this.insight = this.buildInsight(all.length);

    const rows: HistoryRow[] = [];
    for (let i = all.length - 1; i >= 0; i--) {
      const e: MoodEntry = all[i];
      const m: MoodKind = moodOf(e.moodId);
      const r: HistoryRow = new HistoryRow();
      r.emoji = m.emoji;
      r.color = m.hex;
      r.label = m.label;
      r.note = e.note;
      r.day = e.date.length >= 10 ? `${Number(e.date.substring(5, 7))}月${Number(e.date.substring(8, 10))}日` : e.date;
      rows.push(r);
    }
    this.rows = rows;
  }

  private buildInsight(total: number): string {
    if (total === 0) {
      return '还没有记录。心情不好说出口的时候，点一个表情就够了——不用解释。';
    }

    const recorded: number = this.repo.recordedIn(14);
    let text: string = `最近 14 天你记录了 ${recorded} 天。`;
    const heavy: number = this.repo.countOf('lonely') + this.repo.countOf('sad');

    if (heavy >= 3) {
      // 不评价、不诊断，只给两个能立刻做的动作
      text += '「想人」和「难过」出现得比较多。这不代表你不好，只是最近确实有点累。' +
        '要不要试试「呼吸放松」，或者打 12355 找个人说说？';
    } else if (recorded >= 5) {
      text += '能坚持记录自己的心情，是很厉害的一件事——很多人一辈子都做不到。';
    } else {
      text += '想到就来记一下，不用每天都记。';
    }
    return text;
  }

  build() {
    Stack() {
      Backdrop()

      Column({ space: 10 }) {
        PageHeader({
          title: '心情日记',
          subtitle: this.days > 0 ? `一共记了 ${this.days} 天` : '还没有记录',
          trailing: '',
          onBack: () => {
            router.back();
          }
        })

        Scroll() {
          Column({ space: 12 }) {
            this.trendCard()
            this.statsCard()
            this.historyCard()
          }
          .width('100%')
          .padding({ bottom: 12 })
        }
        .width('100%')
        .layoutWeight(1)
        .scrollBar(BarState.Off)
        .align(Alignment.Top)
      }
      .width('100%')
      .height('100%')
      .padding({ left: 20, right: 20, top: 20 + kiosk.safeTopVp, bottom: 12 + kiosk.safeBottomVp })
      .constraintSize({ maxWidth: 780 })
    }
    .width('100%')
    .height('100%')
    .alignContent(Alignment.Center)
  }

  @Builder
  trendCard() {
    Column({ space: 10 }) {
      Text('最近 14 天')
        .fontSize(14)
        .fontWeight(FontWeight.Bold)
        .fontColor('#5D3A47')
        .width('100%')

      Row({ space: 2 }) {
        ForEach(this.trend, (c: TrendCell, i: number) => {
          Column({ space: 3 }) {
            Text(c.emoji)
              .fontSize(13)
              .fontColor(c.has ? '#FFFFFF' : '#C9C3CE')

            Column()
              .width(12)
              .height(c.height)
              .borderRadius(5)
              .backgroundColor(c.color)

            Text(c.label)
              .fontSize(8.5)
              .fontColor(c.isToday ? '#E91E63' : '#C0B2BA')
          }
          .layoutWeight(1)
          .justifyContent(FlexAlign.End)
        }, (c: TrendCell, i: number) => `${i}-${c.label}`)
      }
      .width('100%')
      .alignItems(VerticalAlign.Bottom)
      // 柱子底部对齐：全空的那几天是矮灰条，一眼看得出「那天没记」
      .height(76)
    }
    .width('100%')
    .padding(14)
    .borderRadius(20)
    .backgroundColor('#FFFFFF')
    .alignItems(HorizontalAlign.Start)
  }

  @Builder
  statsCard() {
    Column({ space: 10 }) {
      Row({ space: 8 }) {
        this.statBox(String(this.days), '记了几天', '#FFEBCF')
        this.statBox(String(this.streak), '连续天数', '#E8F5E9')
        this.statBox(this.most, '最常出现', '#E3F2FD')
      }
      .width('100%')

      Text(this.insight)
        .fontSize(12.5)
        .lineHeight(20)
        .fontColor('#6D4C58')
        .width('100%')
        .padding(10)
        .borderRadius(12)
        .backgroundColor('#FBF4F7')
    }
    .width('100%')
    .padding(14)
    .borderRadius(20)
    .backgroundColor('#FFFFFF')
    .alignItems(HorizontalAlign.Start)
  }

  @Builder
  statBox(value: string, label: string, bg: string) {
    Column({ space: 3 }) {
      Text(value)
        .fontSize(20)
        .fontWeight(FontWeight.Bold)
        .fontColor('#4E3B45')

      Text(label)
        .fontSize(10.5)
        .fontColor('#A1858F')
    }
    .layoutWeight(1)
    .padding({ top: 10, bottom: 10 })
    .borderRadius(14)
    .backgroundColor(bg)
  }

  @Builder
  historyCard() {
    Column({ space: 8 }) {
      Text('全部记录')
        .fontSize(14)
        .fontWeight(FontWeight.Bold)
        .fontColor('#5D3A47')
        .width('100%')

      if (this.rows.length === 0) {
        Text('还没有记录')
          .fontSize(13)
          .fontColor('#B08A98')
          .width('100%')
          .textAlign(TextAlign.Center)
          .padding({ top: 14, bottom: 14 })
      } else {
        ForEach(this.rows, (r: HistoryRow, i: number) => {
          Row({ space: 10 }) {
            Stack() {
              Text(r.emoji).fontSize(18)
            }
            .width(36)
            .height(36)
            .borderRadius(18)
            .backgroundColor(r.color)
            .alignContent(Alignment.Center)

            Column({ space: 2 }) {
              Text(r.label)
                .fontSize(14)
                .fontWeight(FontWeight.Bold)
                .fontColor('#5D3A47')

              if (r.note.length > 0) {
                Text(r.note)
                  .fontSize(11.5)
                  .fontColor('#8A7B84')
                  .maxLines(2)
                  .textOverflow({ overflow: TextOverflow.Ellipsis })
              }
            }
            .alignItems(HorizontalAlign.Start)
            .layoutWeight(1)

            Text(r.day)
              .fontSize(11)
              .fontColor('#C0B2BA')
          }
          .width('100%')
          .padding({ left: 10, right: 10, top: 8, bottom: 8 })
          .borderRadius(14)
          .backgroundColor('#FDF9FB')
        }, (r: HistoryRow, i: number) => `${i}-${r.day}`)
      }
    }
    .width('100%')
    .padding(14)
    .borderRadius(20)
    .backgroundColor('#FFFFFF')
    .alignItems(HorizontalAlign.Start)
  }
}
