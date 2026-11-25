import { Backdrop } from '../components/Backdrop';
import { PageHeader } from '../components/PageHeader';
import { kiosk } from '../core/Kiosk';
import { rhymeLearned } from '../core/LearnProgress';
import { rewards } from '../core/Rewards';
import { router } from '../core/Router';
import { RhymeItem, RhymeTopic, RHYME_TOPICS } from '../data/RhymeData';

/**
 * 口诀详情：把主题下的几节内容按形态渲染出来。
 *
 * 三种形态对应 C# 的 Kind：verse 念一段、list 逐条、grid 查表。
 * 表格宽 9 列，手机横着放不下，所以包一层横向滚动——
 * 不缩字号硬塞，挤成一团孩子根本看不清乘到几了。
 */
@ComponentV2
export struct RhymeDetailPage {
  private topic: RhymeTopic = RHYME_TOPICS[0];
  @Local learned: boolean = false;

  aboutToAppear(): void {
    const want: number = router.rhymeIndex;
    if (want >= 0 && want < RHYME_TOPICS.length) {
      this.topic = RHYME_TOPICS[want];
    }
    this.learned = rhymeLearned.has(this.topic.title);
  }

  private markLearned(): void {
    const first: boolean = rhymeLearned.mark(this.topic.title);
    this.learned = true;
    if (first) {
      rewards.grant('rhyme', 3, rhymeLearned.count(), this.topic.title);
      this.getUIContext().getPromptAction().showToast({
        message: `念会《${this.topic.title}》啦！⭐ +3`,
        duration: 1800
      });
    } else {
      this.getUIContext().getPromptAction().showToast({ message: '这个已经念会啦', duration: 1400 });
    }
  }

  build() {
    Stack() {
      Backdrop()

      Column({ space: 12 }) {
        PageHeader({
          title: this.topic.title,
          subtitle: `${this.topic.emoji} 口诀乐园`,
          onBack: () => {
            router.back();
          }
        })

        Scroll() {
          Column({ space: 14 }) {
            ForEach(this.topic.items, (item: RhymeItem, i: number) => {
              Column({ space: 8 }) {
                if (item.title.length > 0) {
                  Text(item.title)
                    .fontSize(18)
                    .fontWeight(FontWeight.Bold)
                    .fontColor(this.topic.color)
                    .width('100%')
                    .margin({ top: 4 })
                }

                if (item.kind === 'verse') {
                  this.verseCard(item.verse)
                } else if (item.kind === 'list') {
                  this.listCards(item.lines)
                } else if (item.kind === 'phrases') {
                  this.phraseRows(item.lines)
                } else {
                  this.gridTable(item.grid)
                }
              }
              .alignItems(HorizontalAlign.Start)
              .width('100%')
            }, (item: RhymeItem, i: number) => `${i}-${item.kind}`)

            Text('念一遍给爸爸妈妈听，他们一定会夸你～')
              .fontSize(13)
              .fontColor('#A1887F')
              .width('100%')
              .textAlign(TextAlign.Center)
              .margin({ top: 4, bottom: 6 })

            Row({ space: 8 }) {
              Text(this.learned ? '✅' : '📚').fontSize(18)
              Text(this.learned ? '已经念会啦' : '我念会了')
                .fontSize(16)
                .fontWeight(FontWeight.Medium)
                .fontColor(this.learned ? '#8E24AA' : '#FFFFFF')
            }
            .justifyContent(FlexAlign.Center)
            .width('100%')
            .height(50)
            .borderRadius(18)
            .backgroundColor(this.learned ? '#F3E5F5' : '#8E24AA')
            .margin({ bottom: 10 })
            .onClick(() => {
              this.markLearned();
            })
          }
          .width('100%')
          .padding({ bottom: 8 })
        }
        .width('100%')
        .layoutWeight(1)
        .scrollBar(BarState.Off)
        .align(Alignment.Top)
      }
      .width('100%')
      .height('100%')
      .padding({ left: 28, right: 28, top: 26 + kiosk.safeTopVp, bottom: 12 + kiosk.safeBottomVp })
      .constraintSize({ maxWidth: 900 })
    }
    .width('100%')
    .height('100%')
    .alignContent(Alignment.Center)
  }

