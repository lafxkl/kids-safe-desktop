import { common, Want } from '@kit.AbilityKit';
import { Backdrop } from '../components/Backdrop';
import { PageHeader } from '../components/PageHeader';
import { router } from '../core/Router';
import {
  BookPick,
  READING_PICKS,
  READING_RULES,
  READING_SOURCES,
  READING_UPDATED_AT,
  ReadingSource
} from '../data/ReadingData';

/**
 * 关爱中心 →「绘本怎么找」。对应 C# 的 ReadingPage。
 *
 * 只做一件事：告诉家长哪几家能合法免费看、每家是什么形式、经典绘本去哪借。
 * 不托管、不转存、不给任何 PDF 直链 —— 原因写在页尾那张提示卡里。
 */
@ComponentV2
export struct ReadingPage {

  /**
   * 用系统浏览器打开。用 startAbility 而不是 Web 组件：
   * 这些站点是给家长在浏览器里慢慢看的，塞进应用内 WebView 既不好用，也容易被误认为「应用内置了这些书」。
   */
  private openUrl(url: string): void {
    try {
      const ctx = getContext(this) as common.UIAbilityContext;
      const want: Want = {
        action: 'ohos.want.action.viewData',
        uri: url
      };
      ctx.startAbility(want).catch(() => {
      });
    } catch (err) {
      // 没有默认浏览器时静默失败：域名已经印在卡片底部，手动输入也能进
    }
  }

  @Builder
  sectionTitle(text: string) {
    Text(text)
      .fontSize(15.5)
      .fontWeight(FontWeight.Bold)
      .fontColor('#3A2B22')
      .width('100%')
      .margin({ top: 8 })
  }

  @Builder
  tipCard() {
    Column({ space: 6 }) {
      Text('📖 只收「能合法免费看」的地方')
        .fontSize(14)
        .fontWeight(FontWeight.Bold)
        .fontColor('#8A6D00')

      Text('下面三张卡都是公益图书馆、基金会项目或开放目录，点进去就能看。但它们的形式不一样：' +
        '有的是扫描版电子书，有的是演员朗读视频，有的是线上借阅。都没有「下载 PDF」这一步，' +
        '因为这些书本来就不允许整本下载。')
        .fontSize(12.5)
        .lineHeight(21)
        .fontColor('#6B5A2A')
    }
    .width('100%')
    .padding(15)
    .borderRadius(20)
    .backgroundColor('#FFF8E1')
    .border({ width: 1, color: '#FFE082' })
    .alignItems(HorizontalAlign.Start)
  }

  @Builder
  sourceCard(s: ReadingSource) {
    Column({ space: 8 }) {
      Row({ space: 10 }) {
        Text(s.emoji).fontSize(26)

        Column({ space: 2 }) {
          Text(s.name)
            .fontSize(16)
            .fontWeight(FontWeight.Bold)
            .fontColor('#3A2B22')

          Text(`${s.lang}　·　${s.ages}`)
            .fontSize(11.5)
            .fontColor('#A1887F')
        }
        .alignItems(HorizontalAlign.Start)
        .layoutWeight(1)
      }
      .width('100%')
      .alignItems(VerticalAlign.Center)

      Text(s.what)
        .fontSize(13)
        .lineHeight(21)
        .fontColor('#3A2B22')
        .width('100%')

      Text('怎么用')
        .fontSize(11.5)
        .fontWeight(FontWeight.Bold)
        .fontColor('#E2603D')
        .width('100%')

      Text(s.how)
        .fontSize(12.5)
        .lineHeight(20)
        .fontColor('#7C6353')
        .width('100%')

      Text(`注意：${s.note}`)
        .fontSize(11.5)
        .lineHeight(19)
        .fontColor('#A1887F')
        .width('100%')

      Text('打开网站')
        .fontSize(13.5)
        .fontWeight(FontWeight.Medium)
        .fontColor('#FFFFFF')
        .padding({ left: 20, right: 20, top: 9, bottom: 9 })
        .borderRadius(19)
        .backgroundColor('#E2603D')
        .onClick(() => {
          this.openUrl(s.url);
        })

      Text(s.url.replace('https://', '').replace('/', ''))
        .fontSize(10.5)
        .fontColor('#B5A092')
        .width('100%')
    }
    .width('100%')
    .padding(15)
    .borderRadius(20)
    .backgroundColor('#FFFFFF')
    .border({ width: 1, color: '#F4E5D3' })
    .alignItems(HorizontalAlign.Start)
  }

