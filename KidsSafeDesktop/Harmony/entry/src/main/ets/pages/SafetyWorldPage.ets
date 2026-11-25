import { Backdrop } from '../components/Backdrop';
import { PageHeader } from '../components/PageHeader';
import { kiosk } from '../core/Kiosk';
import { PageId, router } from '../core/Router';
import { SAFETY_TOPICS, SafetyTopic, safetyTipCount } from '../data/SafetyData';

/**
 * 安全知识首页，对应 C# 的 SafetyKnowledgePage。
 * 按主题分卡片（交通、防拐、防火……），点进去看这个主题下的所有贴士。
 */
@ComponentV2
export struct SafetyWorldPage {
  build() {
    Stack() {
      Backdrop()

      Column({ space: 14 }) {
        PageHeader({
          title: '安全知识',
          subtitle: `${SAFETY_TOPICS.length} 个主题 · ${safetyTipCount()} 条`,
          onBack: () => {
            router.back();
          }
        })

        List({ space: 12 }) {
          ForEach(SAFETY_TOPICS, (topic: SafetyTopic, index: number) => {
            ListItem() {
              this.card(topic, index)
            }
          }, (topic: SafetyTopic) => topic.title)
        }
        .width('100%')
        .layoutWeight(1)
        .scrollBar(BarState.Off)
        .edgeEffect(EdgeEffect.Spring)
      }
      .width('100%')
      .height('100%')
      .padding({ left: 32, right: 32, top: 26 + kiosk.safeTopVp, bottom: 16 + kiosk.safeBottomVp })
      .constraintSize({ maxWidth: 1000 })
    }
    .width('100%')
    .height('100%')
    .alignContent(Alignment.Center)
  }

  @Builder
  card(topic: SafetyTopic, index: number) {
    Row({ space: 14 }) {
      Stack() {
        Column()
          .width('100%')
          .height('100%')
          .borderRadius('50%')
          .backgroundColor(topic.color)

        Text(topic.emoji).fontSize(26)
      }
      .width(56)
      .height(56)

      Column({ space: 3 }) {
        Text(topic.title)
          .fontSize(19)
          .fontWeight(FontWeight.Bold)
          .fontColor('#3E2723')

        Text(`${topic.tips.length} 条`)
          .fontSize(13)
          .fontColor('#9B8574')
      }
      .alignItems(HorizontalAlign.Start)
      .layoutWeight(1)

      Text('›')
        .fontSize(26)
        .fontColor('#D7CCC8')
    }
    .width('100%')
    .padding(16)
    .borderRadius(20)
    .backgroundColor('#FFFFFF')
    .onClick(() => {
      router.safetyIndex = index;
      router.push(PageId.SafetyDetail);
    })
  }
}
