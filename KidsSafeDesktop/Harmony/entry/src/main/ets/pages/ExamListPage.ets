import { Backdrop } from '../components/Backdrop';
import { PageHeader } from '../components/PageHeader';
import { kiosk } from '../core/Kiosk';
import { gradeTitle } from '../core/OralGen';
import { PageId, router } from '../core/Router';
import { ExamPaper, papersOf, subjectColor, subjectEmoji, subjectName } from '../data/ExamData';

/**
 * 某年级的模拟卷列表，对应 C# 的 ExamPaperListPage。
 * 语文 / 数学 / 英语各一套，点开进入答题页。
 */
@ComponentV2
export struct ExamListPage {
  @Local grade: number = 3;
  private papers: ExamPaper[] = [];

  aboutToAppear(): void {
    const g: number = router.learnGrade;
    this.grade = g >= 1 && g <= 9 ? g : 3;
    this.papers = papersOf(this.grade);
  }

  build() {
    Stack() {
      Backdrop()

      Column({ space: 14 }) {
        PageHeader({
          title: `${gradeTitle(this.grade)} · 模拟考试`,
          subtitle: `${this.papers.length} 套卷 · 交卷后自动打分讲错题`,
          onBack: () => {
            router.back();
          }
        })

        if (this.papers.length === 0) {
          Column({ space: 8 }) {
            Text('📭').fontSize(44)
            Text('这个年级的模拟卷还在整理中，先练口算吧～')
              .fontSize(15)
              .fontColor('#8D6E63')
              .textAlign(TextAlign.Center)
          }
          .width('100%')
          .layoutWeight(1)
          .justifyContent(FlexAlign.Center)
        } else {
          List({ space: 12 }) {
            ForEach(this.papers, (p: ExamPaper) => {
              ListItem() {
                this.card(p)
              }
            }, (p: ExamPaper) => `${p.id}`)
          }
          .width('100%')
          .layoutWeight(1)
          .scrollBar(BarState.Off)
          .edgeEffect(EdgeEffect.Spring)
        }
      }
      .width('100%')
      .height('100%')
      .padding({ left: 26, right: 26, top: 26 + kiosk.safeTopVp, bottom: 16 + kiosk.safeBottomVp })
      .constraintSize({ maxWidth: 900 })
    }
    .width('100%')
    .height('100%')
    .alignContent(Alignment.Center)
  }

  @Builder
  card(p: ExamPaper) {
    Row({ space: 14 }) {
      Stack() {
        Column()
          .width('100%')
          .height('100%')
          .borderRadius('50%')
          .backgroundColor(subjectColor(p.subject))

        Text(subjectEmoji(p.subject)).fontSize(26)
      }
      .width(56)
      .height(56)

      Column({ space: 4 }) {
        Text(p.title)
          .fontSize(17)
          .fontWeight(FontWeight.Bold)
          .fontColor('#3E2723')
          .maxLines(2)

        Text(`${p.term} · ${p.questions.length} 题 · 建议 ${p.minutes} 分钟`)
          .fontSize(12)
          .fontColor('#9B8574')

        if (p.note.length > 0) {
          Text(p.note)
            .fontSize(12)
            .fontColor(subjectColor(p.subject))
            .maxLines(2)
        }
      }
      .alignItems(HorizontalAlign.Start)
      .layoutWeight(1)

      Text(subjectName(p.subject))
        .fontSize(12)
        .fontColor('#FFFFFF')
        .padding({ left: 9, right: 9, top: 3, bottom: 3 })
        .borderRadius(10)
        .backgroundColor(subjectColor(p.subject))
    }
    .width('100%')
    .padding(16)
    .borderRadius(20)
    .backgroundColor('#FFFFFF')
    .alignItems(VerticalAlign.Center)
    .onClick(() => {
      router.examPaperId = p.id;
      router.push(PageId.Exam);
    })
  }
}
