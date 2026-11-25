import { Backdrop } from '../components/Backdrop';
import { LargeButton } from '../components/LargeButton';
import { PinPad } from '../components/PinPad';
import { AppPreset, appCatalog } from '../core/AppCatalog';
import { kiosk } from '../core/Kiosk';
import { PageId, router } from '../core/Router';
import { screenTime } from '../core/ScreenTime';
import { settings } from '../core/Settings';
import { makeApp } from '../model/Models';

/**
 * 首次引导三步：起名字 → 设家长密码 → 挑应用。
 * 对应 Linux 端 OnboardingPage.qml。
 */
@ComponentV2
export struct OnboardingPage {
  @Local step: number = 0;
  @Local nickDraft: string = '小朋友';
  @Local avatarDraft: string = '🐯';
  @Local pinStage: number = 0;
  @Local firstPin: string = '';
  @Local pinError: string = '';
  @Local pinDone: boolean = false;
  @Local picked: string[] = [];
  @Local manualBundle: string = '';
  @Local manualName: string = '';
  @Local hint: string = '';

  private avatars: string[] = ['🐯', '🐰', '🐼', '🦊', '🐳', '🦄', '🐨', '🐣'];

  build() {
    Stack() {
      Backdrop()

      Column() {
        Row({ space: 8 }) {
          ForEach([0, 1, 2], (item: number) => {
            Row()
              .width(this.step === item ? 30 : 10)
              .height(10)
              .borderRadius(5)
              .backgroundColor(this.step === item ? '#FF8A5C' : '#E7D3BF')
          }, (item: number) => `step-${item}`)
        }
        .margin({ bottom: 24 })

        if (this.step === 0) {
          this.nameStep()
        } else if (this.step === 1) {
          this.pinStep()
        } else {
          this.appStep()
        }
      }
      .width('100%')
      .height('100%')
      .constraintSize({ maxWidth: 760 })
      .padding({ left: 32, right: 32, top: 40 + kiosk.safeTopVp, bottom: 32 })
      .justifyContent(FlexAlign.Center)
    }
    .width('100%')
    .height('100%')
  }

  @Builder
  nameStep() {
    Column({ space: 22 }) {
      Text('小朋友叫什么名字？')
        .fontSize(28)
        .fontWeight(FontWeight.Bold)
        .fontColor('#4A3B32')

      TextInput({ text: this.nickDraft, placeholder: '输入昵称' })
        .height(58)
        .fontSize(20)
        .borderRadius(18)
        .backgroundColor('#FFFFFF')
        .onChange((value: string) => {
          this.nickDraft = value;
        })

      Text('挑一个头像')
        .fontSize(16)
        .fontColor('#9B8574')

      Flex({ wrap: FlexWrap.Wrap }) {
        ForEach(this.avatars, (emoji: string) => {
          Text(emoji)
            .fontSize(30)
            .width(56)
            .height(56)
            .textAlign(TextAlign.Center)
            .borderRadius(18)
            .backgroundColor(this.avatarDraft === emoji ? '#FFE0C2' : '#FFFFFF')
            .margin(5)
            .onClick(() => {
              this.avatarDraft = emoji;
            })
        }, (emoji: string) => `avatar-${emoji}`)
      }
      .width('100%')

      LargeButton({ text: '下一步', isEnabled: this.nickDraft.trim().length > 0, onTap: () => {
        settings.nickname = this.nickDraft.trim();
        settings.avatarEmoji = this.avatarDraft;
        settings.save();
        this.step = 1;
      } })
    }
    .width('100%')
  }

  @Builder
  pinStep() {
    Column({ space: 18 }) {
      Text('设一个只有家长知道的密码')
        .fontSize(26)
        .fontWeight(FontWeight.Bold)
        .fontColor('#4A3B32')

      Text(this.pinStage === 0 ? '4 位数字，之后进家长后台要用' : '再输一遍确认')
        .fontSize(16)
        .fontColor('#9B8574')

      PinPad({
        title: this.pinStage === 0 ? '输入密码' : '确认密码',
        errorText: this.pinError,
        onComplete: (pin: string) => {
          this.handlePin(pin);
        }
      })

      if (this.pinDone) {
        Text('密码已设好 ✓')
          .fontSize(16)
          .fontColor('#2AA7A0')
      }

      LargeButton({
        text: '下一步', isEnabled: this.pinDone, onTap: () => {
          this.step = 2;
        }
      })
    }
    .width('100%')
  }

