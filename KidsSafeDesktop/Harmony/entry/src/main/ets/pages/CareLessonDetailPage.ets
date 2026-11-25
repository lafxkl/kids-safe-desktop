import { Backdrop } from '../components/Backdrop';
import { PageHeader } from '../components/PageHeader';
import { kiosk } from '../core/Kiosk';
import { rewards } from '../core/Rewards';
import { PageId, router } from '../core/Router';
import { speech } from '../core/Speech';
import { CareLesson, LessonSection, findLesson, kidsLessons } from '../data/CareData';
import { lessonRead } from './CareLessonPage';

/**
 * 心理小课堂详情。对应 C# 的 CareLessonDetailPage。
 *
 * 版面顺序是刻意的：先把「全篇最重要的一句」（KeyLine）放大放在最上面，
 * 再讲正文。孩子往往只看一眼就划走，那就让这一眼看到的是那句能撑住他的话。
 *
 * 「我读完了」发 2 颗星，但每节只有第一次算 —— 靠 lessonRead 去重。
 * 发星不是目的，是为了让这一节在他心里有个「完成了」的句号。
 */
@ComponentV2
export struct CareLessonDetailPage {
  @Local lesson: CareLesson = kidsLessons()[0];
  @Local read: boolean = false;
  @Local gainStars: number = 0;
  @Local flash: string = '';

  aboutToAppear(): void {
    let found: CareLesson | null = findLesson(router.careLessonId);
    if (found === null) {
      // 存档里的 id 对不上（改过内容、跨版本升级）时回退第一课，不要白屏
      found = kidsLessons()[0];
    }
    this.lesson = found;
    this.read = lessonRead.has(found.id);
  }

  private markRead(): void {
    const first: boolean = lessonRead.mark(this.lesson.id);
    this.read = true;
    if (!first) {
      this.flash = '这一节你已经读过了，星星只发一次';
      return;
    }
    const want: number = 2;
    this.gainStars = rewards.grant('carelesson', want, lessonRead.count(), '读过一节');
    this.flash = this.gainStars > 0 ? `⭐ +${this.gainStars}` : '今天小课堂的星星拿满了，明天再来读';
  }

  private speakAll(): void {
    if (speech.speaking) {
      speech.stop();
      return;
    }
    let text: string = `${this.lesson.title}。${this.lesson.keyLine}。`;
    for (let i = 0; i < this.lesson.sections.length; i++) {
      const s: LessonSection = this.lesson.sections[i];
      text += `${s.heading}。${s.body}`;
    }
    speech.speak(text);
  }

