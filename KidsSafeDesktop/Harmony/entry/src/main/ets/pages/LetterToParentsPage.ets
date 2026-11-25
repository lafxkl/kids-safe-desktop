import { Backdrop } from '../components/Backdrop';
import { PageHeader } from '../components/PageHeader';
import { care, CareStore, ParentLetter } from '../core/Care';
import { kiosk } from '../core/Kiosk';
import { router } from '../core/Router';
import { speech } from '../core/Speech';

/**
 * 「想对爸妈说」。对应 C# 的 LetterToParentsPage。
 *
 * 为什么有用：留守儿童最常见的问题不是「不想说」，而是「说不出口」——
 * 电话里往往只剩「嗯」「还好」。先写下来，是把情绪从身体里搬出来；
 * 用 TTS 读出来，则是让孩子听见自己的声音被认真对待。
 *
 * 内容只存本机、不上传、家长面板看不到。这一点必须守住：
 * 一旦孩子怀疑「我妈能看到」，这个功能就废了。
 */

const STARTERS: string[] = [
  '妈妈，我今天学会了一件事：',
  '爸爸，我有句话一直没说：',
  '我这周最想告诉你们的是：',
  '你们不在的时候，我有点怕的是：',
  '我想你们了，我最近过得……',
  '等你们回来，我想和你们一起：'
];

@ComponentV2
export struct LetterToParentsPage {
  private repo: CareStore = care;

  @Local draft: string = '';
  @Local letters: ParentLetter[] = [];
  @Local flash: string = '';
  /** 待确认删除的下标，-1 表示没有 */
  @Local confirming: number = -1;

  aboutToAppear(): void {
    this.reload();
  }

  private reload(): void {
    this.letters = this.repo.letters();
    this.confirming = -1;
  }

  private insertStarter(s: string): void {
    this.draft = this.draft + s;
  }

  private speakDraft(): void {
    const t: string = this.draft.trim();
    if (t.length === 0) {
      this.flash = '先写一句，再读给你听';
      return;
    }
    if (speech.speaking) {
      speech.stop();
      return;
    }
    speech.speak(t);
  }

  private save(): void {
    const t: string = this.draft.trim();
    if (t.length === 0) {
      this.flash = '写一句想对他们说的话吧';
      return;
    }
    this.repo.addLetter(t);
    this.draft = '';
    // 保存后直接念一遍：让孩子听见自己的话被认真读出来，比任何鼓励都具体
    speech.speak(t);
    this.flash = '存好了，只存在这台手机上';
    this.reload();
  }

  private removeAt(i: number): void {
    if (this.confirming !== i) {
      this.confirming = i;
      return;
    }
    this.repo.removeLetter(i);
    this.flash = '已经删掉了';
    this.reload();
  }

