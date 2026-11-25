import { Backdrop } from '../components/Backdrop';
import { PageHeader } from '../components/PageHeader';
import { kiosk } from '../core/Kiosk';
import { PageId, router } from '../core/Router';
import { Poem, POEMS, POEM_CATEGORIES, poemsByCategory, poemCount, poemColor, poemEmoji } from '../data/PoemData';

/** 分类 chip 的取值列表：第一项固定是「全部」。 */
const CHIPS: string[] = ['全部'].concat(POEM_CATEGORIES);

/**
 * 古诗学习首页，对应 C# 的 PoemWorldPage。
 * 按主题筛选 + 诗词列表，内容全部离线内置（data/PoemData.ets），点卡片进详情页。
 */
@ComponentV2
export struct PoemWorldPage {
  @Local category: string = '全部';

  build() {
    Stack() {
      Backdrop()

      Column({ space: 14 }) {
        PageHeader({
          title: '古诗学习',
          subtitle: `${POEMS.length} 首 · 离线可读`,
          onBack: () => {
            router.back();
          }
        })

        this.chips()

        List({ space: 12 }) {
          ForEach(poemsByCategory(this.category), (poem: Poem, index: number) => {
            ListItem() {
              this.card(poem, index)
            }
          }, (poem: Poem) => `${poem.title}-${poem.category}`)
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

  /** 横向滚动的分类 chip：选中填深棕，未选中白底，和 C# 端一致。 */
  @Builder
  chips() {
    Scroll() {
      Row({ space: 9 }) {
        ForEach(CHIPS, (name: string) => {
          Row({ space: 5 }) {
            Text(name === '全部' ? '📚' : poemEmoji(name)).fontSize(14)
            Text(name)
              .fontSize(14)
              .fontColor(this.category === name ? '#FFFFFF' : '#5D4037')
            Text(`${poemCount(name)}`)
              .fontSize(12)
              .fontColor(this.category === name ? '#FFFFFF' : '#A1887F')
          }
          .padding({ left: 14, right: 14, top: 9, bottom: 9 })
          .borderRadius(18)
          .backgroundColor(this.category === name ? '#5D4037' : '#FFFFFF')
          .onClick(() => {
            this.category = name;
          })
        }, (name: string) => name)
      }
      .padding({ right: 16 })
    }
    .scrollable(ScrollDirection.Horizontal)
    .scrollBar(BarState.Off)
    .width('100%')
    .height(42)
  }

  @Builder
  card(poem: Poem, index: number) {
    Row({ space: 14 }) {
      // 分类色圆 + emoji：和应用磁贴同一套视觉语言
      Stack() {
        Column()
          .width('100%')
          .height('100%')
          .borderRadius('50%')
          .backgroundColor(poemColor(poem.category))

        Text(poemEmoji(poem.category)).fontSize(24)
      }
      .width(52)
      .height(52)

      Column({ space: 3 }) {
        Text(poem.title)
          .fontSize(19)
          .fontWeight(FontWeight.Bold)
          .fontColor('#3E2723')
          .maxLines(1)
          .textOverflow({ overflow: TextOverflow.Ellipsis })

        Text(poem.authorLine())
          .fontSize(13)
          .fontColor('#8D6E63')

        Text(poem.preview())
          .fontSize(13)
          .fontColor('#A1887F')
          .maxLines(1)
          .textOverflow({ overflow: TextOverflow.Ellipsis })
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
      router.poemCategory = this.category;
      router.poemIndex = index;
      router.push(PageId.PoemDetail);
    })
  }
}
