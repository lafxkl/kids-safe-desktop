import { Backdrop } from '../components/Backdrop';
import { PageHeader } from '../components/PageHeader';
import { kiosk } from '../core/Kiosk';
import { PageId, router } from '../core/Router';
import { papersOf } from '../data/ExamData';

/**
 * 学习天地，对应 C# 的 LearnWorldPage。
 *
 * 一页两个模式：口算练习 / 模拟考试，共用同一张年级表。
 * 之所以不再分两个入口放到桌面上：桌面已经有 15 个磁贴了，再拆两格会挤到第二屏，
 * 而「练口算」和「做卷子」本来就是同一件事的两个强度，放一起孩子也更容易理解。
 */

/** 年级磁贴的文案。emoji 和配色沿用 C# 端，孩子换端不用重新认。 */
class GradeCard {
  grade: number = 1;
  emoji: string = '📘';
  desc: string = '';
  color: string = '#29B6F6';

  constructor(grade: number, emoji: string, desc: string, color: string) {
    this.grade = grade;
    this.emoji = emoji;
    this.desc = desc;
    this.color = color;
  }
}

const GRADES: GradeCard[] = [
  new GradeCard(1, '🍎', '20 以内加减 · 100 以内不进位', '#EF5350'),
  new GradeCard(2, '🐰', '表内乘除 · 有余数 · 两步混合', '#AB47BC'),
  new GradeCard(3, '🚀', '万以内加减 · 多位数乘 · 除一位数', '#26C6DA'),
  new GradeCard(4, '🌟', '三位数乘两位 · 除两位数 · 四则混合', '#66BB6A'),
  new GradeCard(5, '🌈', '小数乘除 · 简易方程 · 图形面积', '#FFA726'),
  new GradeCard(6, '🎯', '分数 · 百分数 · 比例 · 圆', '#EC407A'),
  new GradeCard(7, '🧭', '有理数 · 整式 · 一元一次方程', '#7E57C2'),
  new GradeCard(8, '🔭', '幂与根式 · 一次函数 · 方程组', '#42A5F5'),
  new GradeCard(9, '🎓', '二次根式 · 一元二次方程 · 函数', '#26A69A')
];

@ComponentV2
export struct LearnWorldPage {
  /** false = 口算练习，true = 模拟考试 */
  @Local examMode: boolean = false;

  build() {
    Stack() {
      Backdrop()

      Column({ space: 14 }) {
        PageHeader({
          title: '学习天地',
          subtitle: this.examMode
            ? '三年级到初三 · 语文 / 数学 / 英语模拟卷'
            : '一年级到初三 · 每次 10 道，做完自动批改',
          onBack: () => {
            router.back();
          }
        })

        this.modeRow()

        Text(this.examMode
          ? '选一个年级，挑一套卷子做完再交卷，会帮你打分并讲解错题～'
          : '选一个年级，做完 10 道题会帮你批改，答错的会告诉你正确答案。')
          .fontSize(14)
          .fontColor('#8D6E63')
          .width('100%')
          .textAlign(TextAlign.Center)

        Grid() {
          ForEach(GRADES, (g: GradeCard) => {
            GridItem() {
              // 模拟考没有 1~2 年级的卷子（题库从三年级起），这里直接标灰，
              // 而不是让家长点进去看到一个空列表。
              this.tile(g, this.examMode && !this.hasPaper(g.grade))
            }
          }, (g: GradeCard) => `${g.grade}`)
        }
        .columnsTemplate('1fr 1fr 1fr')
        .columnsGap(12)
        .rowsGap(12)
        .layoutWeight(1)
        .scrollBar(BarState.Off)
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
  modeRow() {
    Row({ space: 10 }) {
      this.modePill('🧮 口算练习', !this.examMode, '#0288D1', false)
      this.modePill('📝 模拟考试', this.examMode, '#EF5350', true)
    }
    .width('100%')
    .justifyContent(FlexAlign.Center)
  }

  @Builder
  modePill(label: string, active: boolean, color: string, exam: boolean) {
    Text(label)
      .fontSize(15)
      .fontWeight(active ? FontWeight.Bold : FontWeight.Normal)
      .fontColor(active ? '#FFFFFF' : color)
      .textAlign(TextAlign.Center)
      .height(40)
      .layoutWeight(1)
      .borderRadius(20)
      .backgroundColor(active ? color : '#FFFFFF')
      .onClick(() => {
        this.examMode = exam;
      })
  }

  @Builder
  tile(g: GradeCard, muted: boolean) {
    Column({ space: 6 }) {
      Stack() {
        Column()
          .width('100%')
          .height('100%')
          .borderRadius('50%')
          .backgroundColor(muted ? '#ECEFF1' : g.color)
          .opacity(muted ? 0.7 : 1)

        Text(g.emoji).fontSize(28).opacity(muted ? 0.4 : 1)
      }
      .width(56)
      .height(56)

      Text(g.grade === 7 ? '初一' : (g.grade === 8 ? '初二' : (g.grade === 9 ? '初三' : `${g.grade} 年级`)))
        .fontSize(16)
        .fontWeight(FontWeight.Bold)
        .fontColor(muted ? '#B0BEC5' : '#3E2723')

      Text(muted ? '暂无模拟卷' : (this.examMode ? '3 套卷' : this.shortDesc(g.desc)))
        .fontSize(10)
        .fontColor(muted ? '#B0BEC5' : '#9B8574')
        .maxLines(2)
        .textAlign(TextAlign.Center)
        .lineHeight(14)
    }
    .width('100%')
    .height(150)
    .justifyContent(FlexAlign.Center)
    .borderRadius(20)
    .backgroundColor('#FFFFFF')
    .onClick(() => {
      if (muted) {
        this.getUIContext().getPromptAction().showToast({
          message: '这个年级的模拟卷还在整理中，先练口算吧～',
          duration: 2000
        });
        return;
      }
      router.learnGrade = g.grade;
      router.push(this.examMode ? PageId.ExamList : PageId.OralPractice);
    })
  }

  private hasPaper(grade: number): boolean {
    return papersOf(grade).length > 0;
  }

  /** 磁贴只有 10vp 的字，长描述会被截成半句话，这里取前半段。 */
  private shortDesc(desc: string): string {
    const cut: number = desc.indexOf(' · ');
    return cut > 0 ? desc.substring(0, cut) : desc;
  }
}
