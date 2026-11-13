# KidsSafeDesktop · 孩子端

孩子端的 **App 外壳**。界面与业务逻辑全部在 [`../KidsSafeShared`](../KidsSafeShared)，这里只放「每个 App 必须各自拥有」的东西：入口、平台实现、图标与资源、打包配置、发布脚本。

> 为什么页面不在这里：孩子端和家长端（`../KidsSafeParent`）共用同一套 87 个页面。拆成独立工程时把 `Pages / ViewModels / Services / Models / Helpers / Controls / Converters` 整体搬到了 `KidsSafeShared`，命名空间仍是 `KidsSafeDesktop.*`，所以这里的 `using` 看起来像在引用自己，其实是引用共享库。

## 目录

| 路径 | 作用 |
|---|---|
| `App.xaml(.cs)` | 入口。未同意隐私政策 → `PrivacyConsentPage`；否则 → `SplashPage`。窗口级效果（护眼遮罩）与前后台生命周期也在这里转发 |
| `MauiProgram.cs` | DI 注册：真实服务 + 平台服务（`Platforms.Android.*` / `Platforms.Windows.*`） |
| `Platforms/Android/` | `MainActivity`（儿童模式：锁任务、沉浸式、返回键拦截）、荣耀推送、`libs/*.aar`、`Assets/mcs-services.json` |
| `Platforms/Windows/` | WinUI 入口、应用目录枚举、原生音频/音效；启动时注册任务栏恢复兜底（Kiosk 本体在共享库的 `Platforms/Windows/WindowsKioskService.cs`） |
| `Resources/` | 图片、字体、`Raw/`（内置网页、音频、题库、游戏）、图标、启动图、样式 |
| `Installer/` | Inno Setup 脚本（`KidsSafeDesktop.iss`），装当前用户、不弹 UAC |
| `Website/` | 官网静态站（首页、下载、更新日志、隐私政策、来源页）与 `assets/` 截图 |
| `Linux/` | KDE Plasma 版前端（Qt 6 + Kirigami），独立实现，见其 README |
| `Scripts/` | 发布、安装包、部署、隐私政策导出、PDF 转页图等 PowerShell / Python 工具 |
| `Docs/` | 设计文档、技术文档、更新记录、账号体系、实时下发通道、上架材料 |

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

## 官网

`Website/` 是纯静态站，可直接丢到任意静态托管（Nginx / GitHub Pages / OSS）。`Scripts/deploy.ps1` 用 `deploy.config.json` 里的 SSH 目标上传，属于作者自己的部署方式，不是必需步骤。
