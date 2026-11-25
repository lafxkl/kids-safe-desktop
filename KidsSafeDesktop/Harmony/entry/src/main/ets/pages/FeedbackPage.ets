import { router } from '../core/Router';
import { feedback, FeedbackResult } from '../core/Feedback';
import { PageHeader } from '../components/PageHeader';

class DimDef {
  key: string = '';
  label: string = '';

  constructor(key: string, label: string) {
    this.key = key;
    this.label = label;
  }
}

class QDef {
  id: string = '';
  title: string = '';
  options: string[] = [];

  constructor(id: string, title: string, options: string[]) {
    this.id = id;
    this.title = title;
    this.options = options;
  }
}

/**
 * 一个小方块。
 *
 * 做成组件而不是 @Builder：@Builder 的参数不支持函数类型，
 * 把点击回调用 @Event 传进来才是 ArkUI 的标准写法。
 */
@ComponentV2
struct ChipView {
  @Param text: string = '';
  @Param on: boolean = false;
  @Param wide: boolean = false;
  @Event onTap: () => void = () => {
  };

  build() {
    Text(this.text)
      .fontSize(this.wide ? 14 : 13)
      .fontWeight(FontWeight.Medium)
      .fontColor(this.on ? '#FFFFFF' : '#7C6353')
      .backgroundColor(this.on ? '#E2603D' : '#F6EAD9')
      .borderRadius(10)
      .textAlign(TextAlign.Center)
      .layoutWeight(this.wide ? 1 : 0)
      .padding({ left: 13, right: 13, top: 7, bottom: 7 })
      .onClick(() => {
        this.onTap();
      })
  }
}

@ComponentV2
struct TabView {
  @Param text: string = '';
  @Param on: boolean = false;
  @Event onTap: () => void = () => {
  };

  build() {
    Text(this.text)
      .fontSize(14)
      .fontWeight(FontWeight.Medium)
      .fontColor(this.on ? '#FFFFFF' : '#7C6353')
      .backgroundColor(this.on ? '#E2603D' : '#F6EAD9')
      .borderRadius(12)
      .layoutWeight(1)
      .textAlign(TextAlign.Center)
      .padding({ top: 10, bottom: 10 })
      .onClick(() => {
        this.onTap();
      })
  }
}

/**
 * 意见反馈 / 满意度 / 问卷（鸿蒙端）。
 *
 * 三个标签共用一条提交通道（core/Feedback），数据进官网，
 * 与 Windows / 安卓 / Linux 同一套接口、同一份后台统计。
 */
@ComponentV2
export struct FeedbackPage {
  @Local tab: number = 0;

  // 满意度
  @Local rating: number = 0;
  @Local nps: number = -1;
  @Local dimScores: number[] = [0, 0, 0, 0, 0];
  @Local picked: boolean[] = [false, false, false, false, false, false, false, false];
  @Local satText: string = '';

  // 问卷
  @Local answers: string[] = ['', '', '', '', '', ''];
  @Local surveyText: string = '';

  // 意见反馈
  @Local fbCat: string = '问题反馈';
  @Local fbText: string = '';

  // 公共
  @Local contact: string = '';
  @Local allowPublic: boolean = false;
  @Local busy: boolean = false;
  @Local status: string = '';
  @Local pending: number = 0;

