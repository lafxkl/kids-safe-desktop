# 03 · 架构 · Linux 版

Linux 版是**独立实现**，不是把 MAUI 界面套一层壳。选 Qt 6 + Kirigami 的原因和
「为什么不给 MAUI 加个 Linux 目标」写在 `Linux/README.md` 第一节，这里只讲
落地结构。

## 1. 服务层：一个 C++ 类对应一个 `Services/I*.cs`

`main.cpp` 里逐个 new 出来，然后 `engine.rootContext()->setContextProperty(...)`
挂到 QML 上下文。QML 里直接写 `Settings.xxx`、`ScreenTime.xxx`，不需要 import。

| C++ 类（`src/core/`） | 对应接口 | 干什么 |
| --- | --- | --- |
| `Settings` | `ISettingsService` | 配置落盘、PIN、应用白名单、i18n |
| `ScreenTime` | `IScreenTimeService` | 计时、每日限额、到点锁屏 |
| `AppCatalog` | `IAppCatalogService` | 直接读 XDG 下的 `.desktop`，不依赖 ksycoca |
| `AppLauncher` | `IAppLauncherService` | 拉起外部应用 |
| `Kiosk` | `IKioskService` | 全屏、无边框、锁定标记、会话模式判断 |
| `StoryService` | `IStoryService` | 故事列表 / 正文，本地 + 在线 |
| `Weather` | — | Open-Meteo + 离线缓存，8 秒超时后按 30/60/120/300/900 秒退避 |
| `Netplay` | — | 联机房间 + 1.5 秒短轮询 |
| `Playland` / `Maker` | — | 网页小游戏 / 小游戏工坊（QtWebEngine） |
| `CodeLab` | — | 积木 → C 源码 → gcc 编译 → 判题 |
| `MediaPlayer` | — | 儿歌 / 环境音（QtMultimedia） |
| `NesEmulator` + `NesView` | — | SimpleNes 核心 + `QQuickPaintedItem` 画帧 |
| `VnEngine` | — | 吉里吉里游戏：起外部进程，不链核心 |
| `FileModel` | — | 文件管理器：目录模型、回收站、系统目录只读 |
| `Terminal` | — | `forkpty` 真 PTY + ANSI 解析 + 危险命令拦截 |
| `Processes` | — | 任务管理器：读 `/proc`，两次采样算 CPU，结束进程 |
| `Speech` | — | `spd-say` 朗读 |
| `Startup` / `HotkeyGuard` | — | 开机自启 / X11 抢快捷键 |

**刻意不用 KF6Service**：它的 `KApplicationTrader` 依赖 ksycoca 缓存和
`kbuildsycoca6` 进程，在 kiosk / 容器 / 精简系统里首次查询可能长时间不返回。
应用清单改为直接扫 XDG 目录下的 `.desktop` 文件（`appcatalog.cpp`）。

## 2. QML 组织：同一个目录，靠目录而不是 import

所有 `.qml` 平铺在 `src/qml/`，通过 `qt_add_resources(... PREFIX "/qml")` 打包。
同目录文件之间**无需 import 即可互相引用** —— 这是 qrc 打包下最不容易出错的
组织方式，代价是不能按功能分子目录（除了 `projects/`）。

```
Main.qml              根窗口 + StackView 导航 + 护眼遮罩
DesktopPage.qml       儿童桌面：壁纸 + 图标 + 程序坞 + 窗口管理
  DesktopWindow.qml   一扇窗口（拖动 / 最大化 / 关闭）
  DesktopIcon.qml     桌面图标
  DockButton.qml      程序坞按钮
*Page.qml              各功能页
*GamePage.qml         小游戏（数一数 / 拼图 / 找颜色 / 算术 / 打地鼠 / 华容道 / 象棋）
GameShell.qml         小游戏公共外壳（返回 / 计分 / 提示条）
CodeView.qml          只读代码框（绕开 Breeze 的 TextArea bug，见 06）
GlyphIcon.qml         自绘矢量图标（不依赖字体里有没有那些符号）
```

「不依赖字体」这条是硬规矩：`ChessPiece.qml` 用 Canvas 画棋子，
`GlyphIcon.qml` 画图标。系统字体缺字时，`♔` 这种字符会变成豆腐块，在儿童界面
上尤其难看且不可解释。

## 3. 三层锁定（真正锁住孩子的地方）

`session/` 下才是锁的本体，`src/` 只是界面。

| 层 | 做什么 | 做不到什么 |
| --- | --- | --- |
| 会话裁剪 | 不启 plasmashell、不加载全局快捷键服务；Wayland 用 kwin_wayland / cage | 用户另开会话（Ctrl+Alt+F3）就出去了 —— 这是保留出口 |
| 守护重启 | `kidssafe-session` 循环：非正常退出 1 秒后重启；家长点「退出」写 `.session_exit` 标记才真的退出 | 连续快挂 5 次才放弃（防止死循环刷屏） |
| 快捷键抢占 | X11 下 `XGrabKey` 抢 Alt+Tab / Alt+F4 / Ctrl+Shift+Esc | Wayland 没有这个能力，只能靠会话裁剪 |

`Ctrl+Alt+Del` 三端都拦不住，属于系统安全序列。

## 4. 数据落在哪

| 路径 | 内容 |
| --- | --- |
| `~/.config/kidssafe/kidssafe.conf` | 配置（`[General] OnboardingDone`、白名单、限额、PIN 哈希） |
| `~/.local/share/kidssafe/kidssafe.log` | 运行日志（发布版也开），排障第一现场 |
| `~/.local/share/kidssafe/games/` | 吉里吉里游戏目录 |
| `~/.local/share/kidssafe/.session_exit` | 家长「退出会话」标记 |
| `~/.local/share/kidssafe/trash/` | 文件管理器的回收站（不是系统回收站） |
| `~/.cache/kidssafe/weather.json` | 天气缓存，断网时首页不空 |
| `/usr/share/kidssafe/{playland,maker,codelab,audio}` | 随包内容（源码安装时在 `/usr/local/share`） |

想重看一遍引导页：把 `kidssafe.conf` 里的 `OnboardingDone` 改成 `false`。

## 5. 三个「自己写」的工具

不引开源组件的原因不是轮子情结，是这几个都要**受控**：

- **终端** `terminal.cpp`：`forkpty` 起真 PTY，`HISTFILE=/dev/null` 不落历史，
  危险命令（`rm -rf /`、`mkfs`、`dd of=/dev/sda`、fork 炸弹、关机、`sudo`）
  直接拦下来。只能算「够用的终端」：ANSI 只做了着色，跑 `vim`/`htop` 会花屏。
- **任务管理器** `proctree.cpp`：读 `/proc` 两次采样算 CPU，结束进程先
  `SIGTERM` 再 `SIGKILL`，两步确认。
- **文件管理器** `filemodel.cpp`：删除进自己的回收站（可还原），系统目录
  强制只读，数量上限截断。

## 6. 联机对战：为什么不 WebSocket

服务端只存**走法序列** `[{f,t,p}]`，双方从开局重放必然得到同一个棋盘 ——
于是不需要断线重连、不需要状态快照、不需要一致性协议。客户端 1.5 秒轮询一次，
按 `version` 丢弃迟到的响应；本地用 `game.turn` 判轮次（不信服务端回来的
`myTurn`，它有一轮延迟）。

只有 4 位数字房间号、熟人房、没有公开匹配、没有房间列表、没有聊天。
家长开关 `netplayEnabled` 默认关，且必须和 `onlineContentEnabled` 同时打开才生效。
