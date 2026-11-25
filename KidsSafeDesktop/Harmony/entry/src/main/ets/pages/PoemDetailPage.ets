import { Backdrop } from '../components/Backdrop';
import { PageHeader } from '../components/PageHeader';
import { kiosk } from '../core/Kiosk';
import { poemLearned } from '../core/LearnProgress';
import { rewards } from '../core/Rewards';
import { router } from '../core/Router';
import { speech } from '../core/Speech';
import { Poem, poemsByCategory, poemColor, poemEmoji } from '../data/PoemData';

/**
 * 古诗详情，对应 C# 的 PoemDetailPage：正文大字 + 字词注释 + 白话译文 + 小知识，
 * 可朗读、可翻上一首 / 下一首。
 *
 * 「上一首/下一首」只在当前筛选结果里翻，和列表顺序一致——
 * 从「四季风光」点进来的，翻到底也不会跳到别的分类去。
 */
@ComponentV2
export struct PoemDetailPage {
  /** 当前下标。翻页只是改这个值，整个页面按它重渲染。 */
  @Local index: number = 0;
  /** 这一首之前学会没有。学会按钮据此换文案，避免反复发星。 */
  @Local learned: boolean = false;
  private list: Poem[] = [];

  aboutToAppear(): void {
    this.list = poemsByCategory(router.poemCategory);
    const want: number = router.poemIndex;
    if (this.list.length === 0) {
      this.index = 0;
    } else if (want < 0) {
      this.index = 0;
    } else if (want >= this.list.length) {
      this.index = this.list.length - 1;
    } else {
      this.index = want;
    }
    this.refreshLearned();
  }

  private refreshLearned(): void {
    this.learned = this.list.length > 0 && poemLearned.has(this.current().title);
  }

  /**
   * 学会一首。星星只发第一次 —— 孩子会把「我背会了」当按钮一直按，
   * 每按一次都掉星星的话，排行榜就没意义了。
   */
  private markLearned(): void {
    if (this.list.length === 0) {
      return;
    }
    const title: string = this.current().title;
    const first: boolean = poemLearned.mark(title);
    this.learned = true;
    if (first) {
      const n: number = poemLearned.count();
      rewards.grant('poem', 4, n, title);
      this.getUIContext().getPromptAction().showToast({
        message: `学会《${title}》啦！⭐ +4`,
        duration: 1800
      });
    } else {
      this.getUIContext().getPromptAction().showToast({ message: '这首已经学会啦', duration: 1400 });
    }
  }

  aboutToDisappear(): void {
    // 离开页面必须掐掉朗读，否则孩子翻走了声音还在念
    speech.stop();
  }

  build() {
    Stack() {
      Backdrop()

      Column({ space: 12 }) {
        PageHeader({
          title: this.list.length > 0 ? this.current().title : '古诗',
          subtitle: this.list.length > 0 ? this.current().authorLine() : '',
          trailing: this.list.length > 0 ? `${this.index + 1} / ${this.list.length}` : '',
          onBack: () => {
            router.back();
          }
        })

        if (this.list.length === 0) {
          Column() {
            Text('这里还没有古诗').fontSize(17).fontColor('#B8A492')
          }
          .width('100%')
          .layoutWeight(1)
          .justifyContent(FlexAlign.Center)
        } else {
          this.content()
          this.bottomBar()
        }
      }
      .width('100%')
      .height('100%')
      .padding({ left: 32, right: 32, top: 26 + kiosk.safeTopVp, bottom: 12 + kiosk.safeBottomVp })
      .constraintSize({ maxWidth: 820 })
    }
    .width('100%')
    .height('100%')
    .alignContent(Alignment.Center)
  }