  build() {
    Stack() {
      Backdrop()

      Column({ space: 10 }) {
        PageHeader({
          title: this.lesson.audience === 'parents' ? '给家长看的' : '心理小课堂',
          subtitle: this.lesson.tag,
          trailing: `${this.lesson.minutes} 分钟`,
          onBack: () => {
            router.back();
          }
        })

        Scroll() {
          Column({ space: 12 }) {
            this.heroCard()

            ForEach(this.lesson.sections, (s: LessonSection, i: number) => {
              this.sectionCard(s, i)
            }, (s: LessonSection, i: number) => `${i}-${s.heading}`)

            this.tailCard()
          }
          .width('100%')
          .padding({ bottom: 12 })
        }
        .width('100%')
        .layoutWeight(1)
        .scrollBar(BarState.Off)
        .align(Alignment.Top)

        this.actionBar()
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
  heroCard() {
    Column({ space: 8 }) {
      Text(this.lesson.emoji).fontSize(38)

      Text(this.lesson.title)
        .fontSize(21)
        .fontWeight(FontWeight.Bold)
        .fontColor('#3E2A33')
        .textAlign(TextAlign.Center)
        .width('100%')

      Text(this.lesson.subtitle)
        .fontSize(12.5)
        .fontColor('#9B7C88')
        .textAlign(TextAlign.Center)
        .width('100%')

      Text(this.lesson.keyLine)
        .fontSize(16)
        .lineHeight(26)
        .fontWeight(FontWeight.Bold)
        .fontColor('#8E2A54')
        .textAlign(TextAlign.Center)
        .width('100%')
        .padding({ left: 12, right: 12, top: 12, bottom: 12 })
        .borderRadius(16)
        .backgroundColor('#FFF0F5')
        .margin({ top: 4 })
    }
    .width('100%')
    .padding({ left: 16, right: 16, top: 18, bottom: 16 })
    .borderRadius(22)
    .backgroundColor('#FFFFFF')
    .alignItems(HorizontalAlign.Center)
  }

  @Builder
  sectionCard(s: LessonSection, i: number) {
    Column({ space: 6 }) {
      Row({ space: 7 }) {
        Text(String(i + 1))
          .fontSize(10)
          .fontWeight(FontWeight.Bold)
          .fontColor('#FFFFFF')
          .textAlign(TextAlign.Center)
          .width(19)
          .height(19)
          .borderRadius(10)
          .backgroundColor('#C98AA2')

        Text(s.heading)
          .fontSize(14.5)
          .fontWeight(FontWeight.Bold)
          .fontColor('#5D3A47')
          .layoutWeight(1)
      }
      .width('100%')

      Text(s.body)
        .fontSize(14)
        .lineHeight(25)
        .fontColor('#4A3B42')
        .width('100%')
    }
    .width('100%')
    .padding(14)
    .borderRadius(18)
    .backgroundColor('#FFFFFF')
    .alignItems(HorizontalAlign.Start)
  }

  @Builder
  tailCard() {
    Column({ space: 8 }) {
      Text('还是很难受？')
        .fontSize(13)
        .fontWeight(FontWeight.Bold)
        .fontColor('#5D3A47')
        .width('100%')

      Text('找个人说一说，比一个人反复想有用。可以找老师、家里的长辈，也可以打 12355。')
        .fontSize(12)
        .lineHeight(19)
        .fontColor('#8A7B84')
        .width('100%')

      Text('去看求助电话 →')
        .fontSize(12.5)
        .fontWeight(FontWeight.Bold)
        .fontColor('#B4577C')
        .padding({ left: 10, right: 10, top: 6, bottom: 6 })
        .borderRadius(11)
        .backgroundColor('#FCE4EC')
        .onClick(() => {
          // 回到关怀中心首页（栈里上两层），而不是 reset —— reset 会把返回键堵死
          router.popTo(PageId.CareCenter);
        })
    }
    .width('100%')
    .padding(14)
    .borderRadius(18)
    .backgroundColor('#FFF7FA')
    .alignItems(HorizontalAlign.Start)
  }

  @Builder
  actionBar() {
    Column({ space: 6 }) {
      if (this.flash.length > 0) {
        Text(this.flash)
          .fontSize(11.5)
          .fontColor('#8E2A54')
          .textAlign(TextAlign.Center)
          .width('100%')
      }

      Row({ space: 10 }) {
        Text(speech.speaking ? '⏹ 停下来' : '🔊 读给我听')
          .fontSize(14)
          .fontColor('#31597F')
          .textAlign(TextAlign.Center)
          .layoutWeight(1)
          .height(50)
          .borderRadius(16)
          .backgroundColor('#EAF2FB')
          .onClick(() => {
            this.speakAll();
          })

        Text(this.read ? '再读一遍' : '我读完了')
          .fontSize(15)
          .fontWeight(FontWeight.Bold)
          .fontColor('#FFFFFF')
          .textAlign(TextAlign.Center)
          .layoutWeight(1)
          .height(50)
          .borderRadius(16)
          .backgroundColor(this.read ? '#B98CA1' : '#E4577F')
          .onClick(() => {
            this.markRead();
          })
      }
      .width('100%')
    }
    .width('100%')
  }
}
