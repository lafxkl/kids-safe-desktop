import { common, Want } from '@kit.AbilityKit';
import { Backdrop } from '../components/Backdrop';
import { PageHeader } from '../components/PageHeader';
import { care, CareStore, MoodEntry, MoodSaved, shortDay } from '../core/Care';
import { kiosk } from '../core/Kiosk';
import { PageId, router } from '../core/Router';
import { speech } from '../core/Speech';
import { CHANNELS, HelpChannel, MOODS, MoodKind, WARM_WORDS, moodOf } from '../data/CareData';

/**
 * 关怀中心首页。对应 C# 的 CareCenterPage。
 *
 * 设计上刻意「低门槛」：进来第一件事是点一个表情，不需要打字、不需要登录、
 * 不需要回答任何问题。想说的话可以补，但永远不是必填 ——
 * 一个刚被批评完的孩子，不愿意再面对一屏要填的东西。
 *
 * 和 C# 端的一处差别：那边点完心情弹输入框问「想多说一句吗」，
 * 这边改成**先记录、再在卡片里展开输入行**。理由是弹窗会打断「我记完了」这件事，
 * 而星星已经到账，孩子可以直接走开，愿意说再说。
 */

/** 近 7 天的一格。用类而不是 null，模板里就不用到处判空。 */
class DayCell {
  emoji: string = '';
  color: string = '#F1EFF3';
  has: boolean = false;
  label: string = '';
}

@ComponentV2
export struct CareCenterPage {
  private repo: CareStore = care;

  @Local streak: number = 0;
  /** 今天记的心情 id，空串表示还没记 */
  @Local moodId: string = '';
  @Local note: string = '';
  @Local greet: string = '今天心情怎么样？';
  @Local hint: string = '点一个最像你的表情';
  /** 「已记下 +1 ⭐」这类一次性提示 */
  @Local flash: string = '';
  @Local days: DayCell[] = [];
  @Local warm: string = '';
  /** 点完心情后展开的补一句话输入行 */
  @Local askNote: boolean = false;
  @Local draftNote: string = '';
  /** 拨号拉不起来时的提示 */
  @Local dialTip: string = '';

  aboutToAppear(): void {
    this.refresh();
    this.nextWarm();
  }

  private refresh(): void {
    const today: MoodEntry | null = this.repo.todayEntry();
    if (today === null) {
      this.moodId = '';
      this.note = '';
      this.greet = '今天心情怎么样？';
      this.hint = '点一个最像你的表情';
    } else {
      this.moodId = today.moodId;
      this.note = today.note;
      const m: MoodKind = moodOf(today.moodId);
      this.greet = `今天：${m.emoji} ${m.label}`;
      this.hint = today.note.length > 0 ? `“${today.note}”` : '想换一个心情，随时点就行';
    }

    this.streak = this.repo.streak();

    const raw: (MoodEntry | null)[] = this.repo.recent(7);
    const cells: DayCell[] = [];
    for (let i = 0; i < raw.length; i++) {
      const e: MoodEntry | null = raw[i];
      const c: DayCell = new DayCell();
      if (e === null) {
        c.emoji = '·';
        c.color = '#F1EFF3';
        c.has = false;
        c.label = '';
      } else {
        const m: MoodKind = moodOf(e.moodId);
        c.emoji = m.emoji;
        c.color = m.hex;
        c.has = true;
        c.label = shortDay(e.date);
      }
      // 没记录的格子也要有日期标签，否则最后一天看不出是「今天」
      if (c.label.length === 0) {
        const tail: string = i === raw.length - 1 ? '今天' : '';
        c.label = tail;
      }
      cells.push(c);
    }
    this.days = cells;
  }

  private tapMood(m: MoodKind): void {
    const saved: MoodSaved = this.repo.saveMood(m.id, this.note);
    this.flash = saved.firstToday
      ? `已记下 ${m.emoji}${m.label}　+${saved.stars} ⭐`
      : `已改成 ${m.emoji}${m.label}`;
    // 没记过今天才问要不要补一句；改心情时不问，避免每次都弹输入行
    this.askNote = saved.firstToday;
    this.draftNote = this.note;
    this.refresh();
  }

