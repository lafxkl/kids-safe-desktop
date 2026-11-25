import { Backdrop } from '../components/Backdrop';
import { PageHeader } from '../components/PageHeader';
import { isCorrect, scoreOf } from '../core/ExamGrade';
import { kiosk } from '../core/Kiosk';
import { gradeTitle } from '../core/OralGen';
import { rewards } from '../core/Rewards';
import { router } from '../core/Router';
import { EXAM_PAPERS, ExamPaper, ExamQuestion, kindEmoji, kindName, papersOf, QKind, subjectColor, subjectEmoji } from '../data/ExamData';

/**
 * 模拟考试答题页，对应 C# 的 ExamPage。
 *
 * 和口算练习刻意相反：这里**答一题不报对错**，可以前后翻、可以改答案，
 * 最后统一交卷批改。考试场景要保留检查的余地，边做边判会让孩子
 * 靠反馈试答案，而不是真的算。
 *
 * 三种题型共用一套翻页：单选 / 判断给按钮，填空给输入框。
 * 判断题的「对 / 错」不建在库里，由界面生成 —— 库里只存答案，
 * 这样加一种题型不用改数据。
 */

/** 交卷后的一条回顾。 */
class ReviewItem {
  no: number = 0;
  kind: string = '';
  stem: string = '';
  your: string = '';
  correct: string = '';
  explain: string = '';
  ok: boolean = false;
}

@ComponentV2
export struct ExamPage {
  @Local grade: number = 3;
  @Local title: string = '';
  @Local subject: string = 'chinese';
  @Local index: number = 0;
  @Local total: number = 0;
  /** 当前题的答案键：选择题 A~D，判断题「对」「错」，填空题原文 */
  @Local selKey: string = '';
  @Local answered: number = 0;
  @Local finished: boolean = false;
  @Local score: number = 0;
  @Local correctCount: number = 0;
  @Local elapsed: number = 0;
  @Local gainStars: number = 0;
  /** 漏答过 → 再点一次交卷才真的交 */
  @Local warn: boolean = false;
  @Local review: ReviewItem[] = [];

  private questions: ExamQuestion[] = [];
  private answers: string[] = [];
  private timer: number = -1;

  aboutToAppear(): void {
    // 兜底用三年级第一卷：万一 examPaperId 是脏数据（比如换了题库版本），
    // 也不能让孩子看到一张空白试卷。
    const fallback: ExamPaper[] = papersOf(3);
    let paper: ExamPaper = new ExamPaper();
    let found: boolean = false;
    for (let i = 0; i < EXAM_PAPERS.length; i++) {
      if (EXAM_PAPERS[i].id === router.examPaperId) {
        paper = EXAM_PAPERS[i];
        found = true;
        break;
      }
    }
    if (!found && fallback.length > 0) {
      paper = fallback[0];
      found = true;
    }
    if (!found) {
      // 题库读不出来时宁可空着返回，也不要在答题页上崩掉
      router.back();
      return;
    }
    this.grade = paper.grade;
    this.title = paper.title;
    this.subject = paper.subject;
    this.questions = paper.questions;
    this.total = this.questions.length;

    this.answers = [];
    for (let i = 0; i < this.questions.length; i++) {
      this.answers.push('');
    }

    this.index = 0;
    this.finished = false;
    this.elapsed = 0;
    this.selKey = '';
    this.answered = 0;
    this.review = [];
    this.startTimer();
  }

  aboutToDisappear(): void {
    this.stopTimer();
  }

  private q(): ExamQuestion {
    return this.questions[this.index];
  }

  private startTimer(): void {
    this.timer = setInterval(() => {
      if (!this.finished) {
        this.elapsed = this.elapsed + 1;
      }
    }, 1000);
  }

  private stopTimer(): void {
    if (this.timer >= 0) {
      clearInterval(this.timer);
      this.timer = -1;
    }
  }

  private clock(): string {
    const m: number = Math.floor(this.elapsed / 60);
    const s: number = this.elapsed % 60;
    return `${m < 10 ? '0' : ''}${m}:${s < 10 ? '0' : ''}${s}`;
  }

  private recount(): void {
    let n: number = 0;
    for (let i = 0; i < this.answers.length; i++) {
      if (this.answers[i].length > 0) {
        n++;
      }
    }
    this.answered = n;
  }

  private pick(key: string): void {
    if (this.finished) {
      return;
    }
    this.answers[this.index] = key;
    this.selKey = key;
    this.warn = false;
    this.recount();
  }

