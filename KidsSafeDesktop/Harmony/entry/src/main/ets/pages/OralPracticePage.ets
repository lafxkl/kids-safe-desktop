import { Backdrop } from '../components/Backdrop';
import { PageHeader } from '../components/PageHeader';
import { kiosk } from '../core/Kiosk';
import { generateOral, gradeTitle, OralProblem, OralTopic, ROUND_COUNT, spokenOf } from '../core/OralGen';
import { rewards } from '../core/Rewards';
import { router } from '../core/Router';
import { speech } from '../core/Speech';

/**
 * 口算练习，对应 C# 的 OralPracticePage。
 *
 * 交互刻意做成「一题一屏、答完立刻判」：
 *   - 一次只出一道题，屏幕上没有第二件事可分心；
 *   - 答对立刻锁住，必须点「下一题」才继续，避免连点把答案带进下一题；
 *   - 答错当场给正确答案，不让孩子猜第二遍 —— 猜对会记住错的。
 *
 * 星星规则：全对 5 颗、八成以上 4 颗、及格 2 颗、其余 1 颗。
 * 意思是「来做就有收获」，但全对才值得多给。
 */

/** 题型筛选按钮。空串表示不限。 */
class TopicChip {
  label: string = '全部';
  value: string = OralTopic.All;

  constructor(label: string, value: string) {
    this.label = label;
    this.value = value;
  }
}

const TOPICS: TopicChip[] = [
  new TopicChip('全部', OralTopic.All),
  new TopicChip('加法', OralTopic.Add),
  new TopicChip('减法', OralTopic.Sub),
  new TopicChip('乘法', OralTopic.Mul),
  new TopicChip('除法', OralTopic.Div),
  new TopicChip('混合', OralTopic.Mix)
];

@ComponentV2
export struct OralPracticePage {
  @Local grade: number = 1;
  @Local topic: string = OralTopic.All;
  /** play = 答题中，done = 成绩单 */
  @Local phase: string = 'play';
  @Local index: number = 0;
  @Local correct: number = 0;
  @Local total: number = ROUND_COUNT;
  @Local input: string = '';
  /** '' = 还没答，'ok' = 答对，'bad' = 答错 */
  @Local state: string = '';
  @Local gainStars: number = 0;
  @Local elapsed: number = 0;
  @Local expression: string = '';
  @Local category: string = '';

  private deck: OralProblem[] = [];
  private wrongs: string[] = [];
  private timer: number = -1;

  aboutToAppear(): void {
    const g: number = router.learnGrade;
    this.grade = g >= 1 && g <= 9 ? g : 1;
    this.begin();
  }

  aboutToDisappear(): void {
    this.stopTimer();
  }

  private begin(): void {
    this.stopTimer();
    this.deck = generateOral(this.grade, this.topic, ROUND_COUNT);
    this.total = this.deck.length;
    this.index = 0;
    this.correct = 0;
    this.gainStars = 0;
    this.wrongs = [];
    this.phase = 'play';
    this.elapsed = 0;
    this.loadProblem();
    this.startTimer();
  }

  private loadProblem(): void {
    const p: OralProblem = this.deck[this.index];
    this.expression = p.expression;
    this.category = p.category;
    this.input = '';
    this.state = '';
  }

  private startTimer(): void {
    this.timer = setInterval(() => {
      this.elapsed = this.elapsed + 1;
    }, 1000);
  }

  private stopTimer(): void {
    if (this.timer >= 0) {
      clearInterval(this.timer);
      this.timer = -1;
    }
  }

  private current(): OralProblem {
    return this.deck[this.index];
  }

  private toast(msg: string): void {
    this.getUIContext().getPromptAction().showToast({ message: msg, duration: 1600 });
  }

  /** 提交答案。已经答过就直接忽略，防连点。 */
  private submit(): void {
    if (this.state.length > 0) {
      return;
    }
    const raw: string = this.input.trim().replace('−', '-').replace('。', '.');
    if (raw.length === 0) {
      this.toast('先写一个数字吧～');
      return;
    }
    const val: number = Number(raw);
    if (isNaN(val)) {
      this.toast('只能填数字哦');
      return;
    }

    const p: OralProblem = this.current();
    // 口算题的答案最多两位小数，1e-4 的容差足以吸收 0.1+0.2 这类浮点误差
    if (Math.abs(val - p.answer) < 0.0001) {
      this.state = 'ok';
      this.correct = this.correct + 1;
      speech.speak('真棒');
    } else {
      this.state = 'bad';
      // 错题记录留到结束页，孩子回头能看见自己错在哪
      this.wrongs.push(`${p.expression}　你写 ${raw}，正确 ${p.answerText()}`);
      speech.speak(`不对哦，答案是 ${p.answerText()}`);
    }
  }

  private next(): void {
    // 没答对就点下一题：按「未作答」记进错题，不能悄悄跳过
    if (this.state === '') {
      const p: OralProblem = this.current();
      this.wrongs.push(`${p.expression}　没有作答，正确 ${p.answerText()}`);
    }
    this.index = this.index + 1;
    if (this.index >= this.deck.length) {
      this.finish();
      return;
    }
    this.loadProblem();
  }

