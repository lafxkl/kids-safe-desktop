import { Backdrop } from '../components/Backdrop';
import { kiosk } from '../core/Kiosk';
import { PageId, router } from '../core/Router';
import { storyService } from '../core/StoryService';
import { StoryCategory, StorySummary } from '../model/Models';

/** 故事屋，对应 QML 的 StoryHousePage.qml，数据来自家长自建的 KidsSafeServer。 */
@ComponentV2
export struct StoryHousePage {
  @Local keyword: string = '';

  aboutToAppear(): void {
    storyService.refreshOnlineState();
    storyService.loadCategories();
  }

  build() {
    Stack() {
      Backdrop()

      Column({ space: 14 }) {
        Row({ space: 12 }) {
          Text('‹')
            .fontSize(30)
            .fontColor('#7C5CE0')
            .width(36)
            .onClick(() => {
              router.back();
            })

          Column({ space: 2 }) {
            Text('故事屋')
              .fontSize(24)
              .fontWeight(FontWeight.Bold)
              .fontColor('#4A3B32')
            Text('睡前故事 · 童话 · 寓言')
              .fontSize(12)
              .fontColor('#9B8574')
          }
          .alignItems(HorizontalAlign.Start)

          Blank()
        }
        .width('100%')

        Row({ space: 10 }) {
          Search({ value: this.keyword, placeholder: '找故事' })
            .layoutWeight(1)
            .height(46)
            .backgroundColor('#FFFFFF')
            .placeholderColor('#B8A492')
            .onChange((value: string) => {
              this.keyword = value;
            })
            .onSubmit(() => {
              storyService.search(this.keyword);
            })

          Button('搜索')
            .height(46)
            .fontSize(15)
            .backgroundColor('#7C5CE0')
            .onClick(() => {
              storyService.search(this.keyword);
            })
        }
        .width('100%')

        if (storyService.categories.length > 0) {
          Scroll() {
            Row({ space: 10 }) {
              ForEach(storyService.categories, (cat: StoryCategory) => {
                Row({ space: 6 }) {
                  Text(cat.emoji).fontSize(15)
                  Text(cat.name).fontSize(14)
                    .fontColor(storyService.currentTypeId === cat.id ? '#FFFFFF' : '#6C4BD8')
                }
                .padding({ left: 14, right: 14, top: 9, bottom: 9 })
                .borderRadius(18)
                .backgroundColor(storyService.currentTypeId === cat.id ? '#7C5CE0' : '#F1E9FF')
                .onClick(() => {
                  this.keyword = '';
                  storyService.loadList(cat.id, 1);
                })
              }, (cat: StoryCategory) => `cat-${cat.id}`)
            }
            .padding({ right: 20 })
          }
          .scrollable(ScrollDirection.Horizontal)
          .scrollBar(BarState.Off)
          .width('100%')
          .height(44)
        }

        this.body()
      }
      .width('100%')
      .height('100%')
      .padding({ left: 32, right: 32, top: 26 + kiosk.safeTopVp, bottom: 16 })
      .constraintSize({ maxWidth: 1000 })
    }
    .width('100%')
    .height('100%')
    .alignContent(Alignment.Center)
  }

  @Builder
  body() {
    if (!storyService.onlineEnabled) {
      this.tip('家长还没开启在线故事', '在家长后台填上服务器地址就能看了')
    } else if (storyService.loading && storyService.items.length === 0) {
      Column() {
        LoadingProgress().width(48).height(48).color('#7C5CE0')
      }
      .width('100%')
      .layoutWeight(1)
      .justifyContent(FlexAlign.Center)
    } else if (storyService.items.length === 0) {
      this.tip(storyService.errorText.length > 0 ? storyService.errorText : '这里还没有故事', '')
    } else {
      List({ space: 12 }) {
        ForEach(storyService.items, (item: StorySummary) => {
          ListItem() {
            Column({ space: 6 }) {
              Text(item.title)
                .fontSize(17)
                .fontWeight(FontWeight.Medium)
                .fontColor('#4A3B32')
                .maxLines(1)
                .textOverflow({ overflow: TextOverflow.Ellipsis })

              Text(`${item.type} · ${item.readTime} · ${item.length} 字`)
                .fontSize(12)
                .fontColor('#B8A492')

              Text(item.shortDesc)
                .fontSize(13)
                .fontColor('#9B8574')
                .maxLines(2)
                .textOverflow({ overflow: TextOverflow.Ellipsis })
            }
            .alignItems(HorizontalAlign.Start)
            .width('100%')
            .padding(16)
            .borderRadius(18)
            .backgroundColor('#FFFFFF')
            .onClick(() => {
              router.storyId = item.id;
              router.push(PageId.StoryReader);
            })
          }
        }, (item: StorySummary) => `story-${item.id}`)

        if (storyService.hasMore) {
          ListItem() {
            Row() {
              LoadingProgress().width(24).height(24).color('#7C5CE0')
            }
            .width('100%')
            .height(48)
            .justifyContent(FlexAlign.Center)
          }
        }
      }
      .width('100%')
      .layoutWeight(1)
      .scrollBar(BarState.Off)
      .onReachEnd(() => {
        storyService.loadMore();
      })
    }
  }

  @Builder
  tip(title: string, sub: string) {
    Column({ space: 8 }) {
      Text(title).fontSize(18).fontColor('#B8A492')
      if (sub.length > 0) {
        Text(sub).fontSize(13).fontColor('#C4B3A2')
      }
    }
    .width('100%')
    .layoutWeight(1)
    .justifyContent(FlexAlign.Center)
  }
}
