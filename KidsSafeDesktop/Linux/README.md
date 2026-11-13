# 儿童安全桌面 · Linux 版（KDE / Kirigami）

Windows + Android 工程之外的第三个前端，技术栈为 **Qt 6 + KF6 Kirigami**，
目标运行环境是 KDE Plasma 桌面。

---

## 一、为什么不是「给 MAUI 加个 Linux 目标」

.NET MAUI 官方只支持 Android / iOS / macOS / Windows，没有 Linux 后端
（社区版早已停更）。所以 Linux 版无法复用 XAML 层，必须换 UI 框架。

好消息是原工程的平台能力被 6 个接口切得很干净，模型与服务层大部分是纯 .NET：

| 层 | 行数 | 现状 |
|---|---|---|
| Pages（87 个 XAML + 12 个 IDrawable） | — | 需重写，本仓库逐步替换为 QML |
| ViewModels | 7,225 | 逻辑可参考，绑定改写成 QML 属性 |
| Helpers | 2,213 | 大部分算法可直译 |
| Services | 5,064 | 大部分可直译（HTTP / 内容 / 考卷评分等） |
| Models | 6,677 | 数据结构可直接对应 |

---

## 二、目录结构

```
Linux/
├── CMakeLists.txt
├── src/
│   ├── main.cpp                 # 服务注册 + 加载 QML
│   ├── core/                    # 对应 C# Services 的平台能力
│   │   ├── settings.*           # ← ISettingsService / ChildSettings
│   │   ├── screentime.*         # ← IScreenTimeService
│   │   ├── appcatalog.*         # ← IAppCatalogService
│   │   ├── applauncher.*        # ← IAppLauncherService
│   │   ├── kiosk.*              # ← IKioskService
│   │   ├── storyservice.*       # ← IStoryService
│   │   └── speech.*             # ← Helpers/Tts.cs
│   └── qml/                     # 同目录组织，互相引用无需 import
│       ├── Main.qml             # 根窗口 + 导航栈
│       ├── Backdrop / LargeButton / PinPad / AppTile
│       ├── SplashPage.qml
│       ├── OnboardingPage.qml
│       ├── ParentGatePage.qml
│       ├── HomePage.qml
│       ├── LockoutPage.qml
│       ├── ParentDashboardPage.qml
│       ├── StoryHousePage.qml   # ← StoryHousePage / StoryListPage
│       └── StoryReaderPage.qml  # ← StoryReaderPage / StoryPlayerPage
├── session/                     # 会话层（真正的锁定在这里）
│   ├── kidssafe.desktop         # X11 会话入口
│   ├── kidssafe-wayland.desktop # Wayland 会话入口
│   ├── kidssafe-session         # 启动 + 崩溃守护
│   └── kidssafe-wayland-session # 合成器 + 启动
└── scripts/
    ├── install.sh
    └── check-host.sh            # 环境与会话检查（只读）
```

---

## 三、构建

```sh
# Debian / Ubuntu
sudo apt install build-essential cmake extra-cmake-modules \
     qt6-base-dev qt6-declarative-dev \
     libkf6config-dev libkf6coreaddons-dev libkf6service-dev \
     libkf6kirigami-dev libkf6i18n-dev \
     qml6-module-qtquick qml6-module-qtquick-controls qml6-module-qtquick-layouts \
     qqc2-breeze-style

# Fedora
sudo dnf install cmake gcc-c++ extra-cmake-modules \
     qt6-qtbase-devel qt6-qtdeclarative-devel \
     kf6-kconfig-devel kf6-kcoreaddons-devel kf6-kservice-devel \
     kf6-kirigami-devel kf6-ki18n-devel

./scripts/install.sh
```

调试运行（不改会话，只看界面）：

```sh
KIDSSAFE_KIOSK_SESSION=1 ./build/kidssafe
```

> 这样跑只是全屏应用，**没有会话裁剪，不是真正的锁定状态**。
> 验证锁定效果必须注销后在登录界面选「儿童安全桌面」会话。

装好之后跑一遍环境检查（只读，不改系统）：

```sh
./scripts/check-host.sh
```

