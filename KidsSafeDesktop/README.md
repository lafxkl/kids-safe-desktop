# KidsSafeDesktop · 孩子端

孩子端的 **App 外壳**。界面与业务逻辑全部在 [`../KidsSafeShared`](../KidsSafeShared)，这里只放「每个 App 必须各自拥有」的东西：入口、平台实现、图标与资源、打包配置、发布脚本。

> 为什么页面不在这里：孩子端和家长端（`../KidsSafeParent`）共用同一套 87 个页面。拆成独立工程时把 `Pages / ViewModels / Services / Models / Helpers / Controls / Converters` 整体搬到了 `KidsSafeShared`，命名空间仍是 `KidsSafeDesktop.*`，所以这里的 `using` 看起来像在引用自己，其实是引用共享库。

> **文档入口**：所有文档已整理成 wiki，从 [`wiki/Home.md`](wiki/Home.md) 进。
> 想快速定位「哪个功能在哪、怎么打包、报错了怎么查」，看 wiki 比翻 `Docs/` 快。

## 目录

| 路径 | 作用 |
|---|---|
| `App.xaml(.cs)` | 入口。未同意隐私政策 → `PrivacyConsentPage`；否则 → `SplashPage`。窗口级效果（护眼遮罩）与前后台生命周期也在这里转发 |
| `MauiProgram.cs` | 第一件事就是挂运行日志（`AppLog.Install()`，发布版同样开启）；之后是 DI 注册：真实服务 + 平台服务（`Platforms.Android.*` / `Platforms.Windows.*`） |
| `Platforms/Android/` | `MainActivity`（儿童模式：锁任务、沉浸式、返回键拦截）、荣耀推送、`libs/*.aar`、`Assets/mcs-services.json` |
| `Platforms/Windows/` | WinUI 入口、应用目录枚举、原生音频/音效；启动时注册任务栏恢复兜底（Kiosk 本体在共享库的 `Platforms/Windows/WindowsKioskService.cs`） |
| `Resources/` | 图片、字体、`Raw/`（内置网页、音频、题库、游戏）、图标、启动图、样式 |
| `Installer/` | Inno Setup 脚本（`KidsSafeDesktop.iss`），装当前用户、不弹 UAC |
| `Website/` | 官网静态站（首页、下载、更新日志、隐私政策、来源页）与 `assets/` 截图 |
| `Linux/` | KDE Plasma 版前端（Qt 6 + Kirigami），独立实现，见其 README |
| `Scripts/` | 发布、安装包、部署、隐私政策导出、PDF 转页图等 PowerShell / Python 工具 |
| `Docs/` | 设计文档、技术文档、更新记录、账号体系、实时下发通道、上架材料、**开机自启与快捷键拦截**、**用户反馈与问卷** |

## 儿童模式怎么锁住的（三端对照）

| 手段 | Windows | Linux | Android |
|---|---|---|---|
| 开机自启 | HKCU `Run` → `--kiosk-guard` | XDG autostart / systemd `--user` | `BOOT_COMPLETED` + 前台服务 |
| 被杀后自愈 | 守护进程重启主程序（连续 5 次快挂才放弃） | systemd `Restart=always` | `START_STICKY` + 心跳闹钟 + 无障碍互拉 |
| 屏蔽系统快捷键 | 低级键盘钩子（Win/Alt+Tab/Alt+F4/Ctrl+Shift+Esc…） | X11 `XGrabKey`；Wayland 靠会话裁剪 | 无障碍拦截设置/商店/卸载界面 |
| 任务管理器 | 钩子 + `DisableTaskMgr` 策略（可逆备份）+ 盯进程 | 会话里没有任务管理器 | — |
| 兜底 | 安全桌面返回后抢回前台 | 专用会话（不加载桌面环境） | 默认桌面 + 自动回前台 + 设备管理员防卸载 |

`Ctrl+Alt+Del` 三端都拦不住（系统安全序列），这是设计上的保留出口。
完整方案、限制与验收步骤见 **`Docs/开机自启与快捷键拦截.md`**。

## 构建

```bash
# Windows
dotnet build KidsSafeDesktop.csproj -f net10.0-windows10.0.19041.0
dotnet run   --project KidsSafeDesktop.csproj -f net10.0-windows10.0.19041.0

# Android（需 Android SDK + maui-android workload）
dotnet build KidsSafeDesktop.csproj -f net10.0-android
```

