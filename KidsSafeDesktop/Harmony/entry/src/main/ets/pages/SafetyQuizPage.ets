import { Backdrop } from '../components/Backdrop';
import { PageHeader } from '../components/PageHeader';
import { kiosk } from '../core/Kiosk';
import { rewards } from '../core/Rewards';
import { router } from '../core/Router';
import { SAFETY_QUIZ, SafetyQuizQuestion } from '../data/SafetyQuiz';

/**
 * 安全小问答，对应 C# 的 SafetyQuizGamePage。
 *
 * 和「安全知识」的关系：那边是读的贴士，这边是答的题，分类名一致 ——
 * 孩子读完「防骗防拐」再来做这一轮的题，能对上号。
 *
 * 每题只有一次机会（答错也照样给讲解），一轮 8 题。
 * 答完立刻给解析，比对错本身重要：错题不解释，孩子只会记住「这题我错了」。
 */

const ROUND: number = 8;

/** 一轮里的一道题：题干 + 打乱后的选项 + 正确项在这一轮里的位置。 */
class RoundItem {
  q: SafetyQuizQuestion = SAFETY_QUIZ[0];
  options: string[] = [];
  /** 正确选项在 options 里的下标（已按打乱后的顺序重算） */
  correctAt: number = 0;
}

@ComponentV2
export struct SafetyQuizPage {
  @Local index: number = 0;
  @Local total: number = ROUND;
  @Local correct: number = 0;
  @Local finished: boolean = false;
  @Local gainStars: number = 0;
  /** -1 = 还没作答；否则是选中的选项下标 */
  @Local chosen: number = -1;
  @Local emoji: string = '🛡️';
  @Local question: string = '';
  @Local category: string = '';
  @Local explain: string = '';
  @Local options: string[] = [];
  @Local correctAt: number = 0;

  private deck: RoundItem[] = [];

  aboutToAppear(): void {
    this.begin();
  }

  private begin(): void {
    // 从 40 道题里随机抽 8 道，再把每题的选项打乱 ——
    // 不打乱的话，孩子会记住「第三题选 B」，而不是真的理解。
    const pool: SafetyQuizQuestion[] = [];
    for (let i = 0; i < SAFETY_QUIZ.length; i++) {
      pool.push(SAFETY_QUIZ[i]);
    }
    for (let i = 0; i < pool.length; i++) {
      const j: number = i + Math.floor(Math.random() * (pool.length - i));
      const t: SafetyQuizQuestion = pool[i];
      pool[i] = pool[j];
      pool[j] = t;
    }

    const want: number = pool.length < ROUND ? pool.length : ROUND;
    this.deck = [];
    for (let i = 0; i < want; i++) {
      const src: SafetyQuizQuestion = pool[i];

      // 洗选项，同时记住原来的正确项跑到了第几位
      const order: number[] = [];
      for (let k = 0; k < src.options.length; k++) {
        order.push(k);
      }
      for (let k = 0; k < order.length; k++) {
        const j: number = k + Math.floor(Math.random() * (order.length - k));
        const t: number = order[k];
        order[k] = order[j];
        order[j] = t;
      }

      const item = new RoundItem();
      item.q = src;
      item.options = [];
      item.correctAt = 0;
      for (let k = 0; k < order.length; k++) {
        item.options.push(src.options[order[k]]);
        if (order[k] === src.correct) {
          item.correctAt = k;
        }
      }
      this.deck.push(item);
    }

    this.total = this.deck.length;
    this.index = 0;
    this.correct = 0;
    this.finished = false;
    this.gainStars = 0;
    this.loadRound();
  }

  private loadRound(): void {
    const item: RoundItem = this.deck[this.index];
    this.emoji = item.q.emoji;
    this.question = item.q.question;
    this.category = item.q.category;
    this.options = item.options;
    this.correctAt = item.correctAt;
    this.explain = item.q.explain;
    this.chosen = -1;
  }

  private pick(i: number): void {
    // 只能答一次：再点别的选项不算数，避免「挨个试」
    if (this.chosen >= 0) {
      return;
    }
    this.chosen = i;
    if (i === this.correctAt) {
      this.correct = this.correct + 1;
    }
  }

  private next(): void {
    if (this.index + 1 >= this.total) {
      this.finish();
      return;
    }
    this.index = this.index + 1;
    this.loadRound();
  }

  private finish(): void {
    this.finished = true;
    const rate: number = this.total === 0 ? 0 : this.correct / this.total;
    let want: number = 1;
    if (this.correct === this.total) {
      want = 5;
    } else if (rate >= 0.75) {
      want = 4;
    } else if (rate >= 0.5) {
      want = 2;
    }
    this.gainStars = rewards.grant('safequiz', want, this.correct, `答对 ${this.correct} 题`);
  }

  build() {
    Stack() {
      Backdrop()

      Column({ space: 12 }) {
        PageHeader({
          title: '安全小问答',
          subtitle: this.finished ? '成绩单' : `第 ${this.index + 1} / ${this.total} 题`,
          trailing: this.finished ? '' : `${this.category}`,
          onBack: () => {
            router.back();
          }
        })

        if (this.finished) {
          this.resultArea()
        } else {
          this.quizArea()
        }
      }
      .width('100%')
      .height('100%')
      .padding({ left: 24, right: 24, top: 26 + kiosk.safeTopVp, bottom: 16 + kiosk.safeBottomVp })
      .constraintSize({ maxWidth: 780 })
    }
    .width('100%')
    .height('100%')
    .alignContent(Alignment.Center)
  }