  private saveNote(): void {
    const t: string = this.draftNote.trim();
    if (t.length === 0) {
      this.askNote = false;
      return;
    }
    // 第二次 saveMood 不会再发星（firstToday 为 false），只覆盖附言
    this.repo.saveMood(this.moodId, t);
    this.askNote = false;
    this.flash = '这句话存好了，只有你自己能看到';
    this.refresh();
  }

  private nextWarm(): void {
    if (WARM_WORDS.length === 0) {
      return;
    }
    let next: string = WARM_WORDS[Math.floor(Math.random() * WARM_WORDS.length)];
    // 只有一句、或恰好抽到同一句时重抽，避免点「换一句」没反应
    if (next === this.warm && WARM_WORDS.length > 1) {
      const at: number = WARM_WORDS.indexOf(next);
      next = WARM_WORDS[(at + 1) % WARM_WORDS.length];
    }
    this.warm = next;
  }

  /**
   * 拉起系统拨号盘，而不是直接外拨。
   * 直接拨要 ohos.permission.PLACE_CALL（普通应用拿不到），
   * 而且孩子误触就拨出去也不合适 —— 让他自己按最后一下绿色键。
   */
  private dial(number: string): void {
    this.dialTip = '';
    try {
      const ctx = getContext(this) as common.UIAbilityContext;
      const want: Want = {
        action: 'ohos.want.action.dial',
        uri: `tel:${number}`
      };
      ctx.startAbility(want).catch(() => {
        this.dialTip = `打不开拨号，请手动拨打 ${number}`;
      });
    } catch (err) {
      this.dialTip = `打不开拨号，请手动拨打 ${number}`;
    }
  }