  @Builder
  bookCard(b: BookPick) {
    Column({ space: 5 }) {
      Text(b.title)
        .fontSize(15)
        .fontWeight(FontWeight.Bold)
        .fontColor('#3A2B22')
        .width('100%')

      Text(`${b.original}　·　${b.author}`)
        .fontSize(11.5)
        .fontColor('#A1887F')
        .width('100%')

      Text(b.why)
        .fontSize(13)
        .lineHeight(21)
        .fontColor('#7C6353')
        .width('100%')

      Text('查电子版 / 馆藏')
        .fontSize(13)
        .fontWeight(FontWeight.Medium)
        .fontColor('#E2603D')
        .padding({ left: 18, right: 18, top: 8, bottom: 8 })
        .borderRadius(18)
        .backgroundColor('#FFFFFF')
        .border({ width: 1, color: '#F3D2B8' })
        .margin({ top: 4 })
        .onClick(() => {
          this.openUrl(b.searchUrl);
        })
    }
    .width('100%')
    .padding(15)
    .borderRadius(18)
    .backgroundColor('#FFFFFF')
    .border({ width: 1, color: '#F4E5D3' })
    .alignItems(HorizontalAlign.Start)
  }

  @Builder
  rulesCard() {
    Column({ space: 7 }) {
      Text('⚠️ 关于网盘里的「绘本合集」')
        .fontSize(13.5)
        .fontWeight(FontWeight.Bold)
        .fontColor('#B23A48')
        .width('100%')

      ForEach(READING_RULES, (rule: string) => {
        Row({ space: 7 }) {
          Text('•')
            .fontSize(13)
            .fontColor('#B23A48')
            .alignSelf(ItemAlign.Start)

          Text(rule)
            .fontSize(12)
            .lineHeight(20)
            .fontColor('#8A6B70')
            .layoutWeight(1)
        }
        .width('100%')
        .alignItems(VerticalAlign.Top)
      }, (rule: string) => rule)
    }
    .width('100%')
    .padding(15)
    .borderRadius(20)
    .backgroundColor('#FFF1F0')
    .border({ width: 1, color: '#FFCDD2' })
    .alignItems(HorizontalAlign.Start)
  }

  build() {
    Stack() {
      Backdrop()

      Column() {
        PageHeader({
          title: '绘本怎么找',
          subtitle: '只收能合法免费看的',
          onBack: () => {
            router.back();
          }
        })
          .padding({ left: 16, right: 16, top: 10, bottom: 6 })

        Scroll() {
          Column({ space: 12 }) {
            this.tipCard()

            this.sectionTitle('三个可以免费看的来源')

            ForEach(READING_SOURCES, (s: ReadingSource) => {
              this.sourceCard(s);
            }, (s: ReadingSource) => s.id)

            this.sectionTitle('常被问到的几本，去哪儿找')

            Text('这几本都还在版权保护期内。本页不提供 PDF，也不建议去网盘找 —— ' +
              '可以先去 Open Library 查有没有电子版或馆藏，没有的话去少儿图书馆借，或者买一本平装版。')
              .fontSize(12.5)
              .lineHeight(21)
              .fontColor('#7C6353')
              .width('100%')

            ForEach(READING_PICKS, (b: BookPick) => {
              this.bookCard(b);
            }, (b: BookPick) => b.title)

            this.rulesCard()

            Text(`清单更新于 ${READING_UPDATED_AT}　·　出处与授权说明见官网「关爱中心 · 内容来源」`)
              .fontSize(11)
              .fontColor('#B5A092')
              .textAlign(TextAlign.Center)
              .width('100%')
              .margin({ top: 6 })
          }
          .width('100%')
          .padding({ left: 16, right: 16, top: 4, bottom: 28 })
          .alignItems(HorizontalAlign.Start)
        }
        .layoutWeight(1)
        .scrollBar(BarState.Off)
        .align(Alignment.Top)
      }
      .width('100%')
      .height('100%')
    }
  }
}