  build() {
    Stack() {
      Backdrop()

      Column({ space: 10 }) {
        PageHeader({
          title: '想对爸妈说',
          subtitle: this.letters.length > 0 ? `已经写了 ${this.letters.length} 句` : '写下来，也可以读出来',
          trailing: '',
          onBack: () => {
            router.back();
          }
        })

        Scroll() {
          Column({ space: 12 }) {
            this.composeCard()
            this.listCard()
          }
          .width('100%')
          .padding({ bottom: 12 })
        }
        .width('100%')
        .layoutWeight(1)
        .scrollBar(BarState.Off)
        .align(Alignment.Top)
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
  composeCard() {
    Column({ space: 9 }) {
      Text('不知道怎么写？点一句起个头')
        .fontSize(12)
        .fontColor('#8AA3BC')
        .width('100%')

      Flex({ wrap: FlexWrap.Wrap }) {
        ForEach(STARTERS, (s: string) => {
          Text(s)
            .fontSize(11.5)
            .fontColor('#31597F')
            .padding({ left: 9, right: 9, top: 6, bottom: 6 })
            .borderRadius(11)
            .backgroundColor('#F2F7FF')
            .margin({ right: 6, bottom: 6 })
            .onClick(() => {
              this.insertStarter(s);
            })
        }, (s: string) => s)
      }
      .width('100%')

      TextArea({ text: this.draft, placeholder: '写一句想对他们说的话…' })
        .fontSize(14)
        .height(110)
        .width('100%')
        .borderRadius(14)
        .backgroundColor('#FBFDFF')
        .padding(10)
        .maxLength(200)
        .onChange((v: string) => {
          this.draft = v;
        })

      Row({ space: 9 }) {
        Text(speech.speaking ? '⏹ 停一下' : '🔊 读一遍')
          .fontSize(13.5)
          .fontColor('#31597F')
          .textAlign(TextAlign.Center)
          .layoutWeight(1)
          .height(46)
          .borderRadius(14)
          .backgroundColor('#EAF2FB')
          .onClick(() => {
            this.speakDraft();
          })

        Text('存下来')
          .fontSize(14.5)
          .fontWeight(FontWeight.Bold)
          .fontColor('#FFFFFF')
          .textAlign(TextAlign.Center)
          .layoutWeight(1)
          .height(46)
          .borderRadius(14)
          .backgroundColor('#4A80C0')
          .onClick(() => {
            this.save();
          })
      }
      .width('100%')

      if (this.flash.length > 0) {
        Text(this.flash)
          .fontSize(11.5)
          .fontColor('#3E6A99')
          .width('100%')
          .textAlign(TextAlign.Center)
      }

      Text('这些字只存在这台手机上，不会发给任何人，爸爸妈妈也看不到。' +
        '想让他们听到，就点 🔊 读出来，再念给他们听。')
        .fontSize(10.5)
        .lineHeight(17)
        .fontColor('#9AB0C6')
        .width('100%')
    }
    .width('100%')
    .padding(14)
    .borderRadius(20)
    .backgroundColor('#FFFFFF')
    .alignItems(HorizontalAlign.Start)
  }

  @Builder
  listCard() {
    Column({ space: 8 }) {
      Text('写过的')
        .fontSize(14)
        .fontWeight(FontWeight.Bold)
        .fontColor('#31597F')
        .width('100%')

      if (this.letters.length === 0) {
        Text('还没有写过。想他们的时候，就写一句吧。')
          .fontSize(12.5)
          .fontColor('#8AA3BC')
          .width('100%')
          .textAlign(TextAlign.Center)
          .padding({ top: 16, bottom: 16 })
      } else {
        ForEach(this.letters, (l: ParentLetter, i: number) => {
          Row({ space: 8 }) {
            Column({ space: 4 }) {
              Text(l.text)
                .fontSize(13.5)
                .lineHeight(22)
                .fontColor('#2F4A6B')
                .width('100%')

              Text(`${l.date} ${l.time}`)
                .fontSize(10)
                .fontColor('#9AB0C6')
            }
            .alignItems(HorizontalAlign.Start)
            .layoutWeight(1)

            Column({ space: 3 }) {
              Text('🔊')
                .fontSize(13)
                .textAlign(TextAlign.Center)
                .width(34)
                .height(30)
                .borderRadius(9)
                .backgroundColor('#E3F2FD')
                .onClick(() => {
                  if (speech.speaking) {
                    speech.stop();
                  } else {
                    speech.speak(l.text);
                  }
                })

              Text(this.confirming === i ? '真删' : '🗑')
                .fontSize(this.confirming === i ? 10 : 12)
                .fontColor('#C04848')
                .textAlign(TextAlign.Center)
                .width(34)
                .height(30)
                .borderRadius(9)
                .backgroundColor(this.confirming === i ? '#FFCDD2' : '#FDECEA')
                .onClick(() => {
                  this.removeAt(i);
                })
            }
          }
          .width('100%')
          .padding({ left: 11, right: 11, top: 9, bottom: 9 })
          .borderRadius(15)
          .backgroundColor('#FFFFFF')
          .border({ width: 1, color: '#E3EEFB' })
          .alignItems(VerticalAlign.Top)
        }, (l: ParentLetter, i: number) => `${i}-${l.date}-${l.time}`)
      }
    }
    .width('100%')
    .padding(14)
    .borderRadius(20)
    .backgroundColor('#F7FAFE')
    .alignItems(HorizontalAlign.Start)
  }
}