  build() {
    Stack() {
      Backdrop()

      Column({ space: 10 }) {
        PageHeader({
          title: '关怀中心',
          subtitle: this.streak > 0 ? `已经连着记了 ${this.streak} 天` : '慢慢来，什么时候都可以',
          trailing: this.streak > 0 ? `🔥 ${this.streak} 天` : '',
          onBack: () => {
            router.back();
          }
        })

        Scroll() {
          Column({ space: 12 }) {
            this.moodCard()
            this.warmCard()
            this.entriesCard()
            this.channelCard()
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
  moodCard() {
    Column({ space: 10 }) {
      Row({ space: 8 }) {
        Column({ space: 2 }) {
          Text(this.greet)
            .fontSize(19)
            .fontWeight(FontWeight.Bold)
            .fontColor('#5D3A47')

          Text(this.hint)
            .fontSize(12)
            .fontColor('#A1858F')
            .maxLines(2)
            .textOverflow({ overflow: TextOverflow.Ellipsis })
        }
        .alignItems(HorizontalAlign.Start)
        .layoutWeight(1)

        if (this.flash.length > 0) {
          Text(this.flash)
            .fontSize(11)
            .fontColor('#C2185B')
            .padding({ left: 8, right: 8, top: 4, bottom: 4 })
            .borderRadius(10)
            .backgroundColor('#FCE4EC')
            .maxLines(2)
            .constraintSize({ maxWidth: 150 })
        }
      }
      .width('100%')
      .alignItems(VerticalAlign.Top)

      // Flex 没有 rowGap（那是 Grid 的），行距靠子元素自己的 margin 撑开
      Flex({ wrap: FlexWrap.Wrap, justifyContent: FlexAlign.SpaceBetween }) {
        ForEach(MOODS, (m: MoodKind) => {
          this.moodCell(m)
        }, (m: MoodKind) => m.id)
      }
      .width('100%')

      if (this.askNote) {
        Row({ space: 8 }) {
          TextInput({ text: this.draftNote, placeholder: '想多说一句吗？不写也可以' })
            .fontSize(13)
            .layoutWeight(1)
            .height(42)
            .borderRadius(12)
            .backgroundColor('#FFF7FA')
            .padding({ left: 12, right: 12 })
            .maxLength(40)
            .onChange((v: string) => {
              this.draftNote = v;
            })

          Text('存')
            .fontSize(14)
            .fontWeight(FontWeight.Bold)
            .fontColor('#FFFFFF')
            .textAlign(TextAlign.Center)
            .width(52)
            .height(42)
            .borderRadius(12)
            .backgroundColor('#EC6E93')
            .onClick(() => {
              this.saveNote();
            })
        }
        .width('100%')
      }

      Row({ space: 5 }) {
        ForEach(this.days, (d: DayCell, i: number) => {
          Column({ space: 3 }) {
            Stack() {
              Text(d.emoji)
                .fontSize(15)
                .fontColor(d.has ? '#FFFFFF' : '#C9C3CE')
            }
            .width(32)
            .height(32)
            .borderRadius(16)
            .backgroundColor(d.color)
            .alignContent(Alignment.Center)

            Text(d.label)
              .fontSize(9)
              .fontColor('#C0B2BA')
          }
          .layoutWeight(1)
        }, (d: DayCell, i: number) => `${i}-${d.label}`)
      }
      .width('100%')
      .margin({ top: 2 })

      if (this.note.length > 0 && !this.askNote) {
        Row({ space: 6 }) {
          Text('📝').fontSize(12)
          Text(this.note)
            .fontSize(12)
            .fontColor('#8A7B84')
            .layoutWeight(1)
            .maxLines(2)
            .textOverflow({ overflow: TextOverflow.Ellipsis })
        }
        .width('100%')
        .padding({ left: 10, right: 10, top: 6, bottom: 6 })
        .borderRadius(10)
        .backgroundColor('#FBF4F7')
      }
    }
    .width('100%')
    .padding(14)
    .borderRadius(20)
    .backgroundColor('#FFFFFF')
    .alignItems(HorizontalAlign.Start)
  }

  @Builder
  moodCell(m: MoodKind) {
    Column({ space: 3 }) {
      Text(m.emoji).fontSize(26)

      Text(m.label)
        .fontSize(11.5)
        .fontColor('#6D4C58')
    }
    .width('23%')
    .padding({ top: 9, bottom: 9 })
    .borderRadius(15)
    .backgroundColor(this.moodId === m.id ? m.hex : '#FFF7FA')
    .border({ width: this.moodId === m.id ? 2 : 0, color: '#D98CA6' })
    // Flex 换行后行距只能靠 margin 撑，见 moodCard 里的注释
    .margin({ bottom: 8 })
    .onClick(() => {
      this.tapMood(m);
    })
  }

  @Builder
  warmCard() {
    Column({ space: 8 }) {
      Row({ space: 6 }) {
        Text('💌').fontSize(16)

        Text('给你的话')
          .fontSize(14)
          .fontWeight(FontWeight.Bold)
          .fontColor('#5D3A47')
          .layoutWeight(1)

        Text('换一句')
          .fontSize(12)
          .fontColor('#B4577C')
          .padding({ left: 8, right: 8, top: 3, bottom: 3 })
          .borderRadius(9)
          .backgroundColor('#FCE4EC')
          .onClick(() => {
            this.nextWarm();
          })
      }
      .width('100%')

      Text(`“${this.warm}”`)
        .fontSize(16)
        .lineHeight(26)
        .fontColor('#4E3B45')
        .width('100%')

      Row({ space: 6 }) {
        Text(speech.speaking ? '⏹ 停一下' : '🔊 读给我听')
          .fontSize(12)
          .fontColor('#31597F')
          .padding({ left: 10, right: 10, top: 5, bottom: 5 })
          .borderRadius(10)
          .backgroundColor('#EAF2FB')
          .onClick(() => {
            if (speech.speaking) {
              speech.stop();
            } else {
              speech.speak(this.warm);
            }
          })
      }
      .width('100%')
    }
    .width('100%')
    .padding(14)
    .borderRadius(20)
    .backgroundColor('#FFFFFF')
    .alignItems(HorizontalAlign.Start)
  }

  @Builder
  entriesCard() {
    Column({ space: 10 }) {
      Text('还想做点什么')
        .fontSize(14)
        .fontWeight(FontWeight.Bold)
        .fontColor('#5D3A47')
        .width('100%')

      Flex({ wrap: FlexWrap.Wrap, justifyContent: FlexAlign.SpaceBetween }) {
        this.entry('📖', '心情日记', '看看这些天的变化', PageId.MoodJournal, '#FFF3F8')
        this.entry('📚', '心理小课堂', '20 节，挑一节', PageId.CareLesson, '#F1F7FF')
        this.entry('🌬️', '呼吸放松', '心里堵的时候', PageId.Breathing, '#F0FAF3')
        this.entry('✉️', '想对爸妈说', '写下来，能读出来', PageId.LetterToParents, '#FFF8EC')
        this.entry('🖼️', '绘本怎么找', '免费合法看童书', PageId.Reading, '#F3F7EC')
      }
      .width('100%')
    }
    .width('100%')
    .padding(14)
    .borderRadius(20)
    .backgroundColor('#FFFFFF')
    .alignItems(HorizontalAlign.Start)
  }

  @Builder
  entry(emoji: string, title: string, sub: string, target: PageId, bg: string) {
    Column({ space: 3 }) {
      Text(emoji).fontSize(22)

      Text(title)
        .fontSize(13.5)
        .fontWeight(FontWeight.Bold)
        .fontColor('#4E3B45')

      Text(sub)
        .fontSize(10.5)
        .fontColor('#A1858F')
        .maxLines(1)
        .textOverflow({ overflow: TextOverflow.Ellipsis })
    }
    .width('48.5%')
    .padding({ left: 12, right: 12, top: 11, bottom: 11 })
    .borderRadius(15)
    .backgroundColor(bg)
    .margin({ bottom: 8 })
    .alignItems(HorizontalAlign.Start)
    .onClick(() => {
      if (target === PageId.CareLesson) {
        router.careAudience = 'kids';
      }
      router.push(target);
    })
  }

  @Builder
  channelCard() {
    Column({ space: 8 }) {
      Text('如果难受，可以找人说说')
        .fontSize(14)
        .fontWeight(FontWeight.Bold)
        .fontColor('#5D3A47')
        .width('100%')

      Text('不要一个人扛着。打电话是免费的，接电话的叔叔阿姨会好好听你说。')
        .fontSize(11.5)
        .lineHeight(18)
        .fontColor('#A1858F')
        .width('100%')

      ForEach(CHANNELS, (c: HelpChannel) => {
        this.channelRow(c)
      }, (c: HelpChannel) => c.name)

      if (this.dialTip.length > 0) {
        Text(this.dialTip)
          .fontSize(11.5)
          .fontColor('#C62828')
          .width('100%')
      }
    }
    .width('100%')
    .padding(14)
    .borderRadius(20)
    .backgroundColor('#FFFFFF')
    .alignItems(HorizontalAlign.Start)
  }

  @Builder
  channelRow(c: HelpChannel) {
    Row({ space: 9 }) {
      Text(c.emoji).fontSize(18)

      Column({ space: 1 }) {
        Text(c.urgent ? `${c.name}（紧急）` : c.name)
          .fontSize(13)
          .fontWeight(FontWeight.Bold)
          .fontColor(c.urgent ? '#C62828' : '#4E3B45')

        Text(c.when)
          .fontSize(11)
          .lineHeight(16)
          .fontColor('#8A7B84')
      }
      .alignItems(HorizontalAlign.Start)
      .layoutWeight(1)

      if (c.number.length > 0) {
        Text('拨打')
          .fontSize(12)
          .fontColor(c.urgent ? '#B71C1C' : '#1565C0')
          .padding({ left: 10, right: 10, top: 5, bottom: 5 })
          .borderRadius(11)
          .backgroundColor(c.urgent ? '#FFCDD2' : '#E3F2FD')
          .onClick(() => {
            this.dial(c.number);
          })
      }
    }
    .width('100%')
    .padding({ left: 10, right: 10, top: 8, bottom: 8 })
    .borderRadius(13)
    .backgroundColor(c.urgent ? '#FFF5F5' : '#F7F9FC')
    .alignItems(VerticalAlign.Top)
  }
}