  @Builder
  appStep() {
    Column() {
      Scroll() {
        Column({ space: 16 }) {
          Text('给小朋友开放哪些应用？')
            .fontSize(26)
            .fontWeight(FontWeight.Bold)
            .fontColor('#4A3B32')

          Text('鸿蒙不允许应用去扫描设备上装了什么，所以这里只能从预设里挑，或者自己填包名。之后随时可以在家长后台改。')
            .fontSize(14)
            .fontColor('#9B8574')

          ForEach(appCatalog.presets(), (preset: AppPreset) => {
            Row({ space: 12 }) {
              Text(preset.emoji).fontSize(26)
              Column({ space: 2 }) {
                Text(preset.label).fontSize(17).fontColor('#4A3B32')
                Text(preset.bundleName).fontSize(12).fontColor('#B8A492')
              }
              .alignItems(HorizontalAlign.Start)
              .layoutWeight(1)

              Toggle({ type: ToggleType.Switch, isOn: this.picked.indexOf(preset.bundleName) >= 0 })
                .onChange((on: boolean) => {
                  this.togglePreset(preset.bundleName, on);
                })
            }
            .width('100%')
            .padding(14)
            .borderRadius(18)
            .backgroundColor('#FFFFFF')
          }, (preset: AppPreset) => `preset-${preset.bundleName}`)

          Divider().color('#EADFD2').margin({ top: 8, bottom: 8 })

          Text('手动添加（包名在「设置 → 应用和元服务」里能看到）')
            .fontSize(14)
            .fontColor('#9B8574')

          Row({ space: 10 }) {
            TextInput({ text: this.manualBundle, placeholder: 'com.example.app' })
              .layoutWeight(3)
              .height(52)
              .fontSize(15)
              .borderRadius(14)
              .backgroundColor('#FFFFFF')
              .onChange((value: string) => {
                this.manualBundle = value;
              })
            TextInput({ text: this.manualName, placeholder: '显示名' })
              .layoutWeight(2)
              .height(52)
              .fontSize(15)
              .borderRadius(14)
              .backgroundColor('#FFFFFF')
              .onChange((value: string) => {
                this.manualName = value;
              })
            Button('添加')
              .height(52)
              .borderRadius(14)
              .backgroundColor('#7C5CE0')
              .onClick(() => {
                this.addManual();
              })
          }
          .width('100%')

          if (this.hint.length > 0) {
            Text(this.hint).fontSize(13).fontColor('#2AA7A0')
          }
        }
        .width('100%')
        .padding({ bottom: 16 })
      }
      .scrollBar(BarState.Off)
      .width('100%')
      .layoutWeight(1)

      // 按钮固定在底部，不随列表滚走。
      // 两个按钮各套一层 layoutWeight(1) 平分宽度：LargeButton 根节点是 width('100%')，
      // 直接并排放在 Row 里时第二个会被挤出屏幕右侧（表现为「没有下一步按钮」）。
      Row({ space: 12 }) {
        Column() {
          LargeButton({
            text: '上一步', outlined: true, onTap: () => {
              this.step = 1;
            }
          })
        }
        .layoutWeight(1)

        Column() {
          LargeButton({
            text: '完成，进入桌面', onTap: () => {
              this.finish();
            }
          })
        }
        .layoutWeight(1)
      }
      .width('100%')
      .padding({ top: 12 })
    }
    .width('100%')
    .layoutWeight(1)
  }

  private handlePin(pin: string): void {
    if (this.pinStage === 0) {
      this.firstPin = pin;
      this.pinStage = 1;
      this.pinError = '';
      return;
    }
    if (pin !== this.firstPin) {
      this.pinError = '两次输入不一致，再来一次';
      this.pinStage = 0;
      this.firstPin = '';
      return;
    }
    this.pinError = '';
    settings.setPin(pin);
    this.pinDone = true;
  }

  private togglePreset(bundleName: string, on: boolean): void {
    const next: string[] = [];
    for (let i = 0; i < this.picked.length; i++) {
      if (this.picked[i] !== bundleName) {
        next.push(this.picked[i]);
      }
    }
    if (on) {
      next.push(bundleName);
    }
    this.picked = next;

    if (on) {
      const preset = this.findPreset(bundleName);
      if (preset !== null) {
        settings.addApp(appCatalog.toApp(preset));
      }
    } else {
      settings.removeApp(bundleName);
    }
  }

  private findPreset(bundleName: string): AppPreset | null {
    const list = appCatalog.presets();
    for (let i = 0; i < list.length; i++) {
      if (list[i].bundleName === bundleName) {
        return list[i];
      }
    }
    return null;
  }

  private addManual(): void {
    const bundle = this.manualBundle.trim();
    if (bundle.length === 0) {
      this.hint = '先填包名';
      return;
    }
    const name = this.manualName.trim().length > 0 ? this.manualName.trim() : bundle;
    settings.addApp(makeApp(name, bundle, '📦', '#8E9AAF'));
    this.hint = `已添加「${name}」`;
    this.manualBundle = '';
    this.manualName = '';
  }

  private finish(): void {
    settings.onboardingDone = true;
    settings.save();
    screenTime.reevaluate();
    screenTime.start();
    router.reset(PageId.Home);
  }
}