  /** 念一段：行距放大，换行原样保留（凑十歌就是靠换行分句的）。 */
  @Builder
  verseCard(text: string) {
    Column() {
      Text(text)
        .fontSize(18)
        .lineHeight(32)
        .fontColor('#37474F')
        .width('100%')
    }
    .width('100%')
    .padding({ left: 18, right: 18, top: 16, bottom: 16 })
    .borderRadius(18)
    .backgroundColor('#FFFFFF')
    .border({ width: 1, color: '#E8DED2' })
  }

  /** 逐条小贴士：一条一张卡，条与条之间留白，读起来不连成一片。 */
  @Builder
  listCards(lines: string[]) {
    Column({ space: 8 }) {
      ForEach(lines, (line: string) => {
        Row({ space: 8 }) {
          Text('•').fontSize(16).fontColor(this.topic.color)
          Text(line)
            .fontSize(16)
            .lineHeight(26)
            .fontColor('#37474F')
            .layoutWeight(1)
        }
        .width('100%')
        .alignItems(VerticalAlign.Top)
        .padding({ left: 16, right: 16, top: 12, bottom: 12 })
        .borderRadius(16)
        .backgroundColor('#FFFFFF')
      }, (line: string, i: number) => `${i}-${line}`)
    }
    .width('100%')
  }

  /**
   * 查表：9 列塞不进 360vp 的手机屏。
   *
   * 之前用固定 50vp 单元格 + 横向滚动，结果整块内容把页面撑宽、
   * 右侧被屏幕裁掉（连上面的说明卡都跟着溢出）。改成拆成两张表：
   * 每个单元格 layoutWeight(1) 平分宽度，永远贴合屏宽，不需要滚动。
   */
  @Builder
  gridTable(grid: string[][]) {
    Column({ space: 8 }) {
      this.gridBlock(grid, 0, 5, '×1 – ×5')
      this.gridBlock(grid, 5, 9, '×6 – ×9')
    }
    .width('100%')
  }

  @Builder
  gridBlock(grid: string[][], from: number, to: number, label: string) {
    Column({ space: 3 }) {
      Text(label)
        .fontSize(11)
        .fontColor('#9B8574')
        .width('100%')
        .padding({ left: 2 })

      ForEach(grid, (row: string[], r: number) => {
        Row({ space: 3 }) {
          ForEach(row.slice(from, to), (cell: string) => {
            Text(cell)
              .fontSize(11)
              .height(24)
              .layoutWeight(1)
              .textAlign(TextAlign.Center)
              .fontColor('#37474F')
              .backgroundColor('#FFFFFF')
              .borderRadius(5)
          }, (cell: string, c: number) => `${from}-${c}-${cell}`)
        }
        .width('100%')
      }, (row: string[], r: number) => `row-${from}-${r}`)
    }
    .width('100%')
    .padding(8)
    .borderRadius(14)
    .backgroundColor('#F7F1E8')
  }

  /**
   * 口诀串：一行一条，行内短语按屏宽自动折行。
   *
   * 小九九第 9 行有 9 个短语，横排必然超出屏幕；用 Flex 换行后
   * 短语整块换到下一行，不会出现「四一得四 四二得八 四三十二 四 / 四十六」这种断词。
   */
  @Builder
  phraseRows(lines: string[]) {
    Column({ space: 4 }) {
      ForEach(lines, (line: string) => {
        Flex({ wrap: FlexWrap.Wrap }) {
          ForEach(this.splitPhrases(line), (phrase: string, i: number) => {
            Text(phrase)
              .fontSize(15)
              .fontColor('#37474F')
              .padding({ left: 4, right: 8, top: 3, bottom: 3 })
          }, (phrase: string, i: number) => `${i}-${phrase}`)
        }
        .width('100%')
      }, (line: string, r: number) => `p-${r}`)
    }
    .width('100%')
    .padding({ left: 10, right: 10, top: 10, bottom: 10 })
    .borderRadius(16)
    .backgroundColor('#FFFFFF')
  }

  /** 短语之间用两个空格分隔（见 data/RhymeData.ets 的 phrasesItem）。 */
  private splitPhrases(line: string): string[] {
    return line.split('  ').filter((s: string) => s.length > 0);
  }
}
