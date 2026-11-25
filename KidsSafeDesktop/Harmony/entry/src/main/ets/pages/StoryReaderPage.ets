import { Backdrop } from '../components/Backdrop';
import { kiosk } from '../core/Kiosk';
import { storyLearned } from '../core/LearnProgress';
import { rewards } from '../core/Rewards';
import { router } from '../core/Router';
import { speech } from '../core/Speech';
import { storyService } from '../core/StoryService';

/** 故事阅读页，对应 QML 的 StoryReaderPage.qml。 */
@ComponentV2
export struct StoryReaderPage {
  @Local learned: boolean = false;

  aboutToAppear(): void {
    storyService.loadDetail(router.storyId);
    this.learned = storyLearned.has(`s${router.storyId}`);
  }

  private markLearned(): void {
    const id: string = `s${router.storyId}`;
    const title: string = storyService.detail !== null ? storyService.detail.title : `故事 ${id}`;
    const first: boolean = storyLearned.mark(id);
    this.learned = true;
    if (first) {
      rewards.grant('story', 4, storyLearned.count(), title);
      this.getUIContext().getPromptAction().showToast({
        message: `读完《${title}》啦！⭐ +4`,
        duration: 1800
      });
    } else {
      this.getUIContext().getPromptAction().showToast({ message: '这本已经读过啦', duration: 1400 });
    }
  }

  aboutToDisappear(): void {
    // 离开页面必须掐掉朗读，否则孩子翻页了声音还在念
    speech.stop();
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

          Blank()

          Row({ space: 6 }) {
            Text(speech.speaking ? '⏹' : '🔊').fontSize(15)
            Text(speech.speaking ? '停止朗读' : '朗读')
              .fontSize(14)
              .fontColor('#6C4BD8')
          }
          .padding({ left: 14, right: 14, top: 9, bottom: 9 })
          .borderRadius(18)
          .backgroundColor('#F1E9FF')
          .onClick(() => {
            this.toggleSpeech();
          })
        }
        .width('100%')

        this.body()
      }
      .width('100%')
      .height('100%')
      .padding({ left: 36, right: 36, top: 26 + kiosk.safeTopVp, bottom: 20 + kiosk.safeBottomVp })
      .constraintSize({ maxWidth: 820 })
    }
    .width('100%')
    .height('100%')
    .alignContent(Alignment.Center)
  }

  @Builder
  body() {
    if (storyService.detailLoading) {
      Column() {
        LoadingProgress().width(48).height(48).color('#7C5CE0')
      }
      .width('100%')
      .layoutWeight(1)
      .justifyContent(FlexAlign.Center)
    } else if (storyService.detail !== null) {
      Scroll() {
        Column({ space: 12 }) {
          Text(storyService.detail.title)
            .fontSize(26)
            .fontWeight(FontWeight.Bold)
            .fontColor('#4A3B32')

          Text(`${storyService.detail.type} · ${storyService.detail.readTime}`)
            .fontSize(12)
            .fontColor('#B8A492')

          Divider().color('#EFE3D6').margin({ top: 6, bottom: 6 })

          Text(storyService.detail.content)
            .fontSize(19)
            .lineHeight(34)
            .fontColor('#4A3B32')

          Row({ space: 8 }) {
            Text(this.learned ? '✅' : '🌙').fontSize(18)
            Text(this.learned ? '这本已经读完啦' : '我读完了')
              .fontSize(16)
              .fontWeight(FontWeight.Medium)
              .fontColor(this.learned ? '#3F51B5' : '#FFFFFF')
          }
          .justifyContent(FlexAlign.Center)
          .width('100%')
          .height(50)
          .borderRadius(18)
          .backgroundColor(this.learned ? '#E8EAF6' : '#3F51B5')
          .margin({ top: 10 })
          .onClick(() => {
            this.markLearned();
          })
        }
        .alignItems(HorizontalAlign.Start)
        .width('100%')
        .padding({ bottom: 30 })
      }
      .scrollBar(BarState.Off)
      .layoutWeight(1)
      .align(Alignment.Top)
    } else {
      Column({ space: 8 }) {
        Text(storyService.errorText.length > 0 ? storyService.errorText : '这个故事打不开')
          .fontSize(17)
          .fontColor('#B8A492')
      }
      .width('100%')
      .layoutWeight(1)
      .justifyContent(FlexAlign.Center)
    }
  }

  private toggleSpeech(): void {
    if (speech.speaking) {
      speech.stop();
      return;
    }
    const detail = storyService.detail;
    if (detail !== null) {
      speech.speak(detail.content);
    }
  }
}