  private readonly stars: number[] = [1, 2, 3, 4, 5];
  private readonly npsValues: number[] = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10];
  private readonly dimDefs: DimDef[] = [
    new DimDef('ease', '好不好上手'),
    new DimDef('content', '内容合不合适'),
    new DimDef('stable', '稳不稳定'),
    new DimDef('design', '界面好不好看'),
    new DimDef('value', '值不值得推荐')
  ];
  private readonly likeTags: string[] = ['护眼提醒', '小游戏', '故事屋', '学习内容', '家长管控', '日记本', '关爱中心', '开机自启'];
  private readonly cats: string[] = ['问题反馈', '功能建议', '内容纠错', '使用疑问', '其他'];
  private readonly questions: QDef[] = [
    new QDef('孩子年龄', '孩子现在多大？', ['3–5 岁', '6–8 岁', '9–12 岁', '12 岁以上']),
    new QDef('主要设备', '主要在什么设备上用？', ['Windows 电脑', '安卓平板', '安卓手机', 'Linux', '鸿蒙']),
    new QDef('使用频率', '大概多久用一次？', ['几乎每天', '每周几次', '偶尔用', '刚装上']),
    new QDef('最常用', '孩子最常用哪一块？', ['小游戏', '学习/口算', '故事屋', '关爱中心', '家长管控', '日记本']),
    new QDef('最希望新增', '最希望我们下一步做什么？', ['更多课程', '更多小游戏', '学习报告', '多孩管理', '离线内容', '其他']),
    new QDef('愿意推荐吗', '愿意推荐给别的家长吗？', ['愿意', '看情况', '不愿意'])
  ];

  aboutToAppear(): void {
    this.pending = feedback.pendingCount();
    // 启动时顺手补传上次没发出去的；失败只会更新状态文字
    feedback.flush().then((r: FeedbackResult) => {
      this.pending = feedback.pendingCount();
      if (r.message.length > 0) {
        this.status = r.message;
      }
    });
  }

  private answeredCount(): number {
    let n = 0;
    for (let i = 0; i < this.answers.length; i++) {
      if (this.answers[i].length > 0) {
        n++;
      }
    }
    return n;
  }

  private async send(): Promise<void> {
    if (this.busy) {
      return;
    }

    if (this.tab === 0 && this.rating < 1) {
      this.status = '先点一下整体满意度（1–5 星）';
      return;
    }
    if (this.tab === 1 && this.answeredCount() < 3) {
      this.status = '问卷至少选 3 题，我们才好判断';
      return;
    }
    if (this.tab === 2 && this.fbText.trim().length < 5) {
      this.status = '再多写几个字吧（至少 5 个字）';
      return;
    }

    this.busy = true;
    try {
      let result: FeedbackResult;
      if (this.tab === 0) {
        const dims: Record<string, number> = {};
        this.dimDefs.forEach((d: DimDef, i: number) => {
          if (this.dimScores[i] > 0) {
            dims[d.key] = this.dimScores[i];
          }
        });
        const tags: string[] = [];
        this.likeTags.forEach((t: string, i: number) => {
          if (this.picked[i]) {
            tags.push(t);
          }
        });
        result = await feedback.submit(feedback.satisfaction(this.rating, this.nps, dims, tags,
          this.satText, this.contact, this.allowPublic));
      } else if (this.tab === 1) {
        const map: Record<string, string> = {};
        this.questions.forEach((q: QDef, i: number) => {
          if (this.answers[i].length > 0) {
            map[q.id] = this.answers[i];
          }
        });
        result = await feedback.submit(feedback.survey(map, this.surveyText, this.contact, this.allowPublic));
      } else {
        result = await feedback.submit(feedback.feedback(this.fbCat, this.fbText, this.contact, this.allowPublic));
        if (result.ok) {
          this.fbText = '';
        }
      }

      this.status = result.message;
      this.pending = feedback.pendingCount();
    } finally {
      this.busy = false;
    }
  }

  build() {
    Column({ space: 10 }) {
      PageHeader({
        title: '意见反馈与问卷',
        subtitle: this.pending > 0 ? `本机还有 ${this.pending} 条待补传` : '数据会同步到官网统计',
        onBack: () => {
          router.back();
        }
      })
        .padding({ left: 20, right: 20, top: 12 })

      Scroll() {
        Column({ space: 14 }) {
          Row({ space: 8 }) {
            TabView({
              text: '满意度调查', on: this.tab === 0, onTap: () => {
                this.tab = 0;
                this.status = '';
              }
            })
            TabView({
              text: '使用问卷', on: this.tab === 1, onTap: () => {
                this.tab = 1;
                this.status = '';
              }
            })
            TabView({
              text: '意见反馈', on: this.tab === 2, onTap: () => {
                this.tab = 2;
                this.status = '';
              }
            })
          }
          .width('100%')

          if (this.tab === 0) {
            this.satisfactionPane()
          } else if (this.tab === 1) {
            this.surveyPane()
          } else {
            this.feedbackPane()
          }

          TextInput({ text: this.contact, placeholder: '联系方式（选填，只有开发者能看到）' })
            .onChange((v: string) => {
              this.contact = v;
            })
            .height(46)
            .borderRadius(12)
            .backgroundColor('#FFFFFF')

          Row({ space: 10 }) {
            Text('允许匿名展示在官网（只显示正文、星级和月份）')
              .fontSize(13)
              .fontColor('#8D6E63')
              .layoutWeight(1)
            Toggle({ type: ToggleType.Switch, isOn: this.allowPublic })
              .selectedColor('#E2603D')
              .onChange((on: boolean) => {
                this.allowPublic = on;
              })
          }
          .width('100%')

          Button(this.busy ? '提交中…' : '提交')
            .width('100%')
            .height(50)
            .borderRadius(25)
            .backgroundColor(this.busy ? '#F0B49B' : '#E2603D')
            .enabled(!this.busy)
            .onClick(() => {
              this.send();
            })

          if (this.status.length > 0) {
            Text(this.status)
              .fontSize(14)
              .fontColor('#2E7D32')
              .width('100%')
          }

          Text('提交后立刻计入官网「反馈与问卷」页的公开统计；不收集身份信息，需要删除时发邮件到 2279125469@qq.com。')
            .fontSize(12)
            .lineHeight(18)
            .fontColor('#A1887F')
            .width('100%')
        }
        .width('100%')
        .padding({ bottom: 30 })
      }
      .layoutWeight(1)
      .scrollBar(BarState.Off)
      .width('100%')
    }
    .width('100%')
    .height('100%')
    .padding({ left: 20, right: 20, top: 8, bottom: 10 })
  }

  @Builder
  satisfactionPane() {
    Column({ space: 10 }) {
      Text('整体满意度（必填）').fontSize(15).fontWeight(FontWeight.Medium).fontColor('#4A3B32')
      Row({ space: 2 }) {
        ForEach(this.stars, (n: number) => {
          Text('★')
            .fontSize(34)
            .fontColor(this.rating >= n ? '#F2A03D' : '#E2D7C8')
            .onClick(() => {
              this.rating = n;
            })
        }, (n: number) => `star-${n}`)
      }
      .width('100%')

      Text('分项打分（可跳过）').fontSize(15).fontWeight(FontWeight.Medium).fontColor('#4A3B32')
        .margin({ top: 4 })

      ForEach(this.dimDefs, (d: DimDef, i: number) => {
        Column({ space: 4 }) {
          Text(d.label).fontSize(13).fontColor('#8D6E63')
          Row({ space: 6 }) {
            ForEach(this.stars, (n: number) => {
              ChipView({
                text: `${n} 分`,
                on: this.dimScores[i] === n,
                onTap: () => {
                  const copy: number[] = this.dimScores.slice();
                  copy[i] = n;
                  this.dimScores = copy;
                }
              })
            }, (n: number) => `dim-${i}-${n}`)
          }
          .width('100%')
        }
        .width('100%')
        .alignItems(HorizontalAlign.Start)
      }, (d: DimDef) => d.key)

      Text('愿意推荐给别的家长吗？（0 = 完全不会，10 = 一定推荐）')
        .fontSize(15).fontWeight(FontWeight.Medium).fontColor('#4A3B32').margin({ top: 4 })

      Flex({ wrap: FlexWrap.Wrap }) {
        ForEach(this.npsValues, (n: number) => {
          ChipView({
            text: `${n}`,
            on: this.nps === n,
            onTap: () => {
              this.nps = n;
            }
          })
        }, (n: number) => `nps-${n}`)
      }
      .width('100%')

      Text('最喜欢哪一块？（可多选）').fontSize(15).fontWeight(FontWeight.Medium).fontColor('#4A3B32')
        .margin({ top: 4 })

      Flex({ wrap: FlexWrap.Wrap }) {
        ForEach(this.likeTags, (t: string, i: number) => {
          ChipView({
            text: t,
            on: this.picked[i],
            onTap: () => {
              const copy: boolean[] = this.picked.slice();
              copy[i] = !copy[i];
              this.picked = copy;
            }
          })
        }, (t: string) => `tag-${t}`)
      }
      .width('100%')

      TextArea({ text: this.satText, placeholder: '还有什么想说的？（选填）' })
        .onChange((v: string) => {
          this.satText = v;
        })
        .height(100)
        .borderRadius(12)
        .backgroundColor('#FFFFFF')
    }
    .width('100%')
    .padding(16)
    .borderRadius(20)
    .backgroundColor('#FFFDF9')
    .alignItems(HorizontalAlign.Start)
  }

  @Builder
  surveyPane() {
    Column({ space: 12 }) {
      Text(`已答 ${this.answeredCount()} / ${this.questions.length} 题（至少 3 题）`)
        .fontSize(14).fontWeight(FontWeight.Medium).fontColor('#B4552F').width('100%')

      ForEach(this.questions, (q: QDef, qi: number) => {
        Column({ space: 6 }) {
          Text(q.title).fontSize(15).fontWeight(FontWeight.Medium).fontColor('#4A3B32')
          Flex({ wrap: FlexWrap.Wrap }) {
            ForEach(q.options, (opt: string) => {
              ChipView({
                text: opt,
                on: this.answers[qi] === opt,
                onTap: () => {
                  const copy: string[] = this.answers.slice();
                  copy[qi] = opt;
                  this.answers = copy;
                }
              })
            }, (opt: string) => `q-${qi}-${opt}`)
          }
          .width('100%')
        }
        .width('100%')
        .alignItems(HorizontalAlign.Start)
      }, (q: QDef) => q.id)

      TextArea({ text: this.surveyText, placeholder: '其他想补充的（选填）' })
        .onChange((v: string) => {
          this.surveyText = v;
        })
        .height(90)
        .borderRadius(12)
        .backgroundColor('#FFFFFF')
    }
    .width('100%')
    .padding(16)
    .borderRadius(20)
    .backgroundColor('#FFFDF9')
    .alignItems(HorizontalAlign.Start)
  }

  @Builder
  feedbackPane() {
    Column({ space: 8 }) {
      Text('这是什么类型？').fontSize(15).fontWeight(FontWeight.Medium).fontColor('#4A3B32')
      Flex({ wrap: FlexWrap.Wrap }) {
        ForEach(this.cats, (c: string) => {
          ChipView({
            text: c,
            on: this.fbCat === c,
            onTap: () => {
              this.fbCat = c;
            }
          })
        }, (c: string) => `cat-${c}`)
      }
      .width('100%')

      TextArea({
        text: this.fbText,
        placeholder: '例如：宝宝听播到一半切后台回来会从头开始（设备、版本一起写上更好查）'
      })
        .onChange((v: string) => {
          this.fbText = v;
        })
        .height(130)
        .borderRadius(12)
        .backgroundColor('#FFFFFF')
        .margin({ top: 4 })
    }
    .width('100%')
    .padding(16)
    .borderRadius(20)
    .backgroundColor('#FFFDF9')
    .alignItems(HorizontalAlign.Start)
  }
}