它会逐项检查会话文件、QML 模块、合成器、语音包、应用清单，并调用
`kidssafe --self-test`（无界面自检，可在 SSH / CI 里跑）确认配置可写、
应用扫描正常、QML 能实例化。最后会列出必须人工确认的 5 项锁定验证。

---

## 四、Linux 与 Windows 的能力差异（重要）

这是移植时最容易踩空的地方。Windows 版的做法在 Wayland 下**根本无法实现**：

| 能力 | Windows 实现 | Linux 实现 |
|---|---|---|
| 屏蔽 Win / Alt+Tab / Alt+F4 | `SetWindowsHookEx` 低级键盘钩子 | **做不到**。改为会话层：不加载 plasmashell，没有可切换的窗口 |
| 隐藏任务栏 | `FindWindow` + `ShowWindow(SW_HIDE)` | 不存在任务栏（会话里没有 shell） |
| 强制置顶 | `AppWindow` / `SetWindowPos` | 会话内唯一窗口，无需置顶 |
| 崩溃后自救 | 标记文件 + 退出钩子 + 恢复任务栏 | 会话脚本守护循环，非正常退出 1 秒重启 |
| 枚举已安装应用 | COM 枚举开始菜单快捷方式 | `KApplicationTrader` 扫 XDG `.desktop` |
| 启动应用 | `ShellExecute` | `KProcess` 持子进程（才能拿到退出信号） |
| 前台窗口监控 | `GetForegroundWindow` 轮询 | KWin 脚本 / KWindowSystem（待实现） |
| 护眼暖色滤镜 | 全屏遮罩 | 同左，QML 层遮罩 |
| TTS | SAPI | `speech-dispatcher`（`spd-say`） |

**一句话原则**：Linux 侧的可靠性来自「会话裁剪」，不来自应用内的小动作。
不要在 `kiosk.cpp` 里试图复刻 Windows 的按键钩子。

---

## 五、移植进度

已打通的主链路：**启动 → 引导 → 家长验证 → 桌面 → 启动应用 → 超时锁屏 → 家长延长**。

已移植：

- [x] 启动页 / 首次引导（昵称、家长密码、应用白名单）
- [x] 家长验证（数字键盘，带错误抖动）
- [x] 儿童桌面（内置内容入口 + 应用磁贴网格、剩余时间、外部应用遮罩）
- [x] 超时锁屏 + 家长延长
- [x] 家长后台（时长、临时加时、白名单、服务器地址、退出）
- [x] 故事屋：分类浏览 / 搜索 / 分页 / 正文阅读 / 朗读
- [x] 6 个平台服务 + 无界面自检（`--self-test`）

待移植（按优先级）：

- [ ] 学习世界 / 游戏乐园 / 媒体中心 / 电子课本
- [ ] 12 个手绘游戏（`IDrawable` → QML `Canvas`，算法可直译）
- [ ] 家长后台剩余页：内容审核、排行榜、服务器同步、护眼设置
- [ ] 前台窗口监控（KWin 脚本，用于「孩子在别的应用里待了多久」）
- [ ] 打包：Flatpak 或发行版包（`.desktop` + 会话文件 + 图标）

---

## 六、已知限制

- **代码尚未在 Linux 上编译过**。首个编译环境请用 `./scripts/install.sh`，
  报错信息如果能贴回来看，改起来会快很多。
- **故事屋需要家长自建 KidsSafeServer**。没填地址时页面会显示「家长还没开启
  在线故事」，这是预期行为，不是 bug。服务端契约见 `storyservice.h` 头部注释。
- **Wayland 会话尚未在真机验证**。`kidssafe-wayland-session` 里合成器的
  具体行为随 KWin 版本变化，必须在目标发行版上实测后再定稿。
- **中文语音包可能需要手动安装**：`sudo apt install speech-dispatcher-espeak-ng`
  并确认 `spd-say -l zh "测试"` 能出声，否则 TTS 静默降级。
- **应用图标用字母头像**而非真实图标，原因见 `AppTile.qml` 注释。
- 家长密码是 4 位数字，加了随机盐后存 KConfig。安全性足够防孩子，
  不足以防拿到磁盘的人——这是产品定位决定的，不是实现缺陷。
