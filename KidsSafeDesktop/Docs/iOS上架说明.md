# iOS 端说明：形态、构建与上架

> 适用版本：客户端 v1.20 起（`net10.0-ios`，最低 iOS 15.0）
> 目标：在保留「孩子能玩、家长能管、数据不出本机」的前提下，把内容侧完整体搬到 iPhone / iPad。

---

## 一、iOS 版是「内容版」，不是「受控桌面」

Android 版的核心卖点之一是**替换桌面 + 应用白名单 + 使用时长统计 + 防退出**。这四件事在 iOS 上
全部依赖系统不开放的权限，属于**平台限制**，不是工作量问题：

| 能力 | Android | iOS | 说明 |
|---|---|---|---|
| 替换桌面 / 开机自启 | ✅ | ❌ | iOS 不允许第三方 Launcher，也不允许开机自启 |
| 应用白名单、启动其他 App | ✅ | ❌ | 只能通过 URL Scheme 打开，且无法枚举已安装应用 |
| 读取其他 App 使用时长 | ✅ | ⚠️ | 需 FamilyControls（屏幕使用时间）单独审批，且只能管自己的 App 组 |
| 防退出 / 锁机（Kiosk） | ✅ | ⚠️ | 只有「引导式访问」由家长手动开启，App 自身无法强制 |
| 绘本 / 儿歌 / 小游戏 / 口算 / 关爱中心 / 家长 PIN | ✅ | ✅ | 全部保留，离线可用 |
| 增量更新（下发 dll） | ✅ | ❌ | App Store 审核指南 2.5.2 禁止，iOS 只走商店更新 |

因此 iOS 版落地为**内容版**：首页 19 个内置入口、30 多款小游戏、听一听、阅读、学习天地、
关爱中心、家长中心全部保留；依赖系统权限的模块注册为桩实现（`StubServices.cs`），
界面按「没有可管理的外部应用」自动呈现空状态，不会崩、不会卡。

---

## 二、工程结构

新增/改动的文件都在 `KidsSafeDesktop/` 下：

```
Platforms/iOS/
  AppDelegate.cs                     启动入口（日志 + 音频会话类别）
  Program.cs                         UIApplication.Main
  Info.plist                         显示名、方向、后台音频、出口合规
  Entitlements.plist                 权限模板（当前未启用，留档）
  Resources/PrivacyInfo.xcprivacy     隐私清单（强制项 + 数据类型申报）

KidsSafeShared/Platforms/iOS/
  IosAudioPlayerService.cs           听一听：AVPlayer（本地 + http 流）
  IosSoundService.cs                 交互音效：ToneWav 合成 → AVAudioPlayer
  NativeEyeCare.cs                   护眼遮罩：key window 上叠不拦截触摸的暖色层

KidsSafeShared/Services/NoopUpdateService.cs   iOS 的「永不更新」实现

Scripts/build-ios.sh                 macOS 构建脚本（sim / device / ipa）
Signing.iOS.props.sample             签名配置模板（复制为 Signing.iOS.props）
```

两个工程的目标框架都加了条件：

```xml
<TargetFrameworks Condition="$([MSBuild]::IsOSPlatform('osx')) or '$(EnableIosTarget)' == 'true'">
  $(TargetFrameworks);net10.0-ios
</TargetFrameworks>
```

含义：**macOS 上默认参与构建；Windows 上默认不参与**，现有的 Android / Windows 打包完全不受影响。
Windows 只做编译检查时显式加 `-p:EnableIosTarget=true`。

---

## 三、构建

### 1. 只在 Windows 上做编译检查（不需要 Mac）

```powershell
dotnet build KidsSafeDesktop\KidsSafeDesktop.csproj -f net10.0-ios -p:EnableIosTarget=true -c Debug
```

能编译到 `bin\Debug\net10.0-ios\iossimulator-x64\KidsSafeDesktop.dll`，
**但产不出可运行的 .app**（原生链接、资源编译需要 Xcode）。

### 2. macOS 上跑模拟器

```bash
bash Scripts/build-ios.sh sim
xcrun simctl install booted KidsSafeDesktop/bin/Debug/net10.0-ios/iossimulator-arm64/KidsSafeDesktop.app
xcrun simctl launch booted cc.siguangblog.kidsafe
```

### 3. 真机 / TestFlight

```bash
cp Signing.iOS.props.sample Signing.iOS.props   # 填证书名与描述文件名
bash Scripts/build-ios.sh device                # 真机 Debug
bash Scripts/build-ios.sh ipa                   # Release + ipa，产物复制到 dist/
```

