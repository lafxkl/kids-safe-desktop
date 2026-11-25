import { Backdrop } from '../components/Backdrop';
import { PageHeader } from '../components/PageHeader';
import { kiosk } from '../core/Kiosk';
import { rewards } from '../core/Rewards';
import { router } from '../core/Router';
import { BREATH_STEPS, BreathPhase } from '../data/CareData';

/**
 * 4-7-8 呼吸放松。对应 C# 的 BreathingPage。
 *
 * 为什么给孩子做这个：情绪上头时讲道理是无效的，先让身体慢下来才有用。
 * 4-7-8 的关键是「呼气比吸气长」（4 吸、7 屏、8 呼），能激活副交感神经。
 *
 * 界面上只给圆圈和数字，不讲术语：孩子跟着缩放走就行。
 * 成年人看呼吸应用会觉得「这不就是跟着圈动」，孩子也一样 —— 不需要懂原理。
 */

const ROUNDS: number = 4;
/** 三段各自的缩放目标：吸气放大、屏住几乎不动、呼气缩回。 */
const SCALES: number[] = [1.32, 1.34, 1.0];

@ComponentV2
export struct BreathingPage {
  @Local phaseTitle: string = '准备好了吗';
  @Local phaseDetail: string = '找张椅子坐好，后背靠着椅背';
  @Local countdown: number = 0;
  @Local roundText: string = '';
  @Local circleScale: number = 1;
  @Local animateMs: number = 400;
  @Local running: boolean = false;
  @Local finished: boolean = false;
  @Local gainStars: number = 0;
  @Local flash: string = '';
  /** 导引文案（阶段说明），和 phaseTitle 分开是为了「做完了」时不覆盖数字区 */
  @Local headline: string = '';

  private phaseIdx: number = 0;
  private roundIdx: number = 1;
  private timer: number = -1;

  aboutToDisappear(): void {
    this.stopTimer();
  }

  private stopTimer(): void {
    if (this.timer >= 0) {
      clearTimeout(this.timer);
      this.timer = -1;
    }
  }

  private start(): void {
    if (this.running) {
      return;
    }
    this.running = true;
    this.finished = false;
    this.flash = '';
    this.gainStars = 0;
    this.roundIdx = 1;
    this.phaseIdx = 0;
    this.headline = '';
    this.enterPhase();
  }

  private enterPhase(): void {
    const p: BreathPhase = BREATH_STEPS[this.phaseIdx];
    this.phaseTitle = p.title;
    this.phaseDetail = p.detail;
    this.countdown = p.seconds;
    this.roundText = `第 ${this.roundIdx} / ${ROUNDS} 轮`;
    // 动画时长跟这一段一起走完，圆圈和数字同步
    this.animateMs = p.seconds * 1000;
    this.circleScale = SCALES[this.phaseIdx];
    this.tick();
  }

  /** 每秒减一，减到 0 换下一段。用 setTimeout 链而不是 setInterval： */
  /** setInterval 在应用切后台再回来会一次性补发很多次，秒数直接跳完。 */
  private tick(): void {
    this.timer = setTimeout(() => {
      this.timer = -1;
      if (!this.running) {
        return;
      }
      this.countdown = this.countdown - 1;
      if (this.countdown > 0) {
        this.tick();
        return;
      }
      this.phaseIdx = this.phaseIdx + 1;
      if (this.phaseIdx >= 3) {
        // 三段走完 = 一轮结束
        this.phaseIdx = 0;
        this.roundIdx = this.roundIdx + 1;
        if (this.roundIdx > ROUNDS) {
          this.finish();
          return;
        }
      }
      this.enterPhase();
    }, 1000);
  }

  private stop(): void {
    this.running = false;
    this.stopTimer();
    this.circleScale = 1;
    this.animateMs = 300;
    this.countdown = 0;
    this.phaseTitle = '停下了';
    this.phaseDetail = '没关系，想练的时候再来。';
    this.roundText = '';
    this.headline = '';
  }