  private finish(): void {
    this.stopTimer();
    this.phase = 'done';

    const rate: number = this.total === 0 ? 0 : this.correct / this.total;
    let want: number = 1;
    if (this.correct === this.total) {
      want = 5;
    } else if (rate >= 0.8) {
      want = 4;
    } else if (rate >= 0.6) {
      want = 2;
    }
    this.gainStars = rewards.grant('oral', want, this.correct, `${gradeTitle(this.grade)}答对 ${this.correct} 题`);
    if (this.correct === this.total) {
      speech.speak('全对啦，你是口算小状元！');
    }
  }

  private changeTopic(v: string): void {
    if (this.topic === v) {
      return;
    }
    this.topic = v;
    this.toast('换了题型，重新开始一轮');
    this.begin();
  }

  /** 数字键盘不一定带负号（初一有理数要负数），单独给一个 ± 按钮。 */
  private toggleSign(): void {
    if (this.state.length > 0) {
      return;
    }
    const v: string = this.input.trim();
    this.input = v.startsWith('-') ? v.substring(1) : `-${v}`;
  }

  build() {
    Stack() {
      Backdrop()

      Column({ space: 12 }) {
        PageHeader({
          title: `${gradeTitle(this.grade)} · 口算练习`,
          subtitle: this.phase === 'play'
            ? `第 ${this.index + 1} / ${this.total} 题 · ${this.category}`
            : '成绩单',
          trailing: this.phase === 'play' ? `⏱ ${this.clock()}` : '',
          onBack: () => {
            router.back();
          }
        })

        if (this.phase === 'play') {
          this.topicRow()
          this.questionCard()
          this.answerArea()
        } else {
          this.resultArea()
        }
      }
      .width('100%')
      .height('100%')
      .padding({ left: 24, right: 24, top: 26 + kiosk.safeTopVp, bottom: 16 + kiosk.safeBottomVp })
      .constraintSize({ maxWidth: 760 })
    }
    .width('100%')
    .height('100%')
    .alignContent(Alignment.Center)
  }

  private clock(): string {
    const m: number = Math.floor(this.elapsed / 60);
    const s: number = this.elapsed % 60;
    return `${m < 10 ? '0' : ''}${m}:${s < 10 ? '0' : ''}${s}`;
  }

  @Builder
  topicRow() {
    Row({ space: 8 }) {
      ForEach(TOPICS, (t: TopicChip) => {
        Text(t.label)
          .fontSize(13)
          .fontColor(this.topic === t.value ? '#FFFFFF' : '#8D6E63')
          .textAlign(TextAlign.Center)
          .height(32)
          .padding({ left: 12, right: 12 })
          .borderRadius(16)
          .backgroundColor(this.topic === t.value ? '#0288D1' : '#FFFFFF')
          .onClick(() => {
            this.changeTopic(t.value);
          })
      }, (t: TopicChip) => t.value)
    }
    .width('100%')
    .justifyContent(FlexAlign.Center)
  }

  @Builder
  questionCard() {
    Column({ space: 10 }) {
      Row({ space: 8 }) {
        Text(this.category)
          .fontSize(12)
          .fontColor('#8D6E63')
          .padding({ left: 10, right: 10, top: 3, bottom: 3 })
          .borderRadius(10)
          .backgroundColor('#FFF3E0')

        Blank()

        // 一二年级认字不全，题目要能听
        Text('🔊 读题')
          .fontSize(13)
          .fontColor('#0288D1')
          .padding({ left: 10, right: 10, top: 3, bottom: 3 })
          .borderRadius(10)
          .backgroundColor('#E1F5FE')
          .onClick(() => {
            speech.speak(spokenOf(this.expression));
          })
      }
      .width('100%')

      Text(this.expression)
        .fontSize(40)
        .fontWeight(FontWeight.Bold)
        .fontColor('#37474F')
        .width('100%')
        .textAlign(TextAlign.Center)
        .margin({ top: 6, bottom: 6 })
    }
    .width('100%')
    .padding(18)
    .borderRadius(22)
    .backgroundColor('#FFFFFF')
  }