前置条件：Xcode 16+、`dotnet workload install ios maui`、Apple 开发者账号（个人 99 美元/年）。

---

## 四、Bundle ID 与签名

| 项 | 值 |
|---|---|
| Bundle ID | `cc.siguangblog.kidsafe`（全渠道统一，与海外版 Android 包名一致） |
| 显示名 | 儿童安全桌面 |
| 最低系统 | iOS 15.0 |
| 设备 | iPhone + iPad（`UIDeviceFamily = 1,2`） |
| 签名 | `Signing.iOS.props` 里的 `IosCodesignKey` / `IosCodesignProvision` |

> Bundle ID 上架后不可更改。国内版与海外版在 iOS 上**共用一个包**：
> 国内区上架需要 ICP 备案号（见下），海外区不需要；功能完全一致，没有再拆包的必要。
> Android 那边坚持拆两个包，是因为国内包里不能带 Singular 归因 SDK —— iOS 版目前不带任何归因 SDK，
> 所以不存在这个分叉。

---

## 五、App Store 提交要点

1. **隐私清单**：`PrivacyInfo.xcprivacy` 已申报「反馈内容 + 可选邮箱」两项收集，声明不做追踪。
   如果以后接入 Singular 或其他归因 SDK，必须同时改三处：隐私清单、`Info.plist`（ATT 文案）、
   官网 `privacy.html`，否则会出现「清单与政策不一致」的拒审。
2. **儿童类要求**（审核指南 1.3 / 5.1.4）：
   - 不得含第三方广告与分析 SDK（当前一个都没有，保持住）；
   - 外链、购买、反馈提交必须在**家长门**之后（现有家长 PIN 已满足）；
   - 隐私政策必须能从 App 内两步内到达（`设置 → 关于 → 隐私政策`）。
3. **账号**：App 不要求注册，本地自动建账号，不触发「必须提供删除账号入口」的 5.1.1(v)。
4. **后台音频**：`UIBackgroundModes=audio` 用于锁屏哄睡。审核可能问用途，备注写清
   「儿童睡前故事与白噪音，属核心功能，非后台保活」。
5. **中国区上架**：需要工信部**移动互联网应用程序备案**（ICP 备案的 App 形态），
   以及《网络文化经营许可证》视内容而定；仅做工具/内容分发一般只需备案号。
6. **审核备注模板**：

   > 本应用为儿童内容应用，无广告、无内购、无第三方统计 SDK。
   > 家长中心入口需输入 4 位 PIN（默认在首次启动引导中设置）。
   > 所有学习记录仅保存在本机，不上传服务器；意见反馈为用户主动提交。

---

## 六、与 Android 的行为差异（家长需要知道）

- 首页的「外部应用」区域为空（iOS 无法枚举其他 App），内置入口不受影响；
- 「家长中心 → 设备守护」里的桌面替换、悬浮窗、电池白名单按钮在 iOS 上会提示「仅安卓支持」；
- 「检查更新」永远是「已是最新」（更新走 App Store）；
- 听一听支持锁屏播放；护眼遮罩为暖色叠加层（不是系统级滤镜，截图不会被染色）。

---

## 七、后续可做

| 项 | 工作量 | 说明 |
|---|---|---|
| 家长端 iOS | 中 | `KidsSafeParent` 加 `net10.0-ios` 即可复用共享库；需要单独 Bundle ID |
| Singular 归因（海外） | 大 | 需要 Singular iOS SDK 的 xcframework + 绑定工程（`ApiDefinition.cs`），并按 ATT 弹窗 |
| 屏幕使用时间 | 大 | FamilyControls + DeviceActivity，需向 Apple 申请 entitlement，审核不确定 |
| 语音输入 | 中 | `SFSpeechRecognizer` + 麦克风/语音识别两条权限文案，需家长门后再申请 |
| iPad 分栏布局 | 小 | 现有 `LauncherMetrics` 已按宽度自适应，主要是首页时钟与天气卡片 |

---

## 八、自检清单（每次发版）

- [ ] `dotnet build -f net10.0-ios -p:EnableIosTarget=true` 0 错误
- [ ] 模拟器启动 → 隐私同意 → 启动页 → 首页 19 个入口可点
- [ ] 听一听能出声，锁屏后继续播，锁屏控制中心能暂停
- [ ] 护眼遮罩开启后整屏变暖，且所有按钮仍可点
- [ ] 家长 PIN 能进出家长中心；「设备守护」在 iOS 上不崩
- [ ] 反馈提交成功（断网时进队列，联网补传）
- [ ] 隐私清单与实际行为一致（改了 SDK 就要回来改这一条）
