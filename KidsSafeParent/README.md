# KidsSafeParent · 家长端

独立的孩子家长 App。与孩子端（`KidsSafeDesktop`）共用 [`../KidsSafeShared`](../KidsSafeShared) 里的全部页面，区别只有三点：**入口**、**平台能力**、**打包配置**。

> 为什么拆出来：原先家长中心和孩子桌面在同一个工程里，只靠一道 PIN 门挡着。但家长想在自己手机上改设置时，得装一个「儿童桌面」App 再摸进隐藏入口，很别扭。拆分后孩子端仍保留 PIN 保护的家长入口（不做功能删减），家长端则是一个正常的家长工具。

## 和孩子端的差异

| | 孩子端 | 家长端 |
|---|---|---|
| 启动页 | `PrivacyConsentPage` → `SplashPage` → 桌面 | `ParentGatePage`（PIN 校验）→ 家长中心 |
| 平台服务 | 真实实现：枚举已装应用、启动外部应用、锁屏、音效、荣耀推送 | `Stub*` 空实现 |
| Android | `MainActivity` 541 行：LOCK_TASK、沉浸式、返回键拦截、推送 | 20 行普通 `MainActivity` |
| Windows | 隐藏任务栏、Kiosk、异常退出兜底 | 普通 WinUI 窗口，不接管系统 |
| 图标 | 儿童桌面图标 | 占位图标（见下方 TODO） |
| 打包脚本 | `release.bat` / `build-installer.bat` | 无，需手动 `dotnet publish` |

启动流程：

```
ParentGatePage（家长 PIN）
      ↓
ParentDashboardPage（家长中心：时长 / 应用白名单 / 游戏 / 主题 / 护眼 / 在线内容）
      ├── ParentGuidePage（成长指南）
      ├── CareLessonPage（家长课 10 节）
      ├── SpecialParentGuidePage（专项训练指南）
      └── AccountPage（连接家长服务器：地址 + 账号密码 或 6 位配对码）
```

## 构建

```bash
dotnet build KidsSafeParent.csproj -f net10.0-windows10.0.19041.0
dotnet run   --project KidsSafeParent.csproj -f net10.0-windows10.0.19041.0

dotnet build KidsSafeParent.csproj -f net10.0-android
```

Android 打包复用孩子端的签名配置（`../KidsSafeDesktop/Signing.props`），这样两端证书一致；如果该文件不存在，`Import` 会被条件跳过，构建仍可继续（只是不签名）。

## 待完善

- [ ] 自己的应用图标与启动图（现在用共享的 `appicon.svg`）
- [ ] `ApplicationId` 现在是占位的 `com.companyname.kidssafeparent`，上架前必须改
- [ ] 版本号仍是 `1.0 / 1`，没有跟孩子端同步的发布脚本
- [ ] 家长端不注册推送，也就收不到「孩子完成任务」这类通知——需要时再补

## 新增页面时

页面加在 `KidsSafeShared/Pages/`，不要加在这里。这里只有 `App.xaml(.cs)`、`MauiProgram.cs`、`Platforms/`、`Resources/`。

如果新页面用到了新的平台能力，在 `MauiProgram.cs` 里给家长端注册一个桩实现，否则会 `null` 注入、页面打开即崩。