  private setFill(v: string): void {
    if (this.finished) {
      return;
    }
    this.answers[this.index] = v;
    this.selKey = v;
    this.warn = false;
    this.recount();
  }

  private go(delta: number): void {
    const next: number = this.index + delta;
    if (next < 0 || next >= this.questions.length) {
      return;
    }
    this.index = next;
    this.selKey = this.answers[next];
    this.warn = false;
  }

  /** 最后一题时「下一题」就是交卷。漏答先提醒一次。 */
  private nextOrSubmit(): void {
    if (this.index + 1 >= this.questions.length) {
      this.trySubmit();
      return;
    }
    this.go(1);
  }

  private trySubmit(): void {
    const left: number = this.total - this.answered;
    if (left > 0 && !this.warn) {
      this.warn = true;
      this.getUIContext().getPromptAction().showToast({
        message: `还有 ${left} 道题空着，再点一次就交卷`,
        duration: 2200
      });
      return;
    }
    this.submit();
  }

  private submit(): void {
    if (this.finished) {
      return;
    }
    this.stopTimer();

    let correct: number = 0;
    const list: ReviewItem[] = [];
    for (let i = 0; i < this.questions.length; i++) {
      const q: ExamQuestion = this.questions[i];
      const user: string = this.answers[i];
      const ok: boolean = isCorrect(q, user);
      if (ok) {
        correct++;
      }
      const item = new ReviewItem();
      item.no = i + 1;
      item.kind = q.kind;
      item.stem = q.stem;
      item.your = this.describeAnswer(q, user);
      item.correct = this.describeCorrect(q);
      item.explain = q.explain;
      item.ok = ok;
      list.push(item);
    }

    this.correctCount = correct;
    this.score = scoreOf(correct, this.questions.length);
    this.review = list;
    this.finished = true;

    // 星星：90 分以上 6 颗，75 以上 5 颗，及格 3 颗，其余 2 颗。
    // 「做完整套卷子」本身就值得奖励 —— 这套卷子有 12 题，能坐下来做完不容易。
    let want: number = 2;
    if (this.score >= 90) {
      want = 6;
    } else if (this.score >= 75) {
      want = 5;
    } else if (this.score >= 60) {
      want = 3;
    }
    this.gainStars = rewards.grant('exam', want, this.score,
      `${gradeTitle(this.grade)} ${this.score} 分`);
  }

  private describeAnswer(q: ExamQuestion, user: string): string {
    if (user.length === 0) {
      return '（未作答）';
    }
    if (q.kind === QKind.Choice) {
      const i: number = user.charCodeAt(0) - 65;
      if (i >= 0 && i < q.options.length) {
        return `${user}. ${q.options[i]}`;
      }
    }
    return user;
  }

  private describeCorrect(q: ExamQuestion): string {
    if (q.kind === QKind.Choice) {
      const key: string = q.answer.trim();
      const i: number = key.length > 0 ? key.charCodeAt(0) - 65 : -1;
      if (i >= 0 && i < q.options.length) {
        return `${key}. ${q.options[i]}`;
      }
      return key;
    }
    if (q.kind === QKind.Fill) {
      const parts: string[] = q.answer.split('|');
      return parts.length > 0 ? parts[0] : q.answer;
    }
    return q.answer;
  }

  private restart(): void {
    this.answers = [];
    for (let i = 0; i < this.questions.length; i++) {
      this.answers.push('');
    }
    this.index = 0;
    this.finished = false;
    this.elapsed = 0;
    this.selKey = '';
    this.answered = 0;
    this.warn = false;
    this.review = [];
    this.gainStars = 0;
    this.startTimer();
  }

  build() {
    Stack() {
      Backdrop()

      Column({ space: 12 }) {
        PageHeader({
          title: this.finished ? '成绩单' : this.title,
          subtitle: this.finished
            ? `${gradeTitle(this.grade)} · ${this.correctCount} / ${this.total} 题`
            : `第 ${this.index + 1} / ${this.total} 题 · 已答 ${this.answered}`,
          trailing: this.finished ? '' : `⏱ ${this.clock()}`,
          onBack: () => {
            if (!this.finished && this.answered > 0 && !this.warn) {
              this.warn = true;
              this.getUIContext().getPromptAction().showToast({
                message: '现在离开，写的答案不会保存。再点一次返回就离开',
                duration: 2400
              });
              return;
            }
            router.back();
          }
        })

        if (this.finished) {
          this.resultArea()
        } else {
          this.questionArea()
          this.navRow()
        }
      }
      .width('100%')
      .height('100%')
      .padding({ left: 22, right: 22, top: 26 + kiosk.safeTopVp, bottom: 14 + kiosk.safeBottomVp })
      .constraintSize({ maxWidth: 820 })
    }
    .width('100%')
    .height('100%')
    .alignContent(Alignment.Center)
  }