  private finish(): void {
    this.running = false;
    this.stopTimer();
    this.finished = true;
    this.phaseTitle = '做完了';
    this.phaseDetail = '感觉一下：现在的心跳，是不是比刚才慢一点了？';
    this.headline = '🌿';
    this.countdown = 0;
    this.roundText = `${ROUNDS} / ${ROUNDS} 轮`;
    this.animateMs = 600;
    this.circleScale = 1;

    // 一天做满 3 次就不再发星，但练习本身照样能做
    this.gainStars = rewards.grant('care', 1, 0, '完成呼吸放松');
    this.flash = this.gainStars > 0 ? `⭐ +${this.gainStars}` : '今天的星星拿满了，练了就是赚到';
  }

  build() {
    Stack() {
      Backdrop()

      Column({ space: 12 }) {
        PageHeader({
          title: '呼吸放松',
          subtitle: '吸气 4 下 · 停 7 下 · 呼气 8 下',
          trailing: this.roundText,
          onBack: () => {
            router.back();
          }
        })

        Column({ space: 18 }) {
          // 光圈：外圈半透明，跟着中心圆一起缩放，看起来像在「呼吸」
          Stack() {
            Column()
              .width(252)
              .height(252)
              .borderRadius(126)
              .backgroundColor('#BFE7F5')
              .opacity(0.55)
              .scale({ x: this.circleScale, y: this.circleScale })
              .animation({ duration: this.animateMs, curve: Curve.EaseInOut })

            Column({ space: 2 }) {
              if (this.headline.length > 0) {
                Text(this.headline).fontSize(46)
              }
              if (this.countdown > 0) {
                Text(String(this.countdown))
                  .fontSize(58)
                  .fontWeight(FontWeight.Bold)
                  .fontColor('#1B5E6B')
              }
              Text(this.phaseTitle)
                .fontSize(this.countdown > 0 ? 15 : 20)
                .fontWeight(FontWeight.Bold)
                .fontColor('#256C78')
            }
            .width(200)
            .height(200)
            .borderRadius(100)
            .backgroundColor('#E4F6FB')
            .justifyContent(FlexAlign.Center)
            .alignItems(HorizontalAlign.Center)
          }
          .width(252)
          .height(252)
          .alignContent(Alignment.Center)

          Text(this.phaseDetail)
            .fontSize(15)
            .lineHeight(24)
            .fontColor('#4E3B45')
            .textAlign(TextAlign.Center)
            .width('100%')
            .constraintSize({ maxWidth: 420 })

          if (this.flash.length > 0) {
            Text(this.flash)
              .fontSize(14)
              .fontWeight(FontWeight.Bold)
              .fontColor('#EF6C00')
              .padding({ left: 14, right: 14, top: 5, bottom: 5 })
              .borderRadius(12)
              .backgroundColor('#FFEBCF')
          }

          if (this.running) {
            Text('停下来')
              .fontSize(16)
              .fontColor('#546E7A')
              .textAlign(TextAlign.Center)
              .width('100%')
              .constraintSize({ maxWidth: 420 })
              .height(52)
              .borderRadius(17)
              .backgroundColor('#ECEFF1')
              .onClick(() => {
                this.stop();
              })
          } else {
            Text(this.finished ? '再练一次' : '开始')
              .fontSize(17)
              .fontWeight(FontWeight.Bold)
              .fontColor('#FFFFFF')
              .textAlign(TextAlign.Center)
              .width('100%')
              .constraintSize({ maxWidth: 420 })
              .height(54)
              .borderRadius(18)
              .backgroundColor('#26A69A')
              .onClick(() => {
                this.start();
              })
          }

          Text('做 4 轮大概 1 分半。心里堵、睡不着、想发脾气的时候都可以来。')
            .fontSize(11.5)
            .lineHeight(18)
            .fontColor('#A1887F')
            .textAlign(TextAlign.Center)
            .width('100%')
            .constraintSize({ maxWidth: 420 })
        }
        .width('100%')
        .layoutWeight(1)
        .justifyContent(FlexAlign.Center)
        .alignItems(HorizontalAlign.Center)
      }
      .width('100%')
      .height('100%')
      .padding({ left: 20, right: 20, top: 20 + kiosk.safeTopVp, bottom: 16 + kiosk.safeBottomVp })
      .constraintSize({ maxWidth: 780 })
    }
    .width('100%')
    .height('100%')
    .alignContent(Alignment.Center)
  }
}