必须带 `-f`。工程是多目标的，不指定会连 Android 一起还原。

启动链路：

```
PrivacyConsentPage（仅首次）
      ↓
  SplashPage（2.6s 动画，期间并行初始化：读设置 → 护眼 → 账号 → 时长守护 → 儿童模式）
      ↓
OnboardingPage（仅首次：家长 PIN / 主题）
      ↓
   HomePage（桌面）
```

## 需要自己准备的文件

这三个文件不在仓库里，缺了功能会降级，但 `dotnet build` 仍能通过：

```bash
cp Signing.props.sample Signing.props          # Android 打包必需，填 keystore 口令
cp Scripts/deploy.config.sample.json Scripts/deploy.config.json   # 仅部署官网时需要
# Keys/kidssafe.keystore                       # 自己的签名证书，见根 README
# Platforms/Android/Assets/mcs-services.json   # 荣耀推送配置；不用推送可删，同时删 csproj 里那行
```

## 修改版本号

版本号在两个地方，必须同步：

```xml
<ApplicationDisplayVersion>1.20</ApplicationDisplayVersion>  <!-- 对外显示 -->
<ApplicationVersion>21</ApplicationVersion>                  <!-- versionCode，安卓判断覆盖升级 -->
```

不要手改——用脚本改，否则很容易漏。

```bat
release.bat                  :: 用当前版本号重新出包
release.bat 1.21 22          :: 升到 v1.21 / versionCode 22 并出包
release.bat 1.21             :: 只给版本号，versionCode 自动 +1
release.bat -SkipBuild       :: 不重编，只把现有产物打包归位
build-installer.bat          :: 单独打 Windows 安装包（Inno Setup 6）
```

`release.bat` 会依次做：校验并写入版本号 → 构建 Android Release（签名 APK）与 Windows Release → 打包 `-apk / -win.zip` → 归档上一版到 `_release/`（已 gitignore）→ 复制到 `Website/downloads` 与 `KidsSafeServer/wwwroot/downloads` → 输出文件大小与 SHA-256 → 回填官网首页与 `sitemap.xml` 里的下载链接。

## 发布包瘦身

`KidsSafeDesktop.csproj` 里的 `TrimWindowsPublish` 目标在 `Release` + Windows 时按文件名精确剔除四类文件，把自包含发布从 **331MB / 698 文件**压到 **287MB / 442 文件**（约 −13%，图片从 103 张降到 58 张）：

| 类别 | 内容 | 为什么能删 |
|---|---|---|
| ① 调试诊断 | `createdump`、`mscordaccore*`、`mscordbi`、`DiaSymReader` | 只在崩溃后解析 dump 时加载，本项目不上报崩溃、不发 pdb |
| ② 未用框架程序集 | `Microsoft.VisualBasic*`、`Microsoft.CSharp`（无 `dynamic`）、`AsyncEnumerable`、`Dataflow`、`Tar`、`CodePages`、`AccessControl`、`ServiceModel.Web` | 全局无引用 |
| ③ WinUI `.mui` | 非中文/英文语言目录下的本地化资源 | `SatelliteResourceLanguages` 管不到 `.mui`，每份 44K，缺了自动回退英文 |
| ④ MSIX 图标 | `appiconSmallTile/MediumTile/LargeTile/WideTile/StoreLogo/Logo` | 只有 `Package.appxmanifest` 会引用；本项目 `WindowsPackageType=None`，走 Inno Setup 安装 |

注意 ④ 的匹配必须用 `StartsWith`：文件名形如 `appiconLargeTile.scale-400.png`，`%(Filename)` 是 `appiconLargeTile.scale-400`，用 `==` 永远匹配不到。

这不是裁剪器（没用 `PublishTrimmed`），不涉及反射/序列化风险；加新条目请先确认全项目无引用。

## 平台差异备忘

