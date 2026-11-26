# 儿童安全桌面 · KidsSafeDesktop

<p align="center">
  <img src="https://kids.siguangblog.cc/assets/header-readme.png" alt="儿童安全桌面">
</p>

<p align="center">
  <img alt=".NET 10" src="https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet">
  <img alt=".NET MAUI" src="https://img.shields.io/badge/MAUI-Android%20%7C%20Windows%20%7C%20iOS-512BD4">
  <img alt="平台" src="https://img.shields.io/badge/platform-Android%205.0%2B%20%7C%20Windows%2010%2B%20%7C%20iOS%2015%2B-informational">
  <img alt="Linux 与鸿蒙" src="https://img.shields.io/badge/platform-Linux%20%7C%20HarmonyOS-informational">
  <img alt="License" src="https://img.shields.io/badge/license-MIT-green">
</p>

<p align="center">
  简体中文 · <a href="README.en.md">English</a>
</p>

一个给 3–12 岁孩子用的 **受控桌面环境**：孩子打开平板或电脑，看到的是一个只有动画、儿歌、绘本、小游戏和学习内容的童趣桌面，没有浏览器、没有短视频、没有应用商店；家长用另一台设备（网页或家长端 App）决定「能看什么、能玩多久」。

原生客户端，**.NET 10 + .NET MAUI 单代码库**，一套代码出 Android、Windows 与 iOS（iPhone / iPad，内容版）；另有两个不复用 XAML 层的独立前端——**Linux**（Qt 6 + Kirigami，KDE Plasma 全屏会话）与**鸿蒙**（ArkTS + ArkUI）。全部界面离线可用，不依赖任何云服务；需要远程管理时再自己起一个几十 MB 的 ASP.NET Core 服务端。

> iOS 版是**内容版**：绘本、儿歌、小游戏、口算、关爱中心、家长 PIN 全保留；替换桌面、应用白名单、使用时长统计这些依赖系统级权限的能力 iOS 不开放，由桩实现静默降级。差异见 **[Docs/iOS上架说明.md](KidsSafeDesktop/Docs/iOS上架说明.md)**。

> 当前版本：客户端 **v1.20**（versionCode 21），服务端 **1.4.1**。变更见 [更新记录](KidsSafeDesktop/Docs/更新记录.md)。

---

## 目录