  @Builder
  quizArea() {
    Column({ space: 12 }) {
      Scroll() {
        Column({ space: 12 }) {
          Column({ space: 10 }) {
            Text(this.emoji).fontSize(40)

            Text(this.question)
              .fontSize(18)
              .lineHeight(28)
              .fontColor('#263238')
              .width('100%')
              .textAlign(TextAlign.Start)
          }
          .width('100%')
          .padding(18)
          .borderRadius(22)
          .backgroundColor('#FFFFFF')
          .alignItems(HorizontalAlign.Center)

          ForEach(this.options, (opt: string, i: number) => {
            this.optionRow(opt, i)
          }, (opt: string, i: number) => `${this.index}-${i}`)

          if (this.chosen >= 0) {
            Column({ space: 6 }) {
              Text(this.chosen === this.correctAt ? '🌟 答对啦！' : '💡 正确答案是：')
                .fontSize(16)
                .fontWeight(FontWeight.Bold)
                .fontColor(this.chosen === this.correctAt ? '#2E7D32' : '#C62828')

              if (this.chosen !== this.correctAt) {
                Text(this.options[this.correctAt])
                  .fontSize(15)
                  .fontColor('#2E7D32')
                  .width('100%')
              }

              Text(this.explain)
                .fontSize(14)
                .lineHeight(22)
                .fontColor('#5D4037')
                .width('100%')
            }
            .width('100%')
            .alignItems(HorizontalAlign.Start)
            .padding(14)
            .borderRadius(18)
            .backgroundColor(this.chosen === this.correctAt ? '#E8F5E9' : '#FFF3E0')
          }
        }
        .width('100%')
        .padding({ bottom: 8 })
      }
      .width('100%')
      .layoutWeight(1)
      .scrollBar(BarState.Off)
      .align(Alignment.Top)

      if (this.chosen >= 0) {
        Text(this.index + 1 >= this.total ? '看成绩 📊' : '下一题 ➡️')
          .fontSize(18)
          .fontWeight(FontWeight.Bold)
          .fontColor('#FFFFFF')
          .textAlign(TextAlign.Center)
          .width('100%')
          .height(54)
          .borderRadius(18)
          .backgroundColor('#E53935')
          .onClick(() => {
            this.next();
          })
      }
    }
    .width('100%')
    .layoutWeight(1)
  }

  @Builder
  optionRow(opt: string, i: number) {
    Row({ space: 10 }) {
      Text(String.fromCharCode(65 + i))
        .fontSize(13)
        .fontColor('#FFFFFF')
        .textAlign(TextAlign.Center)
        .width(24)
        .height(24)
        .borderRadius(12)
        .backgroundColor(this.markColor(i))

      Text(opt)
        .fontSize(15)
        .fontColor('#37474F')
        .layoutWeight(1)

      if (this.chosen >= 0 && i === this.correctAt) {
        Text('✅').fontSize(15)
      } else if (this.chosen === i && i !== this.correctAt) {
        Text('❌').fontSize(15)
      }
    }
    .width('100%')
    .padding({ left: 14, right: 14, top: 13, bottom: 13 })
    .borderRadius(16)
    .backgroundColor('#FFFFFF')
    .border({ width: this.chosen === i ? 2 : 0, color: this.markColor(i) })
    .onClick(() => {
      this.pick(i);
    })
  }

  /** 答题后：正确项永远标绿，选错的那项标红，其余不变。 */
  private markColor(i: number): string {
    if (this.chosen < 0) {
      return '#B0BEC5';
    }
    if (i === this.correctAt) {
      return '#2E7D32';
    }
    if (i === this.chosen) {
      return '#C62828';
    }
    return '#B0BEC5';
  }

  @Builder
  resultArea() {
    Column({ space: 14 }) {
      Column({ space: 6 }) {
        Text(this.correct === this.total ? '🏆' : (this.correct * 100 / this.total >= 60 ? '🌟' : '💪'))
          .fontSize(48)

        Text(this.correct === this.total
          ? '全部答对！安全意识真棒！'
          : `答对 ${this.correct} / ${this.total} 题`)
          .fontSize(19)
          .fontWeight(FontWeight.Bold)
          .fontColor('#3E2723')
          .textAlign(TextAlign.Center)

        Text(this.correct * 100 / this.total >= 75
          ? '你已经知道怎么保护自己了，遇到事情要记得用上。'
          : '错的那几题回家讲给爸爸妈妈听，就记住啦。')
          .fontSize(14)
          .fontColor('#8D6E63')
          .textAlign(TextAlign.Center)

        if (this.gainStars > 0) {
          Text(`⭐ +${this.gainStars}`)
            .fontSize(22)
            .fontWeight(FontWeight.Bold)
            .fontColor('#EF6C00')
            .padding({ left: 16, right: 16, top: 4, bottom: 4 })
            .borderRadius(14)
            .backgroundColor('#FFEBCF')
        } else {
          Text('今天安全问答的星星已经拿满啦，成绩照样记在排行榜上')
            .fontSize(12)
            .fontColor('#B0BEC5')
            .textAlign(TextAlign.Center)
        }
      }
      .width('100%')
      .padding(20)
      .borderRadius(22)
      .backgroundColor('#FFFFFF')

      Text('再答一轮')
        .fontSize(17)
        .fontWeight(FontWeight.Bold)
        .fontColor('#FFFFFF')
        .textAlign(TextAlign.Center)
        .width('100%')
        .height(54)
        .borderRadius(18)
        .backgroundColor('#E53935')
        .onClick(() => {
          this.begin();
        })

      Text('返回')
        .fontSize(16)
        .fontColor('#546E7A')
        .textAlign(TextAlign.Center)
        .width('100%')
        .height(50)
        .borderRadius(16)
        .backgroundColor('#ECEFF1')
        .onClick(() => {
          router.back();
        })
    }
    .width('100%')
    .layoutWeight(1)
    .justifyContent(FlexAlign.Center)
  }
}