- **Android 儿童模式**：`LOCK_TASK` 固定任务 + 沉浸式 + 返回键拦截；退出走家长 PIN。`MainActivity` 通过 `AndroidHooks` 静态委托向共享库暴露能力（因为共享库不能直接引用 App 工程的类型）。家长端注册的是空实现。
- **Windows 任务栏**：进入儿童模式时隐藏任务栏，恢复有**四层兜底**——窗口失活即恢复、`ProcessExit` 钩子、未处理异常钩子、启动自检标记文件。帮助页还有「🖥️ 恢复任务栏」一键自救。改这块务必保留全部四层，否则异常退出会让用户桌面失去任务栏。
- **签名一致性**：Debug 与 Release 必须用同一张证书。荣耀推送校验安装包签名指纹，混用会出现「发布包能推、调试包 10202」。

## 运行日志（诊断用）

**发布版也照常记录**，不加 `#if DEBUG`。孩子设备上「打不开」「突然退出」「设了没用」这类反馈，
没有现场日志就只能靠猜，所以这一块是和功能同等重要的基础设施。

| 项 | 说明 |
|---|---|
| 实现 | `../KidsSafeShared/Helpers/AppLog.cs`（安卓与 PC 共用同一份） |
| 文件 | `runtime.log`，超过 512KB 滚成 `runtime.1.log`（只留一份旧的） |
| 位置 | Windows：`%LOCALAPPDATA%\<包名>\runtime.log`；安卓：`Android/data/<包名>/files/logs/`（外部专属目录，卸载自动清除，不需要存储权限） |
| 格式 | `MM-dd HH:mm:ss.fff 级别[分类] 内容`，级别为 `I/W/E/T` |
| 会记什么 | 会话横幅（版本 / 系统 / 机型 / .NET）、启动 6 步各自的成败与耗时、进入根页面耗时、Kiosk 是否真的锁住、前后台切换、页面导航、设置读写降级、长轮询重连、以及所有未捕获异常 |
| 在哪看 | 家长中心 → 🩺 诊断日志。页内可切「运行日志 / 详细(nav.log+crash.log)」、刷新、复制、导出、清空 |

未捕获异常挂了**四个**入口，缺一个都会漏：

- `AppDomain.CurrentDomain.UnhandledException` —— 主线程/线程池逃出来的异常；
- `TaskScheduler.UnobservedTaskException` —— 没人 `await` 也没人观察的 Task（以往完全静默，记完仍然 `SetObserved()`，不改变原行为）；
- 安卓 `AndroidEnvironment.UnhandledExceptionRaiser` —— 抛到 Java 层的异常，`AppDomain` 那一路有时收不到；
- WinUI `Application.UnhandledException` —— 界面线程异常，同样不经过 `AppDomain`。

设计上的几条硬约束：**绝不抛异常**（所有入口包 `try`）、**线程安全**（一把锁串写入）、
**不长期持有文件句柄**（每行开关一次文件，家长端才能在应用运行时随时读、随时删）。
`NavFx.Log` 现在同时写运行日志和原来的 `nav.log`，不破坏既有习惯。

> 安卓的诊断数据都在 `Android/data/<包名>/files/logs`。注意 **Android 11+ 用 USB/MTP 浏览该目录会被系统挡住**，
> 手机自带文件管理器多数可见；最省事的路子仍是应用内「诊断日志」页直接「导出」——
> 安卓走系统分享（微信/邮箱都能发），Windows 直接在资源管理器里定位到文件。

## 官网

`Website/` 是纯静态站，可直接丢到任意静态托管（Nginx / GitHub Pages / OSS）。`Scripts/deploy.ps1` 用 `deploy.config.json` 里的 SSH 目标上传，属于作者自己的部署方式，不是必需步骤。

官网里有三个与反馈相关的页面，接口走 `KidsSafeServer` 的 `/api/feedback/*`：

- `feedback.html`：公开的「意见反馈 / 满意度调查 / 使用问卷」三位一体提交页，右侧是实时公开统计（份数、平均分、NPS、分项、精选留言）；
- `comments.html`：公开评论区，可在页内直接发表、按平台与星级筛选、点赞。数据来自 `Website/data/comments.json`（静态精选，可直接编辑）+ 服务端实时公开留言；
- `feedback-admin.html`：明细与 CSV 导出，需要管理密钥（服务端 `Feedback:AdminKey`）。

首页 `index.html` 的「家长怎么说」读的就是 `data/comments.json` 里 `featured: true` 的那几条，改文件即改首页，不用动 HTML。

四端的入口、接口契约、离线队列、隐私边界与验收步骤见 **`Docs/用户反馈与问卷.md`**。
