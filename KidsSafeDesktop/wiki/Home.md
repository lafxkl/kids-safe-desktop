# 儿童安全桌面 · Wiki

给小朋友的专属桌面：开机就在里面，只能玩该玩的、看该看的，到点自己锁上。
本 wiki 是**仓库内文档的唯一入口**：原来散在 `Docs/`、`Linux/README.md`、
`Harmony/README.md` 里的内容，在这里按「想干什么」重新组织了一遍，每条都能
追到原始文件。

## 先看这三页

| 你的目的 | 看这里 |
| --- | --- |
| 我要把它编译起来跑一遍 | [02 构建与运行](02-构建与运行.md) |
| 我要打一个能发给家长的安装包 | [05 打包与分发](05-打包与分发.md) |
| 装好了但打不开 / 界面白屏 / 报错 | [06 排障手册](06-排障手册.md) |

## 全部页面

| 页面 | 讲什么 |
| --- | --- |
| [01 项目全貌](01-项目全貌.md) | 四个端怎么分工、代码放在哪、一份代码怎么跑在三种系统上 |
| [02 构建与运行](02-构建与运行.md) | Windows / Android / iOS / Linux / 鸿蒙 各自的构建命令与依赖 |
| [03 架构 · Linux 版](03-架构-Linux版.md) | Qt 服务注册表、QML 组织、kiosk 三层锁定、数据落盘位置 |
| [04 功能地图](04-功能地图.md) | 每个页面是干什么的、内容从哪来、孩子能玩到哪一步 |
| [05 打包与分发](05-打包与分发.md) | 薄包 / 自带包 / 绿色包的区别，自带 WebEngine 是怎么做到的 |
| [06 排障手册](06-排障手册.md) | 日志里的每一类报错对应什么原因、怎么改 |
| [07 数据与合规](07-数据与合规.md) | 隐私、第三方 SDK、账号与配对、软著与上架材料 |
| [08 文档索引](08-文档索引.md) | 仓库里每一个 `.md` 的一句话说明（含 `Docs/` 全部 33 篇） |

## 平台与版本基线

| 端 | 技术栈 | 工程位置 | 当前版本 |
| --- | --- | --- | --- |
| Windows | .NET MAUI（WinUI 3） | `KidsSafeDesktop/` + `KidsSafeShared/` | v1.20 |
| Android | .NET MAUI | 同上 | v1.20 / versionCode 21 |
| iOS | .NET MAUI（内容版） | 同上（`net10.0-ios`） | v1.20 起 |
| Linux | Qt 6 + Kirigami（C++/QML） | `Linux/` | 1.19.0 |
| 鸿蒙 | ArkTS + ArkUI（Stage 模型） | `Harmony/` | 跟随 MAUI 版本 |

「孩子端 / 家长端 / 共享库」三个 MAUI 工程的分工见
[01 项目全貌](01-项目全貌.md)，原始说明在 `Docs/工程结构-家长端拆分.md`。

## 三条命令先跑起来（Linux）

```sh
sudo ./scripts/install-deps.sh      # 装构建依赖（Debian / Ubuntu / deepin）
./scripts/install.sh                # 编译 + 装到 /usr/local
./build/kidssafe --windowed         # 窗口模式调试（别用全屏，见 06 排障）
```

打一个自带运行时的安装包（用户机器不需要装任何 qt6-* 包）：

```sh
./scripts/bundle-runtime.sh --deb
sudo apt install dist/kidssafe-bundled_1.19.0_amd64.deb
```

## 一句话原则

这个程序装的**不是一个应用，是一个登录会话**。所以它要写
`/usr/share/xsessions`、要能在登录界面被选中、要在 Wayland 下裁掉窗口管理器
—— 于是 `.deb` 是主推形式，AppImage / Flatpak / Snap 都只能试用（原因见
[05 打包与分发](05-打包与分发.md)）。理解这一点，后面所有的设计取舍都好解释了。