- [它解决什么问题](#它解决什么问题)
- [界面截图](#界面截图)
- [功能一览](#功能一览)
- [官网与反馈](#官网与反馈)
- [架构](#架构)
- [快速开始](#快速开始)
- [配置与密钥](#配置与密钥)
- [仓库结构](#仓库结构)
- [文档](#文档)
- [已知限制](#已知限制)
- [路线图](#路线图)
- [参与贡献](#参与贡献)
- [许可证与免责声明](#许可证与免责声明)

---

## 它解决什么问题

把一台设备直接交给孩子，问题不在于「有没有儿童模式」，而在于儿童模式太容易被绕过、或者太贫瘠以至于孩子不想用。这个项目在两者之间找平衡：

- **孩子愿意用** —— 有 30 多款小游戏、15 款内置网页游戏、离线音频中心、绘本、电子课本、口算练习、涂鸦、星星奖励和排行榜，不是一张空白白名单。
- **家长管得住** —— 应用白名单、每日时长、游戏开关、护眼滤镜、在线内容总开关；退出儿童模式要家长 PIN（Windows 侧同时接管任务栏与快捷键）。
- **数据不出本机** —— 成绩、日记、心情记录、使用时长全部存在设备本地（`SecureStorage` / SQLite / JSON）。唯一的网络请求是天气（可关）和可选的家长服务器同步；AI 故事功能默认关闭，需要家长显式打开并自备密钥。
- **不绑账号** —— 首次启动自动建本地账号，离线直接用。想跨设备管，才需要自建服务端。

## 界面截图

五张均为 Android 真机截图（v1.19 / v1.20 界面），没有用示意图代替。手机上可直接点开看原图：

<p align="center">
  <img src="https://kids.siguangblog.cc/assets/screenshot_home.png" alt="桌面式首页">
  <img src="https://kids.siguangblog.cc/assets/screenshot_games.png" alt="游戏乐园">
  <img src="https://kids.siguangblog.cc/assets/screenshot_learn.png" alt="学习天地">
</p>

<p align="center"><strong>桌面式首页 · 游戏乐园 · 学习天地</strong></p>

<p align="center">
  <img src="https://kids.siguangblog.cc/assets/screenshot_player.png" alt="听一听 · 媒体中心">
  <img src="https://kids.siguangblog.cc/assets/screenshot_care.png" alt="关爱中心">
</p>

<p align="center"><strong>听一听 · 媒体中心 · 关爱中心</strong>（更多界面见<a href="https://kids.siguangblog.cc/">官网</a>）</p>

> 图片直接引用官网域名（`kids.siguangblog.cc`）上的文件，不走仓库相对路径——Gitee 手机端对大图的相对路径支持不稳，容易显示成裂图。源文件仍在仓库的 `KidsSafeDesktop/` → `Website/assets/`，换图时先改仓库、再上传到服务器同名文件即可。顶部头图用的 `header-readme.png` 是 `header.png` 缩到 1000px 宽、256 色后的版本（534K → 173K），官网分享卡片仍用原始 `header.png`。

## 功能一览

### 孩子端（KidsSafeDesktop）

| 模块 | 内容 |
|---|---|
| 童趣桌面 | 图标网格（Windows 换行 / 手机横向翻页）、主题切换、逐小时天气与预警、启动动画、护眼徽章 |
| 小游戏乐园 | 21 款原生小游戏：算术、数独、围棋、俄罗斯方块、迷宫、找颜色、打地鼠、打字、气球、听音辨位、识字、拼音、看图识字、安全问答、百科问答、心灵感应……统一难度与排行榜 |
| 游戏乐园 | 15 款完整网页游戏原样内置（雷霆战机、五子棋、贪吃蛇、消消乐、中国象棋、斗兽棋、点字成诗……），断网可玩 |
| 听一听（离线音频中心） | 儿歌音乐盒、安睡摇篮（雨声 / 海浪 / 白噪音）、睡前故事、国学启蒙、英语启蒙、科普知识；原生播放器 + 逐句高亮朗读 |
| 阅读 | 故事屋、绘本、古诗、电子课本书架（需自备教材文件）、文章阅读器 |
| 学习天地 | 一至九年级口算练习与批改（五～九年级走 SQLite 题库）、成绩报告、错题回顾、中文 TTS 报题 |
| 英语学习 | 按年级 / 主题的单词卡与练习模式，音标 + 英文 TTS |
| 关爱中心 | 心情打卡与 7 天趋势、加密日记（孩子自己设密码）、心理小课堂 10 节、呼吸放松、想对爸妈说、10 节家长课、0–15 岁成长指南、抑郁症专区（公益科普）、**绘本怎么找**（三家免费且合法的绘本来源 + 5 本经典绘本清单 + 网盘合集版权风险提示） |
| 安全与健康 | 安全知识（交通 / 防火 / 防骗 / 防溺水）、安全小问答、眼保健操、身体百科 |
| 激励 | 星星、勋章墙、连续打卡、排行榜 |
| 家长入口 | 4 位 PIN 保护的家长后台（本地），可改全部设置 |

### 家长端（KidsSafeParent，独立 App）

同一套家长中心页面，只是入口不同：启动直接进家长中心（仍需 PIN），不加载儿童桌面壳体。

- 使用时长与时段限制、应用白名单（枚举已安装应用）
- 游戏开关、主题、护眼模式（强度 + 定时）、在线内容总开关
- 小游戏工坊：内置网页编辑器，不写代码就能做「接住 / 躲避 / 点一点」小游戏 —— 三种模板、6 类内置 emoji 素材、可导入自己的图片，作品能试玩、能导出成 `.json` 换设备打开（Windows / Android / Linux / 鸿蒙四端都有）
- 成长指南、家长课、专项训练指南
- 连接家长服务器：填地址 + 账号密码或 6 位配对码，即可远程下发配置

### 服务端（KidsSafeServer，可选）

- **家长账号** —— 用户名 + 密码（PBKDF2-SHA256，10 万次迭代、16 字节盐），HMAC 自签令牌
- **设备配对** —— 6 位配对码，10 分钟有效、一次性；设备令牌**只读**，改配置返回 403
- **配置隔离** —— 每个账号一份 `data/profiles/<id>.json`，一台服务器服务多个家庭
- **实时下发** —— 长轮询 `/api/watch`，家长一保存，孩子设备 1 秒内应用，不用 WebSocket / 厂商推送
- **内容与版本** —— 更新日志、安装包列表、内容清单、故事代理（第三方密钥只留服务端）

## 官网与反馈

官网 <https://kids.siguangblog.cc/> 就是 `KidsSafeDesktop/Website/` 这一份纯静态站，丢到 Nginx / GitHub Pages / OSS 都能跑；首页截图与上面 README 用的是同一批素材。

对外有四个「能说话」的页面，接口都走 `KidsSafeServer` 的 `/api/feedback/*`：

- **`comments.html`** —— 公开评论区：页内直接发表、按平台与星级筛选、点赞、加载更多。
  数据来自 `data/comments.json`（14 条静态精选，改文件即改页面）+ 服务端实时留言，按正文去重后合并
- **`feedback.html`** —— 意见反馈 / 满意度调查 / 使用问卷三合一，右侧实时公开统计（份数、平均分、NPS、分项）。精选留言取 3 条
- **`feedback-admin.html`** —— 明细列表与 CSV 导出，需管理密钥（服务端 `Feedback:AdminKey`）
- **`care-sources.html`** —— 关爱中心的内容来源与引用，22 条出处逐条可点开，静态维护

几条刻意的取舍：

- **昵称可选，IP 不落库**：限流按 `X-Forwarded-For` 取真实 IP，只做计数（10 分钟 12 条 / 24 小时 60 条），不写进留言记录；
- **点赞只存本机**：不做服务端计数，不值得为了一个数字引入账号体系；
- **提交是乐观的**：接口返回成功就先把这条插进列表并标「刚刚提交」；断网时进 `localStorage` 队列，回到页面再补发；
- **静态精选优先**：`data/comments.json` 的条目排前面，服务端新留言按 `sortKey` 排在后面，两边同一条按正文前 40 字去重。

四端的入口、接口契约、存储与验收步骤见 **[Docs/用户反馈与问卷.md](KidsSafeDesktop/Docs/用户反馈与问卷.md)**；Nginx 的两处反代配置（`/api/`、`/patches/`）见 **[Docs/官网运维清单.md](KidsSafeDesktop/Docs/官网运维清单.md)**。

## 架构

```
┌────────────────────────┐        ┌────────────────────────┐
│  KidsSafeDesktop       │        │  KidsSafeParent        │
│  孩子端 App            │        │  家长端 App            │
│  App 外壳 + 平台实现   │        │  App 外壳 + 桩服务     │
└───────────┬────────────┘        └───────────┬────────────┘
            │      ProjectReference          │
            └───────────────┬────────────────┘
                            ▼
            ┌────────────────────────────────┐
            │  KidsSafeShared（类库）        │
            │  Pages / ViewModels / Services │
            │  Models / Helpers / Controls   │
            │  Converters（命名空间仍是      │
            │  KidsSafeDesktop.*，历史原因） │
            └───────────────┬────────────────┘
                            │ HTTP（可选，局域网）
                            ▼
            ┌────────────────────────────────┐
            │  KidsSafeServer（ASP.NET Core）│
            │  Minimal API + 静态家长后台    │
            │  data/ 下 JSON 文件存储        │
            └────────────────────────────────┘
```

三个客户端工程的命名空间都是 `KidsSafeDesktop.*`：拆分时为了让 340 多个文件一行 `using` 都不用改，刻意保留了原命名空间。**新增代码请沿用这个命名空间**。

## 快速开始

### 环境要求

- **.NET SDK** 10.0.100+ —— 本项目在 `10.0.401` 上开发
- **MAUI workload** `maui` —— 一次装 Android 与 Windows 两套，命令见下
- **Android SDK** API 36 平台 + API 21 最低版本 —— 只编译 Windows 时不需要
- **Xcode** 16+（仅 iOS）—— 只能在 macOS 上出包；Windows 可做编译检查
- **Windows** 10 1809（`10.0.17763`）以上 —— Windows App SDK 要求

首次装工作负载（只要其中一个平台，换成 `maui-android` 或 `maui-windows` 即可）：

```bash
dotnet workload install maui
```

### 编译客户端（Windows）

```bash
git clone https://github.com/<you>/KidsSafeDesktop.git
cd KidsSafeDesktop

# 孩子端
dotnet build KidsSafeDesktop/KidsSafeDesktop.csproj -f net10.0-windows10.0.19041.0

# 家长端
dotnet build KidsSafeParent/KidsSafeParent.csproj -f net10.0-windows10.0.19041.0
```

> **必须指定 `-f`**。工程是多目标的（`net10.0-android` + Windows），不指定会连 Android 一起还原，没装 Android SDK 会报 `NU1102` 之类的还原错误。

运行：

```bash
dotnet run --project KidsSafeDesktop/KidsSafeDesktop.csproj -f net10.0-windows10.0.19041.0
```

首次启动：隐私同意 → 启动动画 → 4 步引导（4 位家长 PIN、孩子名字与头像、每日使用时间、防退出权限——第 4 步只有 Android 有），之后进桌面。

### 编译 Android

```bash
dotnet build KidsSafeDesktop/KidsSafeDesktop.csproj -f net10.0-android
# APK 输出：KidsSafeDesktop/bin/Debug/net10.0-android/*-Signed.apk
```

签名配置见 [配置与密钥](#配置与密钥)。不做发布打包的话，可以直接用 Debug 签名装到手机上试。

### 编译 iOS

iOS 的完整构建、签名与上架流程见 **[Docs/iOS上架说明.md](KidsSafeDesktop/Docs/iOS上架说明.md)**，这里只给最短路径。

**Windows 上做编译检查**（产不出 .app，但能确认代码没写错）：

```bash
dotnet build KidsSafeDesktop/KidsSafeDesktop.csproj -f net10.0-ios -p:EnableIosTarget=true
```

**macOS 上出包**（需要 Xcode 16+，工作负载装 `ios` 与 `maui`）：

```bash
bash KidsSafeDesktop/Scripts/build-ios.sh sim    # 模拟器
bash KidsSafeDesktop/Scripts/build-ios.sh ipa    # Release + ipa（签名见 Signing.iOS.props.sample）
```

> iOS 目标默认**只在 macOS 上参与构建**：`TargetFrameworks` 用 `IsOSPlatform('osx')` 判断，Windows 上的日常构建完全不受影响，不需要额外参数。

### 运行服务端（可选）

```bash
cd KidsSafeServer
cp appsettings.sample.json appsettings.json   # 至少改掉 AdminPin
dotnet run
```

监听 `http://0.0.0.0:5180`（端口写死在 `Program.cs` 末尾，可用环境变量 `KS_URLS` 覆盖）。

1. 浏览器打开 `http://localhost:5180` → 首次会跳到注册页，注册一个家长账号（第一个账号即管理员）
2. 后台「👤 账号与设备」→ 生成 6 位配对码
3. 孩子端：家长后台 → 服务器地址填 `http://电脑局域网IP:5180`，凭证填这 6 位数字

## 配置与密钥

仓库**不包含**任何真实密钥。下面这些文件被 `.gitignore` 排除，需要自己准备；缺了也能编译运行，只是对应功能不可用。（路径里的 `KidsSafeDesktop/` 前缀为节省宽度省略）

- **`appsettings.json`**（`KidsSafeServer/`）—— 管理员 PIN、第三方故事 API 密钥。
  从 `appsettings.sample.json` 复制；缺失时故事代理不可用，其余正常。
- **`Signing.props`** —— Android keystore 别名与口令。
  从 `Signing.props.sample` 复制；缺失时 Android 包不签名。
- **`Keys/kidssafe.keystore`** —— Android 签名证书，自签一个即可，命令见下。
- **`mcs-services.json`**（`Android/Assets/`）—— 荣耀推送配置。
  不用荣耀推送可删除，同时删掉 csproj 里对应的 `AndroidAsset` 一行。
- **`*.aar`**（`Android/libs/`）—— 荣耀 Push Kit SDK。
  从[荣耀开发者平台](https://developer.honor.com/)自行下载，**不要提交到仓库**（非开源许可）。

生成模板与自签证书：

```bash
cp KidsSafeServer/appsettings.sample.json KidsSafeServer/appsettings.json
cp KidsSafeDesktop/Signing.props.sample KidsSafeDesktop/Signing.props

keytool -genkeypair -alias kidssafe -keyalg RSA -keysize 2048 \
  -validity 10000 -keystore KidsSafeDesktop/Keys/kidssafe.keystore
```

> ⚠️ 换 `ApplicationId` 时注意：Android 上改包名等于换一个 App，已安装的旧版本无法覆盖升级；Windows 侧 `WindowsPackageType=None`，走安装包而不是 MSIX。

**空密钥时的降级行为**：故事代理返回错误 JSON，客户端显示「暂时听不到新故事」，内置内容不受影响；天气走 Open-Meteo 公共接口，无需密钥，但可被家长关闭。

## 仓库结构

```
.
├── KidsSafeDesktop/          孩子端 App（外壳 + 平台实现 + 资源 + 官网 + 脚本）
│   ├── Platforms/            Android：MainActivity / 儿童模式 / 荣耀推送；Windows：WinUI 入口 / 应用枚举
│   ├── Resources/            图片、字体、Raw 网页与音频、图标、启动图
│   ├── Website/              官网静态站与更新日志（部署到任意静态托管）
│   ├── Scripts/              发布、部署、隐私政策导出、PDF 转页图等工具
│   ├── Installer/            Inno Setup 安装脚本
│   ├── Linux/                KDE Plasma 版前端（Qt 6 + Kirigami，独立于 MAUI）
│   ├── Harmony/              鸿蒙版前端（ArkTS + ArkUI，独立于 MAUI）
│   ├── Baby36Server/         「宝贝三十六计」联机陪练的本机小服务（纯 Node 内置模块，不装依赖）
│   ├── Video/                宣传片的采集 / 合成脚本（帧序列与成片不入库，见其 README）
│   └── Docs/                 设计与技术文档、更新记录、上架材料
├── KidsSafeShared/           共享库：全部页面、视图模型、服务、模型、控件
├── KidsSafeParent/           家长端 App（独立外壳，复用共享库）
├── KidsSafeServer/           ASP.NET Core 家长管理服务器
│   ├── Auth/                 账号、密码哈希、令牌、配对码
│   ├── wwwroot/              家长后台 / 登录页 / 文档页 / 下载页
│   └── data/                 运行时生成（账号、令牌密钥、各账号配置），不进仓库
├── LICENSE
└── README.md
```

## 文档

| 文档 | 内容 |
|---|---|
| [Docs/设计文档.md](KidsSafeDesktop/Docs/设计文档.md) | 产品定位、用户场景、25 项能力的设计取舍 |
| [Docs/技术文档.md](KidsSafeDesktop/Docs/技术文档.md) | 技术栈、关键实现（天气定位、护眼滤镜、TTS、时长守护） |
| [Docs/工程结构-家长端拆分.md](KidsSafeDesktop/Docs/工程结构-家长端拆分.md) | 三工程拆分的约定与踩坑记录 |
| [Docs/iOS上架说明.md](KidsSafeDesktop/Docs/iOS上架说明.md) | iOS 内容版的形态边界、构建签名、App Store 审核要点与自检清单 |
| [Docs/账号体系.md](KidsSafeDesktop/Docs/账号体系.md) | 账号 / 配对码 / 令牌设计与迁移路径 |
| [Docs/实时下发通道.md](KidsSafeDesktop/Docs/实时下发通道.md) | 长轮询版本号机制与重连策略 |
| [Docs/更新记录.md](KidsSafeDesktop/Docs/更新记录.md) | 逐版本变更（含服务端） |
| [Docs/小游戏工坊.md](KidsSafeDesktop/Docs/小游戏工坊.md) | 四端共用的网页编辑器：接口约定、作品存哪、改完怎么同步 |
| [Docs/星星奖励与关爱中心.md](KidsSafeDesktop/Docs/星星奖励与关爱中心.md) | 星星与勋章规则、关爱中心各栏目、绘本来源的存储方案与版权红线 |
| [Docs/用户反馈与问卷.md](KidsSafeDesktop/Docs/用户反馈与问卷.md) | 官网三页 + 四端入口、接口契约、离线队列、隐私边界、验收步骤 |
| [Docs/官网运维清单.md](KidsSafeDesktop/Docs/官网运维清单.md) | 官网与接口上线后的运维动作：Nginx 反代、缓存、备份 |
| [KidsSafeServer/wwwroot/docs.html](KidsSafeServer/wwwroot/docs.html) | 面向家长的部署与排障手册 |

## 已知限制

这些是真实存在的边界，不是待办清单的修辞：

- **主力是 Android 与 Windows**（同一份 MAUI 代码库）。**iOS 版已完成工程接入与编译验证**（`net10.0-ios` 目标在本机 0 错误编译，含音频播放、音效、护眼遮罩、隐私清单），但**尚未在真机 / 模拟器上实跑**——签名、上架与手感回归需要在 Mac + Apple 开发者账号下完成，步骤见 [Docs/iOS上架说明.md](KidsSafeDesktop/Docs/iOS上架说明.md)。
- **iOS 是内容版，不是受控桌面**：替换桌面、应用白名单、读取其他 App 使用时长、强制锁机都依赖 iOS 不开放的系统权限，iOS 上这些入口会隐藏或提示「仅安卓支持」。
- **Linux 与鸿蒙是另外两个独立前端**：`KidsSafeDesktop/Linux/`（Qt 6 + Kirigami，KDE Plasma 全屏儿童桌面）与 `KidsSafeDesktop/Harmony/`（ArkTS + ArkUI）。都不复用 XAML 层，功能与桌面端不完全对等，本次开源未做完整回归，差异写在各自目录的 README 里。
- **服务端本身不做 HTTPS，也不自带限流**。它默认只监听 `127.0.0.1:5180`，公网访问必须挂一层带 HTTPS 的反向代理（官网就是这么做的：nginx 把 `/api/*` 转到 5180，并按 `X-Forwarded-For` 让服务端拿到真实 IP）。**不要直接把 5180 端口映射到公网**。
- **家长端还没有自己的图标与发布脚本**，`ApplicationId` 还是占位的 com.companyname.kidssafeparent，打包前要手动改。
- **教材 PDF 不随仓库分发**。`KidsSafeServer/` 的 `wwwroot/books/` 下有 130 份教材，版权归各出版社，仓库只留书目元数据；没有 PDF 时书架是空的。
- **内容版权**。儿歌、白噪音为项目内合成；故事与绘本为原创或在公有领域素材基础上改写；诗词为公有领域。若要商用请自行复核 `Docs/第三方SDK清单.md`。
- **AI 故事默认关闭**，需要家长自备 OpenAI 兼容接口密钥（DeepSeek / 华为云 MaaS 等），开启意味着孩子的输入会发往第三方。
- **移动端手势、配对码输入只在桌面浏览器和模拟尺寸下验证过**，真机手感未做完整回归。

## 路线图

- [ ] 家长端独立图标、版本号与一键打包脚本
- [ ] Linux 与鸿蒙端做一次完整回归，把与桌面端的功能差异补齐
- [ ] 两端配置合并（服务端已就绪，客户端 LiveSync 已实现，缺完整的冲突合并策略）
- [ ] 多孩子档案（一个账号下多个孩子，各自时长与内容）
- [ ] 教材书架改用开放许可教材，去掉版权依赖
- [ ] iOS 真机与模拟器回归（工程与平台实现已完成，待 Mac + 开发者账号：签名、TestFlight、上架）；后续再说屏幕使用时间（FamilyControls）与 Singular iOS 归因
- [ ] 家长端 iOS（共享库已支持，加个 `net10.0-ios` 目标即可）
- [ ] 服务端：找回密码、审计日志、可选的 HTTPS 自签证书模式

## 参与贡献

欢迎 Issue 与 PR。提交前请确认：

1. `dotnet build` 四个工程 0 错误（改共享库要同时编孩子端和家长端，Windows 与 Android 各一次）；
2. 没有把 `appsettings.json`、`Signing.props`、`*.keystore`、`mcs-services.json` 提交上来；
3. 新增页面沿用 `KidsSafeDesktop.*` 命名空间，并在两个 App 的 `MauiProgram` 里各自注册所需服务；
4. 涉及界面的 PR 附截图（Android 竖屏 + Windows 各一张）。

代码风格没有强制工具链，跟随现有文件即可：**中文注释、文件头用 `/// <summary>` 说明「为什么这样做」而不只是「做了什么」**，这是这个仓库里最有价值的部分，改动时请保留。

## 许可证与免责声明

代码以 [MIT License](LICENSE) 发布，可自由使用、修改、商用，保留版权声明即可。

需要注意：

- 本项目是**家长辅助工具**，不能替代监护。任何儿童模式都存在绕过方式（重装、刷机、外接键鼠），请把它当作「降低概率」而不是「绝对防护」。
- 「关爱中心」中的心理科普内容与建议仅供参考，**不构成医疗诊断**。孩子出现持续情绪问题时，请寻求专业帮助。
- 仓库不含任何第三方受版权保护的教材、商业字体或推送 SDK；使用方自行引入的内容由使用方承担版权责任。
- 项目与任何设备厂商、内容平台无关联，未获其背书。
