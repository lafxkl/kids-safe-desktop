import { Backdrop } from '../components/Backdrop';
import { PageHeader } from '../components/PageHeader';
import { kiosk } from '../core/Kiosk';
import { safetyLearned } from '../core/LearnProgress';
import { rewards } from '../core/Rewards';
import { router } from '../core/Router';
import { SAFETY_TOPICS, SafetyTip, SafetyTopic } from '../data/SafetyData';

/**
 * 安全知识详情，对应 C# 的 SafetyKnowledgeDetailPage：
 * 一个主题下的全部贴士，一条一张卡（大 emoji + 一句大白话）。
 *
 * 字号特意比古诗页大一点：这些是要孩子记住并照着做的，
 * 长句子排紧密了容易看漏「不能」「别」这类关键否定词。
 */
@ComponentV2
export struct SafetyDetailPage {
  private topic: SafetyTopic = SAFETY_TOPICS[0];
  @Local learned: boolean = false;

  aboutToAppear(): void {
    const want: number = router.safetyIndex;
    if (want >= 0 && want < SAFETY_TOPICS.length) {
      this.topic = SAFETY_TOPICS[want];
    }
    this.learned = safetyLearned.has(this.topic.title);
  }

  /**
   * 记住了。安全知识是按「主题」算的（一个主题 10 条贴士），
   * 所以成绩是「记住了几个主题」，不是「看了几条」。
   */
  private markLearned(): void {
    const first: boolean = safetyLearned.mark(this.topic.title);
    this.learned = true;
    if (first) {
      rewards.grant('safety', 5, safetyLearned.count(), this.topic.title);
      this.getUIContext().getPromptAction().showToast({
        message: `记住了「${this.topic.title}」！⭐ +5`,
        duration: 1800
      });
    } else {
      this.getUIContext().getPromptAction().showToast({ message: '这个主题已经记住啦', duration: 1400 });
    }
  }

  build() {
    Stack() {
      Backdrop()

      Column({ space: 12 }) {
        PageHeader({
          title: this.topic.title,
          subtitle: `${this.topic.emoji} 安全知识 · ${this.topic.tips.length} 条`,
          onBack: () => {
            router.back();
          }
        })

        List({ space: 10 }) {
          ForEach(this.topic.tips, (tip: SafetyTip) => {
            ListItem() {
              Row({ space: 14 }) {
                Text(tip.emoji).fontSize(32)

                Text(tip.text)
                  .fontSize(17)
                  .lineHeight(27)
                  .fontColor('#37474F')
                  .layoutWeight(1)
              }
              .width('100%')
              .alignItems(VerticalAlign.Center)
              .padding({ left: 16, right: 16, top: 14, bottom: 14 })
              .borderRadius(18)
              .backgroundColor('#FFFFFF')
              .border({ width: 1, color: this.topic.color })
            }
          }, (tip: SafetyTip, i: number) => `${i}-${tip.text}`)

          ListItem() {
            Column({ space: 10 }) {
              Text('记住这些，就能保护好自己～')
                .fontSize(13)
                .fontColor('#A1887F')
                .width('100%')
                .textAlign(TextAlign.Center)

              Row({ space: 8 }) {
                Text(this.learned ? '✅' : '🛡️').fontSize(18)
                Text(this.learned ? '已经记住啦' : '我记住了')
                  .fontSize(16)
                  .fontWeight(FontWeight.Medium)
                  .fontColor(this.learned ? '#E53935' : '#FFFFFF')
              }
              .justifyContent(FlexAlign.Center)
              .width('100%')
              .height(50)
              .borderRadius(18)
              .backgroundColor(this.learned ? '#FFEBEE' : '#E53935')
              .onClick(() => {
                this.markLearned();
              })
            }
            .width('100%')
            .padding({ top: 6, bottom: 12 })
          }
        }
        .width('100%')
        .layoutWeight(1)
        .scrollBar(BarState.Off)
        .edgeEffect(EdgeEffect.Spring)
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
}