  // ───────────────────────── 答题 ─────────────────────────

  @Builder
  questionArea() {
    Scroll() {
      Column({ space: 12 }) {
        Row({ space: 8 }) {
          Text(`${kindEmoji(this.q().kind)} ${kindName(this.q().kind)}`)
            .fontSize(12)
            .fontColor(subjectColor(this.subject))
            .padding({ left: 10, right: 10, top: 3, bottom: 3 })
            .borderRadius(10)
            .backgroundColor('#FFFFFF')

          Blank()

          Text(subjectEmoji(this.subject))
            .fontSize(14)
        }
        .width('100%')

        Text(this.q().stem)
          .fontSize(19)
          .lineHeight(30)
          .fontColor('#263238')
          .width('100%')
          .textAlign(TextAlign.Start)

        if (this.q().kind === QKind.Choice) {
          Column({ space: 10 }) {
            ForEach(this.q().options, (opt: string, i: number) => {
              this.optionRow(`${String.fromCharCode(65 + i)}. ${opt}`, String.fromCharCode(65 + i))
            }, (opt: string, i: number) => `${this.index}-${i}`)
          }
          .width('100%')
        } else if (this.q().kind === QKind.Judge) {
          Column({ space: 10 }) {
            this.optionRow('✅ 对', '对')
            this.optionRow('❌ 错', '错')
          }
          .width('100%')
        } else {
          TextInput({ text: this.selKey, placeholder: '在这里写答案' })
            .fontSize(19)
            .height(60)
            .width('100%')
            .borderRadius(16)
            .backgroundColor('#FFFFFF')
            .onChange((v: string) => {
              this.setFill(v);
            })
        }
      }
      .width('100%')
      .padding({ bottom: 8 })
    }
    .width('100%')
    .layoutWeight(1)
    .scrollBar(BarState.Off)
    .align(Alignment.Top)
  }

  @Builder
  optionRow(label: string, key: string) {
    Row({ space: 10 }) {
      Text(label)
        .fontSize(16)
        .fontColor(this.selKey === key ? '#FFFFFF' : '#37474F')
        .layoutWeight(1)
        .textAlign(TextAlign.Start)

      if (this.selKey === key) {
        Text('✓').fontSize(17).fontColor('#FFFFFF')
      }
    }
    .width('100%')
    .padding({ left: 16, right: 16, top: 14, bottom: 14 })
    .borderRadius(16)
    .backgroundColor(this.selKey === key ? subjectColor(this.subject) : '#FFFFFF')
    .onClick(() => {
      this.pick(key);
    })
  }

  @Builder
  navRow() {
    Row({ space: 10 }) {
      if (this.index > 0) {
        Text('上一题')
          .fontSize(16)
          .fontColor('#546E7A')
          .textAlign(TextAlign.Center)
          .width(96)
          .height(52)
          .borderRadius(16)
          .backgroundColor('#ECEFF1')
          .onClick(() => {
            this.go(-1);
          })
      }

      Text(this.index + 1 >= this.total ? '📊 交卷' : '下一题 ➡️')
        .fontSize(17)
        .fontWeight(FontWeight.Bold)
        .fontColor('#FFFFFF')
        .textAlign(TextAlign.Center)
        .layoutWeight(1)
        .height(52)
        .borderRadius(16)
        .backgroundColor(this.index + 1 >= this.total ? '#EF5350' : subjectColor(this.subject))
        .onClick(() => {
          this.nextOrSubmit();
        })
    }
    .width('100%')
  }

  // ───────────────────────── 成绩 ─────────────────────────