  @Builder
  answerArea() {
    Column({ space: 12 }) {
      Row({ space: 10 }) {
        TextInput({ text: this.input, placeholder: '在这里写答案' })
          .type(InputType.NUMBER_DECIMAL)
          .fontSize(26)
          .fontWeight(FontWeight.Bold)
          .textAlign(TextAlign.Center)
          .height(60)
          .layoutWeight(1)
          .borderRadius(16)
          .backgroundColor('#FFFFFF')
          .enabled(this.state.length === 0)
          .onChange((v: string) => {
            this.input = v;
          })
          .onSubmit(() => {
            this.submit();
          })

        Text('±')
          .fontSize(22)
          .fontColor('#0288D1')
          .textAlign(TextAlign.Center)
          .width(52)
          .height(60)
          .borderRadius(16)
          .backgroundColor('#E1F5FE')
          .opacity(this.state.length === 0 ? 1 : 0.4)
          .onClick(() => {
            this.toggleSign();
          })
      }
      .width('100%')

      if (this.state === 'ok') {
        Row({ space: 8 }) {
          Text('🌟').fontSize(20)
          Text('答对啦！').fontSize(20).fontWeight(FontWeight.Bold).fontColor('#2E7D32')
        }
        .width('100%')
        .justifyContent(FlexAlign.Center)
        .padding(10)
        .borderRadius(16)
        .backgroundColor('#E8F5E9')
      }

      if (this.state === 'bad') {
        Column({ space: 4 }) {
          Text('再想想～ 正确答案是').fontSize(14).fontColor('#C62828')
          Text(this.current().answerText())
            .fontSize(26)
            .fontWeight(FontWeight.Bold)
            .fontColor('#C62828')
        }
        .width('100%')
        .padding(12)
        .borderRadius(16)
        .backgroundColor('#FFEBEE')
      }

      if (this.state === '') {
        Text('确 定')
          .fontSize(19)
          .fontWeight(FontWeight.Bold)
          .fontColor('#FFFFFF')
          .textAlign(TextAlign.Center)
          .width('100%')
          .height(56)
          .borderRadius(18)
          .backgroundColor('#0288D1')
          .onClick(() => {
            this.submit();
          })
      } else {
        Text(this.index + 1 >= this.total ? '看成绩 📊' : '下一题 ➡️')
          .fontSize(19)
          .fontWeight(FontWeight.Bold)
          .fontColor('#FFFFFF')
          .textAlign(TextAlign.Center)
          .width('100%')
          .height(56)
          .borderRadius(18)
          .backgroundColor(this.state === 'ok' ? '#2E7D32' : '#EF6C00')
          .onClick(() => {
            this.next();
          })
      }

      Blank()

      Text(`⭐ 本轮已答对 ${this.correct} 题`)
        .fontSize(13)
        .fontColor('#9B8574')
    }
    .width('100%')
    .layoutWeight(1)
    .alignItems(HorizontalAlign.Center)
  }

  @Builder
  resultArea() {
    Scroll() {
      Column({ space: 14 }) {
        Column({ space: 6 }) {
          Text(this.correct === this.total ? '🏆' : (this.correct * 100 / this.total >= 60 ? '🌟' : '💪'))
            .fontSize(48)

          Text(this.correct === this.total
            ? '全对啦！你是口算小状元！'
            : (this.correct * 100 / this.total >= 80 ? '很棒！再练练就能全对！' : '不错哦，多练习会更厉害！'))
            .fontSize(18)
            .fontWeight(FontWeight.Bold)
            .fontColor('#3E2723')
            .textAlign(TextAlign.Center)

          Text(`共 ${this.total} 题，答对 ${this.correct} 题 · 用时 ${this.elapsed} 秒`)
            .fontSize(14)
            .fontColor('#8D6E63')

          if (this.gainStars > 0) {
            Text(`⭐ +${this.gainStars}`)
              .fontSize(22)
              .fontWeight(FontWeight.Bold)
              .fontColor('#EF6C00')
              .padding({ left: 16, right: 16, top: 4, bottom: 4 })
              .borderRadius(14)
              .backgroundColor('#FFEBCF')
          } else {
            Text('今天口算的星星已经拿满啦，成绩照样记在排行榜上')
              .fontSize(12)
              .fontColor('#B0BEC5')
              .textAlign(TextAlign.Center)
          }
        }
        .width('100%')
        .padding(18)
        .borderRadius(22)
        .backgroundColor('#FFFFFF')

        if (this.wrongs.length > 0) {
          Column({ space: 8 }) {
            Text(`错题回顾（${this.wrongs.length} 道）`)
              .fontSize(15)
              .fontWeight(FontWeight.Bold)
              .fontColor('#C62828')
              .width('100%')

            ForEach(this.wrongs, (line: string, i: number) => {
              Text(line)
                .fontSize(14)
                .fontColor('#5D4037')
                .width('100%')
                .padding(10)
                .borderRadius(12)
                .backgroundColor('#FFEBEE')
            }, (line: string, i: number) => `${i}-${line}`)
          }
          .width('100%')
        }

        Row({ space: 10 }) {
          Text('再练一轮')
            .fontSize(17)
            .fontWeight(FontWeight.Bold)
            .fontColor('#FFFFFF')
            .textAlign(TextAlign.Center)
            .layoutWeight(1)
            .height(54)
            .borderRadius(18)
            .backgroundColor('#0288D1')
            .onClick(() => {
              this.begin();
            })

          Text('换题型 / 年级')
            .fontSize(17)
            .fontColor('#0288D1')
            .textAlign(TextAlign.Center)
            .layoutWeight(1)
            .height(54)
            .borderRadius(18)
            .backgroundColor('#E1F5FE')
            .onClick(() => {
              router.back();
            })
        }
        .width('100%')
      }
      .width('100%')
      .padding({ bottom: 12 })
    }
    .width('100%')
    .layoutWeight(1)
    .scrollBar(BarState.Off)
    .align(Alignment.Top)
  }
}
