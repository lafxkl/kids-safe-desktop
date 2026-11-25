import { Backdrop } from '../components/Backdrop';
import { PageHeader } from '../components/PageHeader';
import { LearnProgress } from '../core/LearnProgress';
import { kiosk } from '../core/Kiosk';
import { PageId, router } from '../core/Router';
import { CareLesson, kidsLessons, parentLessons } from '../data/CareData';

export const lessonRead: LearnProgress = new LearnProgress('learned.carelesson');

/**
 * 心理小课堂列表。对应 C# 的 CareLessonPage。
 *
 * 分「给孩子」和「给家长」两套，配色也分开：
 * 孩子是浅绿（放松），家长是浅紫（安静）。
 * 不分的话，家长翻着孩子的「想哭就哭吧」会觉得没自己的事，
 * 而孩子翻到「15 分钟电话怎么打」会以为点错了。
 */
@ComponentV2
export struct CareLessonPage {
  @Local audience: string = 'kids';
  @Local lessons: CareLesson[] = [];
  @Local readCount: number = 0;

  aboutToAppear(): void {
    this.audience = router.careAudience === 'parents' ? 'parents' : 'kids';
    this.reload();
  }

  private reload(): void {
    this.lessons = this.audience === 'parents' ? parentLessons() : kidsLessons();
    let n: number = 0;
    for (let i = 0; i < this.lessons.length; i++) {
      if (lessonRead.has(this.lessons[i].id)) {
        n++;
      }
    }
    this.readCount = n;
  }

  private switchTo(a: string): void {
    if (this.audience === a) {
      return;
    }
    this.audience = a;
    router.careAudience = a;
    this.reload();
  }

  build() {
    Stack() {
      Backdrop()

      Column({ space: 10 }) {
        PageHeader({
          title: this.audience === 'parents' ? '给家长看的' : '心理小课堂',
          subtitle: `共 ${this.lessons.length} 节 · 已读 ${this.readCount} 节`,
          trailing: '',
          onBack: () => {
            router.back();
          }
        })

        this.tabs()

        Scroll() {
          Column({ space: 10 }) {
            Text(this.intro())
              .fontSize(12)
              .lineHeight(20)
              .fontColor('#8A7B84')
              .width('100%')
              .padding(12)
              .borderRadius(14)
              .backgroundColor(this.audience === 'parents' ? '#F3EEFF' : '#EAF7EE')

            ForEach(this.lessons, (l: CareLesson) => {
              this.lessonRow(l)
            }, (l: CareLesson) => l.id)
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

  private intro(): string {
    if (this.audience === 'parents') {
      return '这一组写给爸爸妈妈、爷爷奶奶：怎么跟孩子解释外出、15 分钟电话怎么打、' +
        '哪些信号需要专业帮助，也有几节讲日常怎么说话、怎么放手。';
    }
    return '这些课讲的是你可能遇到过、但没人跟你好好说过的事。' +
      '不用一次看完，挑一节现在最想看的。';
  }

  @Builder
  tabs() {
    Row({ space: 8 }) {
      this.tab('给孩子', 'kids')
      this.tab('给家长', 'parents')
    }
    .width('100%')
  }

  @Builder
  tab(label: string, key: string) {
    Text(label)
      .fontSize(14)
      .fontWeight(this.audience === key ? FontWeight.Bold : FontWeight.Normal)
      .fontColor(this.audience === key ? '#FFFFFF' : '#7A6B74')
      .textAlign(TextAlign.Center)
      .layoutWeight(1)
      .height(40)
      .borderRadius(13)
      .backgroundColor(this.audience === key ? (key === 'parents' ? '#7E57C2' : '#2E7D32') : '#FFFFFF')
      .onClick(() => {
        this.switchTo(key);
      })
  }

  @Builder
  lessonRow(l: CareLesson) {
    Row({ space: 10 }) {
      Stack() {
        Text(l.emoji).fontSize(20)
      }
      .width(42)
      .height(42)
      .borderRadius(14)
      .backgroundColor(this.audience === 'parents' ? '#F3EEFF' : '#EAF7EE')
      .alignContent(Alignment.Center)

      Column({ space: 3 }) {
        Row({ space: 6 }) {
          Text(l.title)
            .fontSize(14.5)
            .fontWeight(FontWeight.Bold)
            .fontColor('#4E3B45')
            .layoutWeight(1)
            .maxLines(1)
            .textOverflow({ overflow: TextOverflow.Ellipsis })

          if (lessonRead.has(l.id)) {
            Text('已读')
              .fontSize(9.5)
              .fontColor('#2E7D32')
              .padding({ left: 5, right: 5, top: 1, bottom: 1 })
              .borderRadius(6)
              .backgroundColor('#E8F5E9')
          }
        }
        .width('100%')

        Text(l.subtitle)
          .fontSize(11.5)
          .fontColor('#8A7B84')
          .maxLines(1)
          .textOverflow({ overflow: TextOverflow.Ellipsis })

        Text(`${l.tag} · 约 ${l.minutes} 分钟`)
          .fontSize(10)
          .fontColor('#B0A2AC')
      }
      .alignItems(HorizontalAlign.Start)
      .layoutWeight(1)
    }
    .width('100%')
    .padding({ left: 11, right: 11, top: 10, bottom: 10 })
    .borderRadius(16)
    .backgroundColor('#FFFFFF')
    .onClick(() => {
      router.careLessonId = l.id;
      router.push(PageId.CareLessonDetail);
    })
  }
}