  @Builder
  resultArea() {
    Scroll() {
      Column({ space: 12 }) {
        Column({ space: 4 }) {
          Text(this.score >= 90 ? '🏆' : (this.score >= 75 ? '🌟' : (this.score >= 60 ? '💪' : '📚')))
            .fontSize(44)

          Text(`${this.score} 分`)
            .fontSize(38)
            .fontWeight(FontWeight.Bold)
            .fontColor(this.score >= 60 ? '#2E7D32' : '#C62828')

          Text(this.score >= 90
            ? '太厉害了！这份卷子几乎全对，可以去挑战下一套了。'
            : (this.score >= 75
              ? '不错哦！把错题弄懂，下次就能冲 90 分。'
              : (this.score >= 60
                ? '及格啦，错题下面的解析要认真看一遍。'
                : '别灰心，先把错题的知识点补上，再来一次会好很多。')))
            .fontSize(14)
            .fontColor('#5D4037')
            .textAlign(TextAlign.Center)

          Text(`答对 ${this.correctCount} / ${this.total} 题 · 用时 ${this.clock()}`)
            .fontSize(13)
            .fontColor('#9B8574')

          if (this.gainStars > 0) {
            Text(`⭐ +${this.gainStars}`)
              .fontSize(20)
              .fontWeight(FontWeight.Bold)
              .fontColor('#EF6C00')
              .padding({ left: 16, right: 16, top: 4, bottom: 4 })
              .borderRadius(14)
              .backgroundColor('#FFEBCF')
          } else {
            Text('今天模拟考的星星已经拿满啦，分数照样记在排行榜上')
              .fontSize(12)
              .fontColor('#B0BEC5')
              .textAlign(TextAlign.Center)
          }
        }
        .width('100%')
        .padding(16)
        .borderRadius(22)
        .backgroundColor('#FFFFFF')

        Column({ space: 10 }) {
          Row() {
            Text('逐题解析')
              .fontSize(16)
              .fontWeight(FontWeight.Bold)
              .fontColor('#3E2723')
            Blank()
            Text(`${this.correctCount} ✅ / ${this.total - this.correctCount} ❌`)
              .fontSize(13)
              .fontColor('#9B8574')
          }
          .width('100%')

          ForEach(this.review, (item: ReviewItem) => {
            this.reviewCard(item)
          }, (item: ReviewItem) => `${item.no}`)
        }
        .width('100%')
        .padding(16)
        .borderRadius(22)
        .backgroundColor('#FFFFFF')

        Row({ space: 10 }) {
          Text('再做一次')
            .fontSize(16)
            .fontWeight(FontWeight.Bold)
            .fontColor('#FFFFFF')
            .textAlign(TextAlign.Center)
            .layoutWeight(1)
            .height(52)
            .borderRadius(16)
            .backgroundColor(subjectColor(this.subject))
            .onClick(() => {
              this.restart();
            })

          Text('返回')
            .fontSize(16)
            .fontColor('#546E7A')
            .textAlign(TextAlign.Center)
            .layoutWeight(1)
            .height(52)
            .borderRadius(16)
            .backgroundColor('#ECEFF1')
            .onClick(() => {
              router.back();
            })
        }
        .width('100%')
        .padding({ bottom: 10 })
      }
      .width('100%')
    }
    .width('100%')
    .layoutWeight(1)
    .scrollBar(BarState.Off)
    .align(Alignment.Top)
  }

  @Builder
  reviewCard(item: ReviewItem) {
    Column({ space: 6 }) {
      Row({ space: 6 }) {
        Text(item.ok ? '✅' : '❌').fontSize(15)
        Text(`第 ${item.no} 题 · ${kindName(item.kind)}`)
          .fontSize(13)
          .fontColor('#8D6E63')
        Blank()
        Text(item.ok ? '答对' : '答错')
          .fontSize(12)
          .fontColor(item.ok ? '#2E7D32' : '#C62828')
      }
      .width('100%')

      Text(item.stem)
        .fontSize(14)
        .lineHeight(22)
        .fontColor('#37474F')
        .width('100%')

      Text(item.your.startsWith('（未作答）') ? '你的答案：未作答' : `你的答案：${item.your}`)
        .fontSize(13)
        .fontColor(item.ok ? '#2E7D32' : '#C62828')
        .width('100%')

      if (!item.ok) {
        Text(`正确答案：${item.correct}`)
          .fontSize(13)
          .fontWeight(FontWeight.Medium)
          .fontColor('#2E7D32')
          .width('100%')
      }

      if (item.explain.length > 0) {
        Text(item.explain)
          .fontSize(12)
          .lineHeight(19)
          .fontColor('#8D6E63')
          .width('100%')
          .padding(8)
          .borderRadius(10)
          .backgroundColor('#FFF8E1')
      }
    }
    .width('100%')
    .padding(12)
    .borderRadius(14)
    .backgroundColor(item.ok ? '#F1F8E9' : '#FFEBEE')
  }
}