  @Builder
  content() {
    Scroll() {
      Column({ space: 14 }) {
        // 标题区：诗名居中，下面是分类小标签
        Column({ space: 6 }) {
          Text(this.current().title)
            .fontSize(30)
            .fontWeight(FontWeight.Bold)
            .fontColor('#3E2723')
            .textAlign(TextAlign.Center)

          Text(this.current().authorLine())
            .fontSize(15)
            .fontColor('#8D6E63')

          Row({ space: 5 }) {
            Text(poemEmoji(this.current().category)).fontSize(12)
            Text(this.current().category).fontSize(12).fontColor('#FFFFFF')
          }
          .padding({ left: 12, right: 12, top: 4, bottom: 4 })
          .borderRadius(12)
          .backgroundColor(poemColor(this.current().category))
        }
        .width('100%')
        .alignItems(HorizontalAlign.Center)
        .margin({ top: 4, bottom: 2 })

        // 正文：逐句居中，行距放大，孩子指读不串行
        Column({ space: 10 }) {
          ForEach(this.current().lines, (line: string) => {
            Text(line)
              .fontSize(24)
              .lineHeight(38)
              .fontColor('#3E2723')
              .textAlign(TextAlign.Center)
              .width('100%')
          }, (line: string, i: number) => `${i}-${line}`)
        }
        .width('100%')
        .padding({ left: 22, right: 22, top: 22, bottom: 22 })
        .borderRadius(22)
        .backgroundColor('#FFFBF3')
        .border({ width: 1, color: '#EFE3D0' })

        // 注释
        Column({ space: 8 }) {
          Text('📝 字词注释').fontSize(16).fontWeight(FontWeight.Bold).fontColor('#5D4037')
          Column({ space: 7 }) {
            ForEach(this.current().notes, (note: string) => {
              Text(note)
                .fontSize(14.5)
                .lineHeight(23)
                .fontColor('#6D4C41')
                .width('100%')
            }, (note: string, i: number) => `n-${i}`)
          }
          .alignItems(HorizontalAlign.Start)
          .width('100%')
        }
        .alignItems(HorizontalAlign.Start)
        .width('100%')
        .padding({ left: 18, right: 18, top: 16, bottom: 16 })
        .borderRadius(20)
        .backgroundColor('#FFFFFF')

        // 译文
        Column({ space: 8 }) {
          Text('🗒 白话译文').fontSize(16).fontWeight(FontWeight.Bold).fontColor('#5D4037')
          Text(this.current().translation)
            .fontSize(15)
            .lineHeight(26)
            .fontColor('#4E342E')
            .width('100%')
        }
        .alignItems(HorizontalAlign.Start)
        .width('100%')
        .padding({ left: 18, right: 18, top: 16, bottom: 16 })
        .borderRadius(20)
        .backgroundColor('#FFFFFF')

        // 小知识，用暖橙底和上面几张白卡区分开
        Column({ space: 8 }) {
          Text('💡 小知识').fontSize(16).fontWeight(FontWeight.Bold).fontColor('#E65100')
          Text(this.current().appreciation)
            .fontSize(15)
            .lineHeight(26)
            .fontColor('#6D4C41')
            .width('100%')
        }
        .alignItems(HorizontalAlign.Start)
        .width('100%')
        .padding({ left: 18, right: 18, top: 16, bottom: 16 })
        .borderRadius(20)
        .backgroundColor('#FFF4E5')
        .border({ width: 1, color: '#FFE0B2' })

        Text('读三遍，试着背给爸爸妈妈听～')
          .fontSize(13)
          .fontColor('#A1887F')
          .width('100%')
          .textAlign(TextAlign.Center)
          .margin({ top: 4, bottom: 8 })

        // 学会按钮：把「读过了」变成一件有结果的事。没有这个动作，
        // 古诗页就只是个阅读器，星星也永远发不出去。
        Row({ space: 8 }) {
          Text(this.learned ? '✅' : '📖').fontSize(18)
          Text(this.learned ? '已经学会这首啦' : '我背会了')
            .fontSize(16)
            .fontWeight(FontWeight.Medium)
            .fontColor(this.learned ? '#2AA7A0' : '#FFFFFF')
        }
        .justifyContent(FlexAlign.Center)
        .width('100%')
        .height(50)
        .borderRadius(18)
        .backgroundColor(this.learned ? '#E4F5F4' : '#2AA7A0')
        .margin({ top: 2, bottom: 10 })
        .onClick(() => {
          this.markLearned();
        })
      }
      .width('100%')
      .padding({ bottom: 8 })
    }
    .width('100%')
    .layoutWeight(1)
    .scrollBar(BarState.Off)
    .align(Alignment.Top)
  }

  @Builder
  bottomBar() {
    Row({ space: 10 }) {
      Button('⏮ 上一首')
        .fontSize(15)
        .fontWeight(FontWeight.Medium)
        .fontColor('#5D4037')
        .backgroundColor('#FFFFFF')
        .borderRadius(16)
        .height(48)
        .layoutWeight(1)
        .enabled(this.index > 0)
        .opacity(this.index > 0 ? 1 : 0.4)
        .onClick(() => {
          this.go(-1);
        })

      // 朗读按钮文案跟着引擎状态走，孩子一眼知道现在是「念」还是「停」
      Button(speech.speaking ? '⏹ 停止朗读' : '🔊 朗读一遍')
        .fontSize(16)
        .fontWeight(FontWeight.Bold)
        .fontColor('#FFFFFF')
        .backgroundColor(speech.speaking ? '#B39DDB' : '#8D6E63')
        .borderRadius(16)
        .height(48)
        .layoutWeight(1.3)
        .onClick(() => {
          this.toggleSpeech();
        })

      Button('下一首 ⏭')
        .fontSize(15)
        .fontWeight(FontWeight.Medium)
        .fontColor('#5D4037')
        .backgroundColor('#FFFFFF')
        .borderRadius(16)
        .height(48)
        .layoutWeight(1)
        .enabled(this.index < this.list.length - 1)
        .opacity(this.index < this.list.length - 1 ? 1 : 0.4)
        .onClick(() => {
          this.go(1);
        })
    }
    .width('100%')
  }

  private go(delta: number): void {
    const next: number = this.index + delta;
    if (next < 0 || next >= this.list.length) {
      return;
    }
    // 换诗就停掉上一首的朗读，否则会接着念旧内容
    speech.stop();
    this.index = next;
    router.poemIndex = next;
    this.refreshLearned();
  }

  private toggleSpeech(): void {
    if (speech.speaking) {
      speech.stop();
      return;
    }
    speech.speak(this.current().speakText());
  }

  private current(): Poem {
    return this.list[this.index];
  }
}
