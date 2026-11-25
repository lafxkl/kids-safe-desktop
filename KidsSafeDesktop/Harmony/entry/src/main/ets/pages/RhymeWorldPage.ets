import { Backdrop } from '../components/Backdrop';
import { PageHeader } from '../components/PageHeader';
import { kiosk } from '../core/Kiosk';
import { PageId, router } from '../core/Router';
import { RhymeTopic, RHYME_TOPICS } from '../data/RhymeData';

/**
 * 口诀乐园首页，对应 C# 的 MathRhymesPage。
 * 只有三张主题卡（乘法 / 加减法 / 除法），点进去看口诀表和窍门。
 */
@ComponentV2
export struct RhymeWorldPage {
  build() {
    Stack() {
      Backdrop()

      Column({ space: 14 }) {
        PageHeader({
          title: '口诀乐园',
          subtitle: '背熟口诀，算题又快又准',
          onBack: () => {
            router.back();
          }
        })

        List({ space: 14 }) {
          ForEach(RHYME_TOPICS, (topic: RhymeTopic, index: number) => {
            ListItem() {
              this.card(topic, index)
            }
          }, (topic: RhymeTopic) => topic.title)
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
  card(topic: RhymeTopic, index: number) {
    Row({ space: 16 }) {
      // 主题色圆底 + emoji，和应用磁贴同一套视觉
      Stack() {
        Column()
          .width('100%')
          .height('100%')
          .borderRadius('50%')
          .backgroundColor(topic.color)

        Text(topic.emoji).fontSize(30)
      }
      .width(64)
      .height(64)

      Column({ space: 4 }) {
        Text(topic.title)
          .fontSize(20)
          .fontWeight(FontWeight.Bold)
          .fontColor('#3E2723')

        Text(`${topic.items.length} 节 · 点开就能念`)
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
    .padding(18)
    .borderRadius(22)
    .backgroundColor('#FFFFFF')
    .onClick(() => {
      router.rhymeIndex = index;
      router.push(PageId.RhymeDetail);
    })
  }
}
