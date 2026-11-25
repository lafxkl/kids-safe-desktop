import { promptAction } from '@kit.ArkUI';
import { Backdrop } from '../components/Backdrop';
import { kiosk } from '../core/Kiosk';
import { OrderItem, ShopItem, rewards, timeLabel } from '../core/Rewards';
import { PageId, router } from '../core/Router';

/**
 * 星星中心 + 兑换商店。
 *
 * 星星怎么来、能换什么，是两个必须同时讲清楚的事 —— 只显示余额孩子不知道
 * 「星星有什么用」，只显示商品孩子不知道「还差多少」。所以放同一页：
 * 上面余额，下面货架，够不够一眼可见。
 *
 * 兑换物全部由家长在后台配置（家长后台 → 星星商店）。默认给了四件零成本的奖励，
 * 家长不配置也不至于「星星没处花」。
 */

/** 货架卡片高度。之前是「两行固定 168」的算法，卡片会被拉成 346 的长条，见 shelf() 。 */
const CARD_H: number = 136;
const CARD_GAP: number = 10;

@ComponentV2
export struct StarShopPage {
  /**
   * 货架数据只取一次。
   *
   * 之前 ForEach 和「算 Grid 高度」各调一次 visibleShop()，两次结果不一致时
   * 就会出现「按两行留高度、只画一行」—— 卡片被撑到 346vp，中间全是空白。
   * 同一份数据喂给两边，尺寸就永远对得上。
   */
  @Local items: ShopItem[] = [];
  /** 货架可用宽度，窄屏两列、宽屏三列。 */
  @Local paneW: number = 0;

  aboutToAppear(): void {
    rewards.load();
    this.items = rewards.visibleShop();
  }

  build() {
    Stack() {
      Backdrop()

      Column({ space: 10 }) {
        this.header()

        Scroll() {
          Column({ space: 12 }) {
            this.hero()
            this.shelf()
            this.recent()
          }
          .width('100%')
          .padding({ bottom: 24 })
        }
        .scrollBar(BarState.Off)
        .layoutWeight(1)
        // 内容比视口短时 Scroll 会把内容居中，顶上就空一大块；钉死从顶部排
        .align(Alignment.Top)
      }
      .width('100%')
      .height('100%')
      .padding({ left: 20, right: 20, top: 20 + kiosk.safeTopVp, bottom: 14 + kiosk.safeBottomVp })
      .constraintSize({ maxWidth: 780 })
    }
    .width('100%')
    .height('100%')
    .alignContent(Alignment.Center)
  }

  @Builder
  header() {
    Row({ space: 12 }) {
      Text('‹')
        .fontSize(28)
        .fontColor('#7C5CE0')
        .width(34)
        .onClick(() => {
          router.back();
        })

      Text('星星中心')
        .fontSize(22)
        .fontWeight(FontWeight.Bold)
        .fontColor('#4A3B32')

      Blank()

      Row({ space: 5 }) {
        Text('🏆').fontSize(13)
        Text('排行榜').fontSize(12).fontColor('#6C4BD8')
      }
      .padding({ left: 11, right: 11, top: 6, bottom: 6 })
      .borderRadius(13)
      .backgroundColor('#F1E9FF')
      .onClick(() => {
        router.push(PageId.Leaderboard);
      })
    }
    .width('100%')
  }

  @Builder
  hero() {
    Column({ space: 6 }) {
      Row({ space: 8 }) {
        Text('⭐').fontSize(30)
        Column({ space: 0 }) {
          Row({ space: 4 }) {
            Text(`${rewards.stars}`)
              .fontSize(34)
              .fontWeight(FontWeight.Bold)
              .fontColor('#FFFFFF')
            Text('颗星星')
              .fontSize(14)
              .fontColor('#FFF3E0')
              .margin({ bottom: 6 })
          }
          Text(`今天赚了 ${rewards.todayStars} 颗 · 一共赚过 ${rewards.total} 颗`)
            .fontSize(11)
            .fontColor('#FFE7C9')
        }
        .alignItems(HorizontalAlign.Start)
      }
      .width('100%')

      Text('玩游戏、读古诗、看故事，都能赚星星。想换什么，攒够了就来点。')
        .fontSize(11)
        .fontColor('#FFE7C9')
        .width('100%')
    }
    .width('100%')
    .padding(16)
    .borderRadius(20)
    .linearGradient({ angle: 145, colors: [['#FF9A3D', 0.0], ['#F2741F', 1.0]] })
  }

  @Builder
  shelf() {
    Column({ space: 8 }) {
      Row() {
        Text('兑换商店')
          .fontSize(16)
          .fontWeight(FontWeight.Medium)
          .fontColor('#4A3B32')
        Blank()
        Text('由家长准备')
          .fontSize(11)
          .fontColor('#B8A492')
      }
      .width('100%')

      if (this.items.length === 0) {
        Column({ space: 8 }) {
          Text('🎁').fontSize(34)
          Text('货架还空着')
            .fontSize(15)
            .fontColor('#9B8574')
          Text('请爸爸妈妈在「家长后台 → 星星商店」里放几件礼物')
            .fontSize(12)
            .fontColor('#C4B3A2')
            .textAlign(TextAlign.Center)
        }
        .width('100%')
        .padding({ top: 22, bottom: 22 })
        .borderRadius(18)
        .backgroundColor('#FFFFFF')
      } else {
        Grid() {
          ForEach(this.items, (item: ShopItem) => {
            GridItem() {
              this.goods(item)
            }
          }, (item: ShopItem) => item.id)
        }
        .columnsTemplate(this.colsTemplate())
        .columnsGap(CARD_GAP)
        .rowsGap(CARD_GAP)
        .width('100%')
        // 高度按「行数 × 卡片高」显式给，且卡片自己也有固定高：
        // 只给 Grid 高度、让 GridItem 用 100% 撑满，行高会跟着 Grid 走，卡片就被拉长
        .height(this.shelfHeight())
        .scrollBar(BarState.Off)
        .onAreaChange((oldArea: Area, newArea: Area) => {
          const w: number = newArea.width as number;
          if (w <= 0) {
            return;
          }
          // 只在首次量到宽度、或真的换了两列/三列时才写回，免得反复重排
          const nowWide: boolean = w >= 540;
          const wasWide: boolean = this.paneW >= 540;
          if (this.paneW === 0 || nowWide !== wasWide) {
            this.paneW = w;
          }
        })
      }
    }
    .width('100%')
  }

  @Builder
  goods(item: ShopItem) {
    Column({ space: 4 }) {
      Text(item.emoji).fontSize(26)

      Text(item.title)
        .fontSize(13)
        .fontWeight(FontWeight.Medium)
        .fontColor('#4A3B32')
        .maxLines(2)
        .textAlign(TextAlign.Center)
        .lineHeight(17)
        .textOverflow({ overflow: TextOverflow.Ellipsis })

      if (item.note.length > 0) {
        Text(item.note)
          .fontSize(10)
          .fontColor('#B8A492')
          .maxLines(1)
          .textAlign(TextAlign.Center)
          .textOverflow({ overflow: TextOverflow.Ellipsis })
      }

      Blank()

      if (rewards.stars >= item.cost) {
        Text(`⭐ ${item.cost} 兑换`)
          .fontSize(12)
          .fontWeight(FontWeight.Bold)
          .fontColor('#FFFFFF')
          .textAlign(TextAlign.Center)
          .width('100%')
          .height(30)
          .borderRadius(10)
          .backgroundColor('#F0862F')
          .onClick(() => {
            this.ask(item);
          })
      } else {
        Text(`还差 ${item.cost - rewards.stars} 星`)
          .fontSize(11)
          .fontColor('#B8A492')
          .textAlign(TextAlign.Center)
          .width('100%')
          .height(30)
          .borderRadius(10)
          .backgroundColor('#F4EEE7')
      }
    }
    .width('100%')
    .height(CARD_H)
    .padding({ top: 12, bottom: 8, left: 8, right: 8 })
    .borderRadius(18)
    .backgroundColor('#FFFFFF')
  }

  @Builder
  recent() {
    if (rewards.orders.length > 0) {
      Column({ space: 8 }) {
        Text('我换过的')
          .fontSize(14)
          .fontWeight(FontWeight.Medium)
          .fontColor('#4A3B32')
          .width('100%')

        ForEach(rewards.orders.slice(0, 5), (o: OrderItem) => {
          Row({ space: 10 }) {
            Text(o.emoji).fontSize(17)
            Column({ space: 1 }) {
              Text(o.title).fontSize(12).fontColor('#4A3B32')
              Text(timeLabel(o.at)).fontSize(10).fontColor('#B8A492')
            }
            .alignItems(HorizontalAlign.Start)
            .layoutWeight(1)

            Text(o.done ? '已兑现' : '等家长兑现')
              .fontSize(11)
              .fontColor(o.done ? '#2AA7A0' : '#E08A2E')
          }
          .width('100%')
          .padding({ left: 11, right: 11, top: 8, bottom: 8 })
          .borderRadius(13)
          .backgroundColor('#FFFDF9')
        }, (o: OrderItem) => o.id)
      }
      .width('100%')
      .padding(11)
      .borderRadius(18)
      .backgroundColor('#FFFFFF')
    }
  }

  private cols(): number {
    return this.paneW >= 540 ? 3 : 2;
  }

  private colsTemplate(): string {
    return this.cols() === 3 ? '1fr 1fr 1fr' : '1fr 1fr';
  }

  /** 行数 × 卡片高：Grid 在 Scroll 里不写高度会塌成 0，写 1fr 会把一行拉满整块。 */
  private shelfHeight(): number {
    const rows: number = Math.max(1, Math.ceil(this.items.length / this.cols()));
    return rows * CARD_H + (rows - 1) * CARD_GAP;
  }

  private ask(item: ShopItem): void {
    this.getUIContext().getPromptAction().showDialog({
      title: `换「${item.title}」？`,
      message: `会花掉 ${item.cost} 颗星星，换完剩 ${rewards.stars - item.cost} 颗。`,
      buttons: [
        { text: '再想想', color: '#9B8574' },
        { text: '换！', color: '#F0862F' }
      ]
    })
      .then((result: promptAction.ShowDialogSuccessResponse) => {
        if (result.index !== 1) {
          return;
        }
        const ok: boolean = rewards.redeem(item);
        this.getUIContext().getPromptAction().showToast({
          message: ok ? '换好啦！记得找爸爸妈妈拿 🎁' : '星星不够哦',
          duration: 2200
        });
      })
      .catch(() => {
      });
  }
}
