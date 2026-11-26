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
│   │   ├── eyecare.*            # ← Helpers/EyeCare.cs（护眼判定）
│   │   ├── themeicon.*          # ← 应用图标（QIcon 主题 → image://theme/…）
│   │   ├── weather.*            # ← IWeatherService（Open-Meteo + 离线缓存）
│   │   ├── vnengine.*           # 吉里吉里游戏启动桥（外部进程，不链核心）
│   │   ├── mediaplayer.*        # 媒体中心：内容表 + QtMultimedia 播放（可选依赖）
│   │   ├── codelab.*            # 小小程序员：积木 → C 源码 → gcc 编译 + 判题
│   │   └── speech.*             # ← Helpers/Tts.cs
│   ├── nes/                     # 红白机（SimpleNes，MIT）
│   │   ├── core/                # 上游核心原样存放（纯 C++17，零 Qt 依赖）
│   │   ├── nesemulator.*        # 帧循环 / 调色板 / 音频 / 存档
│   │   └── nesview.*            # QQuickPaintedItem，把一帧画到 QML
│   └── qml/                     # 同目录组织，互相引用无需 import
│       ├── Main.qml             # 根窗口 + 导航栈 + 护眼遮罩
│       ├── Backdrop / LargeButton / PinPad / AppTile / HomeBanner
│       ├── GlyphIcon.qml        # 自绘矢量图标（书架 / 盾牌 / 沙漏 / 天气 / 头像…）
│       ├── WeatherCard.qml      # 首页天气条（实况 + 逐小时）
│       ├── SplashPage.qml
│       ├── OnboardingPage.qml
│       ├── ParentGatePage.qml
│       ├── HomePage.qml
│       ├── LockoutPage.qml
│       ├── ParentDashboardPage.qml
│       ├── StoryHousePage.qml   # ← StoryHousePage / StoryListPage
│       ├── StoryReaderPage.qml  # ← StoryReaderPage / StoryPlayerPage
│       ├── SafetyWorldPage.qml  # ← SafetyKnowledgePage（10 个主题）
│       ├── SafetyTopicPage.qml  # ← SafetyKnowledgeDetailPage
│       ├── SafetyQuizPage.qml   # ← SafetyQuizGamePage
│       ├── SafetyData.js        # 52 条安全贴士（与三端同源）
│       ├── SafetyQuiz.js        # 40 道安全问答
│       ├── GamesHubPage.qml      # ← GamesHubPage（难度 + 游戏磁贴）
│       ├── GameShell.qml         # 小游戏公共外壳（返回 / 计分 / 提示条）
│       ├── GameResultOverlay.qml # 结算面板
│       ├── NesPage.qml           # 红白机（选 ROM + 玩）
│       ├── VnPage.qml            # 故事剧场（吉里吉里游戏，外部引擎）
│       ├── MediaHubPage.qml      # 媒体中心（儿歌 / 环境音 / 国学朗读）
│       ├── CodeStudioPage.qml    # 小小程序员（积木 + 关卡 + C 代码预览）
│       ├── ProjectLabPage.qml    # 项目工坊（拆三个现成作品 + 调参数）
│       ├── projects/             # 项目素材：PlaneWar / Sokoban / Platformer
│       ├── KlotskiGamePage.qml   # 华容道（三套布局 A* 验过可解；支持联机竞速）
│       ├── ChessGamePage.qml     # 国际象棋（人机 + 联机对战共用一页）
│       ├── ChessPiece.qml        # 棋子：Canvas 手绘，不依赖字体
│       ├── ChessEngine.js        # 走子规则 + 评估 + 搜索（纯函数，可用 node 跑 perft）
│       ├── NetplayPanel.qml      # 联机面板（开房 / 输 4 位号码 / 等人 / 掉线）
│       └── *GamePage.qml         # 数一数 / 拼图配对 / 找颜色 / 算术乐园 / 打地鼠
├── server/                      # 故事服务端（Python 3，零依赖）
│   ├── kidssafe_story_server.py # story / learn / game 三组接口 + 家长后台网页
│   ├── data/stories.json        # 内容库（示例 19 篇）
│   ├── data/stories/            # 或往这里丢 txt，按目录分分类
│   ├── web/index.html           # 家长后台（只读查看）
│   ├── install-server.sh        # 装成 systemd 用户服务
│   └── selftest.py              # 67 项接口自测（含联机房间生命周期）
├── session/                     # 会话层（真正的锁定在这里）
│   ├── kidssafe.desktop         # X11 会话入口
│   ├── kidssafe-wayland.desktop # Wayland 会话入口
│   ├── kidssafe-session         # 启动 + 崩溃守护
│   └── kidssafe-wayland-session # 合成器 + 启动
├── scripts/
│   ├── install-deps.sh          # 装构建依赖（Debian / Ubuntu）
│   ├── install.sh
│   └── check-host.sh            # 环境与会话检查（只读）
└── tests/
    ├── chess_engine_test.js     # 引擎离线校验（perft + 规则 + 自对弈）
    └── netplay_test.js          # 联机同步校验（起真服务端，两台客户端对下）
```

---

## 三、构建

```sh
# Debian / Ubuntu（一条命令装齐，见 scripts/install-deps.sh）
sh scripts/install-deps.sh

# 等价的手工命令（下面这张表是 Debian 系真实包名，别抄 Fedora 的写法）：
sudo apt install build-essential cmake extra-cmake-modules \
     qt6-base-dev qt6-declarative-dev \
     libkf6config-dev libkf6coreaddons-dev libkf6i18n-dev \
     libkirigami-dev qml6-module-org-kde-kirigami \
     qml6-module-qtquick qml6-module-qtquick-window \
     qml6-module-qtquick-controls qml6-module-qtquick-layouts \
     qml6-module-qtquick-templates qml6-module-qtqml-workerscript \
     qml6-module-org-kde-breeze

# Fedora
sudo dnf install cmake gcc-c++ extra-cmake-modules \
     qt6-qtbase-devel qt6-qtdeclarative-devel \
     kf6-kconfig-devel kf6-kcoreaddons-devel kf6-kservice-devel \
     kf6-kirigami-devel kf6-ki18n-devel

# deepin 25（注意与 Debian/Ubuntu 的四处差异，见下）
sudo apt install build-essential cmake extra-cmake-modules \
     qt6-base-dev qt6-declarative-dev \
     libkf6config-dev libkf6coreaddons-dev libkf6i18n-dev \
     libkirigami-dev qml6-module-org-kde-kirigami \
     qml6-module-qtquick qml6-module-qtquick-controls qml6-module-qtquick-layouts \
     qml6-module-qtquick-templates qml6-module-qtqml-workerscript

# libxkbcommon-dev / libxkbcommon-x11-dev 不是本工程的依赖（代码里一行都没用），
# 只有 cmake 报 XKB 相关错误时才需要临时装一下：deepin 25 的源里这两包不一定有，
# 写进 debian/control 会让 dpkg-checkbuilddeps 直接拒绝打包。

# 可选：语音朗读（缺了只降级成静默，不阻断）
sudo apt install speech-dispatcher

# 可选：网页小游戏（15 款 HTML5，用 QtWebEngine 原样内嵌）
# 不装也能编译，只是游戏乐园里没有「网页游戏乐园」入口，会显示一行安装提示
# 注意两个包都要装：-dev 是编译期头文件，qml6-module-qtwebengine 是运行期 QML 模块，
# 只装前者能编过，一打开网页页就报 module "QtWebEngine" is not installed
sudo apt install qt6-webengine-dev qml6-module-qtwebengine

# 可选：红白机声音（缺了能玩，只是静音，页面会提示「没有声音」）
# 只影响红白机，其它功能完全不受影响
sudo apt install qt6-multimedia-dev

./scripts/install.sh
```

装到 `/usr/local` 就是自己用。要做成能发给别人的发行包（`.deb`），见第七节。

### deepin 25 上的几处坑

1. **源必须是 `crimson` 套件**：

   ```
   deb https://community-packages.deepin.com/deepin/beige/ crimson main commercial community
   ```

   同一路径下的 `beige` 套件也存在（HTTP 200，apt 不会报错），但里面**没有 KF6**、
   Qt 也停在旧版，选错会连续报"无法定位软件包"和"破坏软件包间的依赖关系"。
   `VERSION_CODENAME` 在 deepin 25 上恰好是 `beige`，极容易照抄错。
2. **kirigami 开发包叫 `libkirigami-dev`**（Ubuntu 24.04 起也是这个名字），
   不是 `libkf6kirigami-dev`。KF6 那一堆框架都带 `kf6` 前缀
   （`libkf6config-dev`、`libkf6i18n-dev` …），只有 Kirigami 例外——上游库名
   本来就叫 `Kirigami`。写错的后果不是「少装一个包」：apt 一遇上
   「无法定位软件包」就整条命令中止，同一条命令里其它包也不会装。
3. **没有 `qqc2-breeze-style`**（那是 Fedora / Arch 的名字）。Ubuntu 上是
   `qml6-module-org-kde-breeze`，deepin 干脆没有（它用自家 DTK 风格）。
   这一条踩错会直接黑屏：
   `QQuickStyle::setStyle("org.kde.breeze")` 指向不存在的样式模块时，Qt **不会**
   悄悄退回默认样式，而是让第一个 `import QtQuick.Controls` 的文件报
   `module "org.kde.breeze" is not installed`，整个界面加载失败。`main.cpp`
   现在会先按 QML 导入路径探测 `org/kde/breeze/qmldir`，缺了就保持内置样式。
   另外 deepin 的 KF6 可能缺 `KF6Config.cmake` 总入口，
   `CMakeLists.txt` 已做了逐个 `find_package(KF6Xxx)` 的兜底。
4. **不需要 `libkf6service-bin` / `kbuildsycoca6`**：应用清单是自己遍历 XDG
   目录读 `.desktop`，不走 KService。原先用 `KApplicationTrader` 时踩过两个坑：
   缓存没生成时它安静返回空表（家长后台「可加入白名单的应用」为 0），以及
   首次查询要等 `kbuildsycoca6` 建库，在精简系统上会卡住——表现就是界面停在
   启动闪屏不动。
5. **深色主题下控件会「消失」**：界面配色写在 QML 里（米色背景 / 白色卡片 /
   棕色文字），但 `TextField`、`Switch` 这类控件的外观取自系统调色板。系统是
   深色主题时，输入框变成白字白底，摆在白色卡片上等于看不见（引导页的姓名
   输入框就这么"没了"），开关则变成一颗纯黑药丸。`main.cpp` 现在固定了一套
   浅色调色板，引导页的输入框另外自己画背景和边框，不依赖任何样式。
6. **`TapHandler` 里取不到 `Window.window`**：`Window.window` 是附加属性，靠
   「item → 所在窗口」反查，而 `TapHandler` / `HoverHandler` 这些 handler 不是
   `Item`，取到的永远是 `null`，于是 `null.pushPage(…)` 抛 TypeError、界面点了
   没反应（日志里是一行 `TypeError: Cannot call method 'pushPage' of null`）。
   `MouseArea` 是 `Item`，同样的写法在它里面反而正常，所以这个坑只在个别按钮上
   显形。现在各页面统一在根 `Item` 上写 `readonly property var navWin: Window.window`，
   页面内一律用 `page.navWin.…`，handler 里也不会再踩。
7. **`anchors.centerIn` 不能指向 Layout 的子项**：`ColumnLayout`/`RowLayout` 的子项
   坐标由布局器给，被跨层级 `anchors.centerIn: list`（list 是布局子项）锚定时，
   deepin / Qt 6.8 上实测不生效，元素会堆到左上角。症状很像「标题和图标叠在一起」：
   HomePage 的空状态、StoryHousePage 的离线提示都这样糊在左上角。做法是**把目标
   和提示一起放进一个普通 `Item`**，Item 带 `Layout.fillWidth/fillHeight`，
   内容的 `anchors.fill: parent`、提示的 `anchors.centerIn: parent`。
   注意放进普通 Item 之后，原来写 `Layout.fillWidth` 的子项要改成 `anchors.fill`，
   `Layout.*` 在非 Layout 环境里是空操作（会变成 0 尺寸）。
8. **emoji 会被渲染成黑白线稿**：deepin 默认只装黑白符号字体，没有 Noto Color Emoji，
   `📖 🛡️ ⏳ 🚀` 这些在界面上全变成单色轮廓，和 Windows 端的彩色贴纸完全不是一回事。
   不能要求用户为了一个儿童桌面去装字体，所以关键图标全部改成 **Canvas 手绘**
   （`GlyphIcon.qml`，坐标固定 100×100，随尺寸缩放），不依赖任何字体和图标主题。
   应用磁贴上的真实图标走另一条路：C++ 的 `ThemeIconProvider` 把
   `QIcon::fromTheme()` 渲染成 `image://theme/<图标名>`，QML 侧 `Image` 直接显示。
9. **`default property alias` 会把组件自己的子项也塞进内容区**：写
   `default property alias content: contentArea.data` 想让使用者往里塞内容时，
   组件自己声明的背景和顶部栏也会一并进 `contentArea`，而内容区尺寸又取决于这些
   子项 —— 布局无法收敛，界面整块空白，日志里什么都没有。`GameShell.qml` 改用
   「内容区与被注入的子项做兄弟，子页面 `anchors.fill: page.contentArea`」。
10. **`StackView.pop(item)` 不等于「返回上一页」**：Qt 6 里 `pop(item)` 的语义是
   「一直弹到该 item 成为当前页为止」。页面里写 `nav.pop(page)`（把当前页自己传进去）
   会被判定为「已经是目标页」，**一页都不弹，也不报错**，日志只有 `pop depth 3 -> 3`，
   界面上就是「点返回没反应」。所有页面统一调无参的 `popPage()`；`Main.qml`
   另外加了栈底判断和 300ms 去抖（防孩子连点把上一页也弹掉）。
   排查心得：**先去日志里看 depth 变没变** —— 变了是渲染问题，没变是导航问题。
11. **`Kirigami.ApplicationWindow` 会打一行平台插件警告**：启动时
   `Failed to find a Kirigami platform plugin for style "Chameleon"`，
   deepin 上没有这个插件。本应用配色/字号全写死，不需要 Kirigami 主题体系，
   窗口直接用 `QtQuick.Controls` 的 `ApplicationWindow`，警告消失。
   （`DGuiApplicationHelper: Don't use setPalette` 那行来自 DTK，无害，保留不改。）
12. **引用了不存在的 `id` 时，界面是「空白」而不是「报错」**：打地鼠页里格里写
   `width: board.cellSize`，但 `Grid` 忘了写 `id: board`。QML 只在控制台留一行
   `ReferenceError: board is not defined`，格子宽高取到 0，九个洞全部零尺寸 ——
   界面上只剩背景和顶栏，看着像「页面没写」。凡是「整块内容不见了」，先看控制台
   有没有 ReferenceError，再确认每个 `xxx.yyy` 里的 `xxx` 都有对应的 `id:`。

13. **`ColumnLayout` 空间不够时不会裁剪，而是「溢出到外面」**：安全学堂的卡片里
   写了「图标 / 标题 / 贴士数 / 弹性空白 / 读一读」五段，卡片高 170、内容要 190，
   布局不会报错，`读一读` 直接跑到卡片下沿之外，看着像掉在卡片下面。
   两条改法一起用：① 卡片高度按固定内容倒推（`Math.max(182, cellWidth * 0.66)`）；
   ② 末尾那行改成 `anchors.bottom: parent.bottom` 贴底，不再参与弹性分配。

14. **默认头像 `🐯` 没进映射表 = 左上角一团黑**：`Settings` 的默认头像是 Windows 端的
   `🐯`，而 `HomePage.avatarGlyph()` 只映射了几种，没命中的会退回 `Text` 显示 emoji，
   在 deepin 上就是黑色轮廓。凡是「图片/图标显示成黑色剪影」，先确认它是不是走了
   emoji 文本兜底。新增头像时记得同时改 `avatarGlyph()` 的 `switch` 和
   `GlyphIcon.paintOn()` 的分派。

15. **`QtWebEngineQuick::initialize()` 必须在 `QGuiApplication` 构造之前调用**：
   它要设置 `Qt::AA_ShareOpenGLContexts` 等属性，晚了 Chromium 起不来，日志是
   `Qt WebEngine seems to be initialized from a plugin, which is not supported`，
   界面表现是网页区域整块空白。`main.cpp` 里放在「高分屏取整策略」之后、
   `QGuiApplication app(...)` 之前；`--self-test` 用 offscreen 平台会跳过它。

16. **可选依赖要用 `OPTIONAL_COMPONENTS`，别用第二次 `find_package`**：写成
   `find_package(Qt6 REQUIRED COMPONENTS … OPTIONAL_COMPONENTS WebEngineQuick)`，
   命中与否看 `Qt6WebEngineQuick_FOUND`。**没命中时千万不要把 `WebGamePage.qml`
   放进 qrc** —— 那个文件 `import QtWebEngine`，模块不存在时它一被加载就报
   `module "QtWebEngine" is not installed`，整页白。CMake 里用
   `${KIDSSAFE_QML_WEB}` 变量控制是否加入资源列表，游戏乐园的入口也靠
   `Playland.available` 降级成灰色提示。

17. **网页资源装到文件系统，不要塞 qrc**：HTML 里全是 `pages/assets/mobile.css`、
   `assets/images/*.png` 这种**相对路径**，走 `file://` 时 Chromium 解析最稳；
   塞进 qrc 后相对路径、localStorage 源、缓存行为都容易出意外。现在
   `install(DIRECTORY assets/playland …)` 装到 `<prefix>/share/kidssafe/playland`，
   `Playland` 依次探测「环境变量 → 二进制旁边的 share → 常见前缀 → 源码树」，
   改一版游戏不用重编译。

18. **一个不存在的属性名会让整页「静默白屏」，而且能叠出一摞白屏**：写了
   `settings.fullscreenSupportEnabled`（正确写法是 `fullScreenSupportEnabled`，
   Screen 的 S 要大写）。QML 不会在编译期报错，只在控制台打
   `Component is not ready` + `Cannot assign to non-existent property`，
   `StackView.push()` 照样往栈里塞一个空页面。孩子看到白屏继续点，栈就一层层
   堆到 depth 11，点返回只弹一层（日志 `pop 8 -> 7`），看起来像「返回也坏了」。
   现在 `Main.qml` 的 `pushPage()` 会先用 `Qt.createComponent(url,
   Component.PreferSynchronous)` 体检：编译失败就不进栈，并在屏幕顶端弹一句
   「这个页面打不开」。**排查这类问题只看日志最后一行不够，要往上找第一条
   `Cannot assign to non-existent property`。**

   另外：`setHighDpiScaleFactorRoundingPolicy must be called before creating the
   QGuiApplication instance` 这两行是 deepin 的 DTK 主题插件在 `QGuiApplication`
   构造之后自己又设了一次策略导致的，我们的调用在 app 之前（`main.cpp` 第 282 行
   附近），设置是生效的，属于噪音，可以忽略。

19. **`gbm_wrapper.cc … Failed to export buffer to dma_buf` 是 Chromium 的 GPU 送图
   路径失败，不是崩溃**：一打开网页游戏，终端就开始成片刷
   `Failed to get fd for plane.: 没有那个文件或目录`。意思是 GPU 进程想用
   GBM/DMA-BUF 零拷贝把画面交给合成器，但当前环境导不出 dma-buf 缓冲。常见于
   ① 虚拟机（VirtualBox / VMware / QEMU-KVM）的虚拟显卡；② 远程桌面 / VNC；
   ③ 闭源 NVIDIA 驱动没装好、内核 DRM 权限不对；④ Wayland 下没有 DRM render node。
   Chromium 会自动退回软件合成，所以多数情况画面正常、只是日志脏，但偶发会
   网页区全黑。

   这 15 款都是 Canvas 2D，硬件合成收益接近零，所以 `main.cpp` 在
   `QtWebEngineQuick::initialize()` 之前设了两层：

   - `QTWEBENGINE_FORCE_USE_GBM=0` —— QtWebEngine 自己的开关，关掉「网页帧用
     dma-buf 直接交给 Qt 合成器」这条通路。**只加 `--disable-gpu` 盖不住这些
     报错**：那只是不让 Chromium 用硬件合成，GBM 送图那段仍会被执行到。启动时
     会多两行 `GBM support is force disabled.` 的警告，是 Qt 提示这个变量
     「仅供调试」，属预期。
   - `--disable-gpu --disable-dev-shm-usage`（`QTWEBENGINE_CHROMIUM_FLAGS`）。

   想试硬件加速：`KIDSSAFE_WEB_GPU=1 ./kidssafe --windowed`（GBM 仍保持关闭）。
   启动日志会打一行 `[app] 网页渲染： 软件（默认） | GBM: 0 | Chromium flags: …`，
   先确认这行再排查。不重编译也能临时验证：
   `QTWEBENGINE_FORCE_USE_GBM=0 QTWEBENGINE_CHROMIUM_FLAGS="--disable-gpu" ./build/kidssafe --windowed`。
   如果某些驱动连软件路径都刷，可以再加 `--log-level=3` 把 Chromium 的 ERROR
   全关掉（不影响 Qt 自己的 `[nav]` / `[webgame]` 日志）。

20. **`Q_PROPERTY(... NOTIFY xxx)` 要求 `xxx` 真的存在，而且得在 `signals:` 段里**：
   只写 `NOTIFY stateChanged`、却忘了写 `signals: void stateChanged();` 时，
   编译器报的不是「信号未声明」，而是 moc 生成的静态断言——
   ```
   error: static assertion failed: NOTIFY signal stateChanged does not exist in class
   error: ‘stateChanged’ was not declared in this scope   （在 .cpp 的 emit 处）
   ```
   两条一起出现时，九成是头文件里漏了 `signals:` 段（或把信号写进了
   `private:`，那样 moc 依然找不到）。顺带一提：给类加 `Q_PROPERTY` 之后
   必须重新跑 cmake 生成 moc，只 `make` 有时会用旧 moc 缓存。

21. **`天气请求失败：操作被取消` 不是「被取消」，是 8 秒超时**：
   `QNetworkRequest::setTransferTimeout()` 到点后是 `abort()`，错误码成了
   `OperationCanceledError`、`errorString()` 是 `Operation canceled`，
   中文即「操作被取消」。照抄进日志会让人以为是自己误触了什么。
   `Weather::describeNetworkError()` 把它、`TimeoutError`、`HostNotFoundError`
   （没联网）、`SslHandshakeFailedError`（系统时间/证书）分别翻成人话。
   另外：**失败要退避重试**。原来失败一次就等 30 分钟定时器，而开机那次请求
   几乎必然失败（Wi-Fi 还没连上），表现出来就是「天气空了半小时」。
   顺带记一下 `QNetworkInformation::Reachability` 的枚举，别照英文直译去猜：
   `Unknown / Disconnected / Local / Site / Online` —— **没有 `Offline`**，
   「断网」是 `Disconnected`。而 `Unknown` 不能当断网拦，后端没加载时就是它，
   拦下来会变成「明明是好的网络却永远不发请求」。

22. **第三方 C++ 库在 Linux 上最先炸的通常不是逻辑，是「大小写」和「MSVC 专有函数」**：
   SimpleNes 上游把文件叫 `savebundle.h`，但代码里 `#include "SaveBundle.h"`。
   Windows 上无所谓，Linux 直接「找不到头文件」。同理 `memcpy_s`（C11 附录 K）
   只有 MSVC 提供，glibc 至今没实现，`NesCartridge.cpp` 用了它 →
   `memcpy_s was not declared in this scope`。搬任何 Windows 项目过来，
   先 grep 这两类：`grep -r "[a-z]_s(" src/` 与 `ls | sort -f` 对比 include。

23. **模拟器核心的日志默认级别是 Debug，会直接把帧率拖死**：
   `Famicom()` 构造函数里 `Logger::init(LogLevel::Debug)`，一帧几百行 `std::cerr`，
   60fps 下光是终端输出就能让画面变成幻灯片。插卡后立刻改成 `LogLevel::Error`。
   不取 `Warn`：核心每次插卡都会找 `games/mirror_overrides.txt`，找不到就打一行
   WARN，而我们的工作目录里永远没有这个文件。

24. **`QImage::scanLine()` 是非 const 的，它会 detach**：
   `composeFrame()` 每帧往 `m_image` 里写像素，而渲染线程同时在 `grabFrame()`
   里拷一份去画。看着像数据竞争，实际上 `scanLine()` 会触发隐式共享的 detach，
   读到的那份不会被改写。前提是**写的时候必须持锁**（`m_frameMutex`），
   而且 `grabFrame()` 也要在锁内拷贝。

25. **`QPointer<前向声明类型>` 不能直接当 `QObject*` 返回**：
   `nesview.h` 里只有 `class NesEmulator;`，却写了
   `QObject *emulator() const { return m_emulator; }`，编译报
   `cannot convert 'const QPointer<NesEmulator>' to 'QObject*'`。
   原因是 `QPointer<T>` 的 `operator T*()` 里用了 `static_cast<T*>`，
   需要 T 是**完整类型**才能确定派生关系，只靠前向声明这条转换路径不成立。
   修法是把 getter 的定义挪到 `.cpp`（那里 `#include "nesemulator.h"` 了），
   头文件只留声明 —— 既修了错，也没让头文件背上模拟器那一大坨依赖。

26. **「回到桌面」写成 `pop()` 就只在家长界面里打转**：
   进家长后台的路径是「桌面 → 家长验证 → 家长后台」，后台的「回到桌面」如果只
   弹一层，落点是**家长验证页**。家长以为没生效，再输一次密码就又是后台，于是
   看上去被锁死在家界面（录像里就是这样循环的）。同理，把锁定页当栈底
   （`initialItem: lockedOut ? LockoutPage : HomePage`）时，「家长解锁 15 分钟」
   之后也没地方可去：栈底弹不掉，屏幕还是「今天的时间用完啦」。
   现在 `Main.qml` 里加了 `popToRoot()`（逐层 pop，不用有 300ms 去抖的
   `popPage`），锁定页改成压在桌面之上的一层，两处都弹到底。

27. **X11 的「全屏」是窗口管理器协议，kiosk 会话里没人执行它**：
   注销后换到「儿童安全桌面（X11）」，界面只剩 1280×800 一小块贴在左上角，
   其余全是黑的（VM 里尤其明显）。根因是 `showFullScreen()` 在 X11 上只是给
   WM 发一条 `_NET_WM_STATE_FULLSCREEN` 请求，而本会话**故意不启动
   kwin / plasmashell**——没有 WM，就没有人执行，窗口停在 QML 里写的默认尺寸，
   背后是黑色的根窗口（没有壁纸，所以整片黑）。
   修法两条一起上：`Main.qml` 把宽高绑到 `Screen.width/height`（自己铺满，
   不依赖 WM，分辨率变了会自动重排），`Kiosk::attach()` 再按
   `QScreen::geometry()` 兜一次底并监听 `geometryChanged`（VM 里拉窗口、
   外接显示器都会触发）。最小尺寸 800×560 只在调试窗口模式下生效，
   否则小屏上反而会把窗口撑出屏幕。

28. **deepin 25 里没有 `dde-dock` 这个命令了，任务栏是 `dde-shell` 的插件**：
   照老教程敲 `sudo nohup dde-dock` 会得到
   `nohup: 无法运行命令 'dde-dock': 没有那个文件或目录`。deepin 25 起
   `dde-dock` 已被 `dde-shell` 取代（官方《deepin 25 preview DDE 移植指南》
   原话），任务栏只是 dde-shell 的 `org.deepin.ds.dock` 插件，由 systemd
   用户单元 `dde-shell@DDE.service` 拉起。普通会话里重启任务栏：
   `systemctl --user restart dde-shell@DDE.service`，起不来就看
   `systemctl --user status dde-shell@DDE.service` 的日志；
   `killall dde-shell` 也能用（靠单元里的 `Restart=` 重新拉起），
   但崩因只在 journal 里，排查时别只知道 kill。
   两个附带结论：**① GUI 程序别用 `sudo` 起**——`sudo` 会丢掉 `DISPLAY`、
   `XAUTHORITY`、`DBUS_SESSION_BUS_ADDRESS`，即使二进制找得到也会变成
   `cannot connect to X server`，还会在 root 名下生成一堆属主错误的配置；
   **② 儿童会话里不该、也拉不起任务栏**——那里没有 dde-session、
   没有 dde-application-manager、也没有 WM，dde-shell 的插件没有宿主；
   要出去走家长后台的「退出儿童桌面」（点两次确认）。

29. **`程序启动失败：Child process set up failed: execve: 此文件或目录不存在`
   不是「缺 gcc」**：这条只在**运行阶段**出现 —— 说明 gcc 已经返回 0、源码编译
   通过了，卡在 `execve()` 那一步。缺 gcc 走的是另一条分支
   （`QStandardPaths::findExecutable` 找不到就直接提示 `sudo apt install gcc`），
   根本到不了运行阶段。`execve` 对一个**存在**的文件返回 `ENOENT`，现实中只有三种：
   ① 产物真的不在了（临时目录被清理）；
   ② **ELF 解释器缺失**：`PT_INTERP` 指向的 `ld-linux-*.so` 本机没有（跨架构、
      精简系统），`ldd ./prog` 会立刻露馅；
   ③ 路径在子进程的命名空间里不通（容器、私有 `/tmp`）。
   注意目录挂 `noexec` 报的是 `EACCES`（权限不够）而不是 `ENOENT`，两种别混。
   排查顺序：`ls -l <prog>` → `ldd <prog>` → `mount | grep -E ' /tmp | /home '`。
   顺带改了两处：编译目录从 `QDir::tempPath()` 挪到 `~/.cache/kidssafe/codelab/`
   （避开 `/tmp` 的 `noexec` 与定时清理），真起不来时 `onRunStartFailed()`
   会把「文件在不在 / 有没有 x 位 / 多少字节」逐条查完再给结论，
   不再只甩一个 errno 给孩子看。

30. **网页存东西要 `file://`，`qrc:` 拿不到 localStorage**：小游戏工坊把作品存在
   页面的 localStorage 里，而 `qrc:` 是自定义 scheme，Chromium 不给它 opaquestorage ——
   症状是「当场存住了，重开就没了」，最难查的一类：没有任何报错。
   所以编辑器走 `install(DIRECTORY assets/maker)` + `file://`，且**目录名不能改**
   （换路径 = 换 origin = 所有作品消失）。
31. **`QtQuick.Dialogs` 在精简发行版上是单独一个包**：`FileDialog` 属于
   `qml6-module-qtquick-dialogs`，不属于 `qt6-declarative` 的必装部分。
   直接把 `import QtQuick.Dialogs` 写进主页面，缺包时**整页 QML 起不来**；
   拆成 `MakerFilePicker.qml` 用 `Loader` 加载，缺包只丢「选文件」这一件事。
   同理，`WebEngineView.contextMenuRequested` 是 6.6 之后才有的信号，
   往 `WebEngineView` 里写不存在的信号处理器会让整个组件加载失败 —— 不要为了
   关右键菜单冒这个险（kiosk 窗口本来就没有菜单键）。
32. **Xlib 的默认错误处理是 `exit()`**：`XGrabKey` 撞上「这组键已经被人抓了」
   （普通桌面里的 Alt+Tab 就是这样）会异步产生 `BadAccess`。不接管
   `XSetErrorHandler` 的话，Xlib 打印一行就 `exit(1)`，症状是「点一下屏蔽快捷键，
   应用直接消失」。`hotkeyguard.cpp` 里临时接管、抢完还原，并且对每个组合键
   单独判 `g_lastXError` —— 抢不到就跳过，不要当成整体失败。
33. **`XGrabKey` 必须连 NumLock / CapsLock 变体一起抓**：开着大写锁时修饰符里多一个
   `LockMask`，只抓「干净」那一组会漏。孩子按一下 CapsLock 就能 Alt+Tab 了 ——
   这种「按一下某个键就绕过」的漏洞在真机上一定会被发现。
34. **systemd 的 `StartLimitIntervalSec` 写在 `[Unit]` 段**：写进 `[Service]`
   systemd 会忽略并警告，默认策略「10 秒内失败 5 次就彻底放弃」继续生效，
   对儿童桌面等于「崩几次守护就没了」。另外单元里只 `enable` 不要 `--now`，
   否则当前活着的实例旁边会再起一份，两个全屏窗口互相抢焦点。
35. **自启和守护不能同时开**：`~/.config/autostart/kidssafe.desktop` 与
   systemd 用户单元都负责「启动应用」，两个都开就是登录时起两份。
   `core/startup.cpp` 里两者互斥，开一个会删另一个 —— 改这块时别把这个约束去掉。

36. **嵌套 `Repeater` 会盖掉外层的 `modelData`**：问卷是「题目 Repeater → 选项
   Repeater」，内层直接写 `modelData` 拿到的是选项文字，外层题目对象已经看不见了。
   表现是「点了选项没反应」或「所有题共用同一个答案」。正确做法是在外层 delegate
   上存一份 `property var q: modelData`，内层用 `q.id` / `q.options`。
   分项打分（`page.dims` × `model: 5`）同理：内层 `modelData` 是数字下标。


### 调试运行：用窗口跑，别用全屏

```sh
./build/kidssafe --windowed
```

`--windowed`（或环境变量 `KIDSSAFE_WINDOWED=1`）会跳过全部锁定手段：不加无边框、
不置顶、不全屏，`×` 也能直接关。窗口旁边摆个终端，日志实时可见——排障时不要
用全屏，全屏会话里切不到终端，出了问题只能看最后一屏画面。

不加参数的 `./build/kidssafe` 依然是全屏无边框（体验层锁定），适合验证真实观感；
它的日志同时落在 `~/.local/share/kidssafe/kidssafe.log`，事后可以翻：

```sh
tail -f ~/.local/share/kidssafe/kidssafe.log
```

> 从终端直接跑（含 `KIDSSAFE_KIOSK_SESSION=1 ./build/kidssafe`）都**没有会话裁剪**，
> 不是真正的锁定状态。验证锁定效果必须注销后在登录界面选「儿童安全桌面」会话。

### 跑起来是黑屏时怎么查

`kidssafe --self-test` 是最快的判断入口，它会在没有显示器的情况下把 QML 真的
实例化一遍，并给出一行结论。几个关键字段：

| 输出 | 含义 |
|---|---|
| `QML 样式` | 显示用了 Breeze 还是内置样式；`module "org.kde.breeze" is not installed` 只可能出现在样式探测失效时 |
| `QML 界面加载 失败` | QML 没实例化，上面一定还有一行具体的 `qrc:/qml/*.qml:...` 报错 |
| `可加入白名单的应用 0` | 应用目录里没有可用的 `.desktop`，先确认 `/usr/share/applications` 有内容 |
| `语音合成 (spd-say) 不可用` | 只影响朗读。`detail` 会写清原因（未安装 / 没有音频输出模块 / 4 秒超时），装完在家长后台点「重新检测」即可，不用重启 |
| `kiosk 会话模式 否` | 说明是从终端直接跑的，没带 `KIDSSAFE_KIOSK_SESSION=1` |

如果屏幕上是一块纯黑的「KDE Wayland Compositor (wayland-0)」窗口，那是
`kidssafe-wayland-session` 在桌面会话里**嵌套**拉起 `kwin_wayland` 的结果：
合成器起来了，但里面的应用因为上面的原因没加载出来，所以整块是黑的。直接关掉
这个窗口即可；真正验证要注销后从登录界面进「儿童安全桌面」。

装好之后跑一遍环境检查（只读，不改系统）：

```sh
./scripts/check-host.sh
```

它会逐项检查会话文件、QML 模块、合成器、语音包、应用清单，并调用
`kidssafe --self-test`（无界面自检，可在 SSH / CI 里跑）确认配置可写、
应用扫描正常、QML 能实例化。最后会列出必须人工确认的 5 项锁定验证。

### 故事服务端（故事屋的内容来源）

故事屋默认是离线空状态：它连的是**家长自建**的服务器，不连任何公网服务。
Linux 版自带一个最小服务端，只用 Python 3 标准库，不装依赖、不建库、不需要 root：

```sh
cd server
./install-server.sh          # 装成 systemd 用户服务，随登录启动
```

启动后终端会打印两行地址：家长后台（本机打开）和「孩子端可填」（局域网地址）。
把后者填进家长后台的「服务器地址」，打开「在线故事与内容」开关，回桌面进故事屋即可。

同一个地址还负责**收学习情况**：孩子过一关、交一次作业，客户端就会 POST
`/api/learn/report` 上报进度与成绩，家长在服务器网页的「学习情况」里能看到
章节进度条和每套作业的最高分。没填地址时一个请求都不发，成绩只留在本机。

自带 19 篇示例故事、7 个分类。加内容有两种方式：改 `data/stories.json`，
或往 `data/stories/<分类名>/` 里丢 txt，然后在家长后台点「重新载入内容」。
完整说明（接口表、排障、卸载）见 [`server/README.md`](server/README.md)。

---

### 首页天气（数据从哪来、断网怎么办）

首页那条淡蓝色天气条走的是 **Open-Meteo**（公开接口，免注册免密钥），
和家长自建的 KidsSafeServer 没有任何关系，两条链路互不影响：

| 步骤 | 接口 | 说明 |
|---|---|---|
| 定位 | `ipwho.is` | 家长没填城市时按出口 IP 取经纬度 |
| 定位 | `geocoding-api.open-meteo.com` | 填了城市名时按名字查经纬度 |
| 天气 | `api.open-meteo.com/v1/forecast` | `current` + `hourly`，一次请求拿全 |

- **城市**：家长后台 → 首页天气 → 留空或填 `auto` 表示按 IP 定位；也可以直接填
  城市名（如 `达州`）。改完点「刷新」即可，不用重启。
- **断网**：拿不到新数据就沿用上一次的结果，卡片角落写「离线 · 20:10 的数据」。
  缓存文件在 `~/.local/share/kidssafe/weather.json`（**带经纬度**），第一次联网前
  这里是空的，此时整条天气隐藏（不留空白，也不会弹错误框）。
- **超时与重试**：每个请求 8 秒。日志里的 `操作被取消` / `Operation canceled`
  不是被谁取消，而是 `QNetworkRequest::setTransferTimeout` 到点 abort 的产物，
  现在一律翻译成「超时（8 秒没有响应）」。失败后按 **30s → 60s → 2min → 5min
  → 15min** 退避重试（开机时 Wi-Fi 常常还没连上，第一次失败几乎是必然的），
  网络从断到通则立刻补一次。相同的失败原因只打一行 WARN，之后折叠成计数。
- **省一次请求**：定位/地理编码成功后坐标在本次运行内复用，30 分钟的定时刷新
  直接取预报，不再问一遍定位服务；定位服务挂了就用缓存里的坐标接着取。
- **改城市立即生效**：新请求会作废在途的旧请求（abort + 代次校验），
  旧回复晚到也不会覆盖新结果。
- **隐私**：定位会向 `ipwho.is` 发一次请求（对方只能看到出口 IP）。不想有任何
  外部请求的家庭，在家长后台把「首页天气」关掉即可，关掉后不发请求、不显示卡片。
- **图标**：天气图标同样是手绘（`GlyphIcon` 的 `sun/partly/cloud/rain/snow/thunder/fog`），
  不用 emoji —— 原因见下面第 8 条坑。

---

### 红白机（NES / Famicom）

核心用的是 **SimpleNes**（MIT，纯 C++17、零 Qt 依赖），原样放在
`src/nes/core/`，编成静态库 `kidssafe_nes_core`；Qt 侧只有三个文件：

| 文件 | 职责 |
|---|---|
| `src/nes/nesemulator.*` | 帧循环、调色板、声音、存档、ROM 扫描 |
| `src/nes/nesview.*` | `QQuickPaintedItem`，把 256×240 的帧画出来 |
| `src/qml/NesPage.qml` | 选游戏列表 + 游戏画面 + 暂停/重来/存档/读档 |

**ROM 不随包分发**（版权原因），家长自己放：

```sh
mkdir -p ~/.local/share/kidssafe/roms
cp *.nes ~/.local/share/kidssafe/roms/          # 或用 KIDSSAFE_ROM_DIR 指定别的目录
```

页面里显示的路径是运行时算出来的，不用改代码；放完点「刷新」即可。

- **按键**：方向键移动，`Z` / `J` / 空格 = A，`X` / `K` = B，`Enter` = Start，
  `Shift` / `\` = Select，`Esc` 退出。没有手柄支持（SDL 没引进来）。
- **存档**：`~/.local/share/kidssafe/saves/<ROM名>.nes.sav`，是核心的整机快照
  （含 CPU/PPU/APU/内存），换 ROM 后读旧档会提示「存档不兼容」。
- **画面**：帧循环用 `QTimer(4ms, PreciseTimer)` + `QElapsedTimer` 定帧，
  按 60.0988fps 推进（不是整数 60），否则声音与画面会越跑越偏。
  落后超过 5 帧时直接对齐到「现在」而不追帧，避免卡顿后画面快进。
- **声音**：44100Hz 单声道 16 位直通 `QAudioSink`，缓冲约 200ms。
  没装 QtMultimedia 就静音，`audioReady` 为 false。
- **支持的 mapper**：0/1/2/3/4/7/23/66/74/245。少见 mapper 的 ROM 会打开失败，
  页面提示「这不是有效的 NES 游戏」——那是核心返回的，不是文件损坏。
- **为什么自己画帧不用 Image**：`Image` 换图要换 URL，60 次/秒的 URL 查询既浪费，
  某些平台上还会短暂保留旧纹理，表现为画面「抽一下」。

> 移植时改过的两处上游代码：`savebundle.h/.cpp` 改名成 `SaveBundle.h/.cpp`
> （Linux 区分大小写，上游的 include 写的是 `SaveBundle.h`）；
> `common.h` 补了 `memcpy_s` 的兼容宏（glibc 没实现 C11 附录 K）。

### 故事剧场（吉里吉里 / KiriKiri 2）

`D:/Downloads/krkr2` 那份是 krkr2 的**只读 SVN 存档**（trunk/branches/tags 全在里面，
1.4 GB），不是能直接编进本工程的库。判断依据不是感觉，是数出来的：

```
199 个文件 include <windows.h>      HWND 出现 510 次、HDC 106 次
渲染走 GDI（StretchBlt）、声音走 DirectSound、窗口走 HWND 消息循环
官方 readme 自己写着「残念ながら それほど移植性が高い物とは言えません」
```

再加上许可：krkr2 是 **GPLv2**（双重许可里的 GPL 分支）。链进本程序，整个程序就必须
按 GPL 发布。所以这里和红白机不一样，**不做内核移植，只做启动器**——
进程边界就是许可边界，也是平台边界。

| 文件 | 职责 |
|---|---|
| `src/core/vnengine.*` | 探测运行时、扫游戏目录、起/停子进程 |
| `src/qml/VnPage.qml` | 游戏列表 + 「正在播放」层 + 让窗口的逻辑 |

**后端按这个顺序自动挑**（`Vn.backend` 会显示实际用了哪个）：

1. `KIDSSAFE_VN_ENGINE` 环境变量 —— 显式指定，支持 `{game}` 占位：
   ```sh
   KIDSSAFE_VN_ENGINE='krkrsdl2 {game}' kidssafe
   ```
2. `krkrsdl2` / `krkrsdl` / `kirikiri2` —— SDL2 移植版，Linux 原生，首选。
   会先在 `PATH` 里找，再找 `~/.local/share/kidssafe/engine/`；
3. `wine` —— 跑游戏自带的 `.exe`。兼容性最好、启动最慢，排在最后。

**游戏怎么放**：一个游戏一个文件夹，整个丢进 `~/.local/share/kidssafe/games`
（`KIDSSAFE_GAMES_DIR` 可覆盖）。目录里有 `*.xp3` 或 `*.exe` 就被认出来：

```sh
mkdir -p ~/.local/share/kidssafe/games
cp -r ~/下载/某游戏 ~/.local/share/kidssafe/games/
```

**窗口怎么让位**（kiosk 会话里没有窗口管理器，这是关键）：

- `--windowed`：`hide()` 自己，游戏退出后 `showNormal()`，方便旁边看终端；
- kiosk：只 `lower()` 自己，让游戏的窗口压在上面。**故意不用 `hide()`**——
  万一引擎根本没画出窗口（缺插件、显卡驱动），界面还在下面，孩子不会面对
  一片黑屏无路可走；那层「故事正在播放」上有「结束故事」，能退出来。

已知限制：

- **全屏由游戏自己决定**。没有 WM，`_NET_WM_STATE_FULLSCREEN` 没人执行，
  引擎可能开一个 800×600 的窗口贴在中间。SDL 移植版一般会自己铺满，
  Wine 下的原版可能不会——这是「不要窗口管理器」的代价，不是 bug。
- **存档在游戏自己的目录里**（吉里吉里把存档写进游戏目录的 `savedata/`），
  不像红白机那样由我们统一管。备份游戏目录 = 备份存档。
- **只认「目录」**。单独一个 `data.xp3` 丢在 games 下不会被识别，必须放在文件夹里。
- **游戏内容与版权都由家长负责**，包和仓库里不含任何一个游戏。

### 媒体中心（听一听）

对应 Windows 端的 `MediaHubPage`。内容**全部随包**，不打开任何外部网页：

| 分类 | 内容 | 播放方式 |
| --- | --- | --- |
| 儿歌音乐盒 | 8 首公有领域童谣（小星星、两只老虎、生日快乐…） | 随包 wav，播完自动接下一首 |
| 安睡摇篮 | 雨声 / 海浪 / 白噪音 | 随包 wav，`loops=Infinite` 一直循环 |
| 国学启蒙 | 唐诗 5 首 + 三字经节选 | 纯文本，由 `Speech`（spd-say）朗读 |

- 音频在 `assets/audio/*.wav`（约 10 MB，来自 `Docs/tools/generate_kids_audio.py`
  合成的音乐盒音色），内容表是 `assets/audio/library.json`。**改内容只改这个
  JSON**：加一首歌就多一条 `tracks`，`sections` 里的 `count` 是算出来的，不用维护。
- 播放逻辑在 C++ 的 `src/core/mediaplayer.*`，不在 QML 里用 `MediaPlayer`。
  原因和红白机的音频一样：QML 的媒体类型要额外装 `qml6-module-qtmultimedia`，
  只装 `-dev` 时能编过、运行期 `import QtMultimedia` 直接失败，整页打不开；
  C++ 侧 QtMultimedia 是**可选依赖**，没装时页面照样打开、音频卡片点下去
  只提示「这个版本放不了音频」，国学朗读仍然可用（它走 spd-say，不碰音频解码）。
- 三个状态：分类浏览 → 底部播放条（进度可点、上一首/暂停/下一首/停止）→
  朗读层（正文大字 + 朗读/停止）。**离开页面自动 `Media.stop()` +
  `Speech.stop()`**：这不是背景音乐，没理由在孩子回桌面后继续响。
- 自动接下一首只在**同一分类内**：儿歌播放到最后一首就停，不会串到雨声里。

已知取舍：

- **没有音量滑块**：固定 0.8。音乐盒音色的峰值偏高，满音量在小喇叭上会破；
  孩子更需要的是「一键停止」，而不是调音量。
- **朗读不显示进度**：spd-say 是外部进程，拿不到「读到第几句」。
  页面给的是一个大字正文 + 停止按钮，而不是假装有进度条。
- **`library.json` 里 `file` 指向的文件缺失**：卡片仍然显示（能看到名字），
  点下去会明确说「找不到音频文件：xxx」，不会静默失败。

### 小小程序员（积木 → C 语言）

孩子不去写 `printf`，而是往一条链上叠彩色的积木；每叠一块，右边的代码就多一行
**真正的 C 语言**。点「运行」，程序由系统的 `gcc` 编译成一个临时可执行文件跑起来，
输出直接显示在下面。

这么设计不是为了炫技，而是因为「编译」这件事本身就是最好的反馈：孩子看到自己
搭的东西变成了 `for (int i = 0; i < 3; i++) {`，比任何讲解都直观；而代码框里的
东西是可以选中、复制、拿到终端里自己 `gcc` 一遍的。

| 积木 | 生成的 C | 教什么 |
| --- | --- | --- |
| 打印一句话 | `printf("%s\n", "你好");` | 顺序 |
| 变量设值 | `v1 = 3;` | 变量是个盒子 |
| 变量加 | `v1 = v1 + 4;` | 状态会变 |
| 打印变量 | `printf("变量1 = %d\n", v1);` | 读出状态 |
| 重复 N 次 | `for (int i = 0; i < 3; i++) { … }` | 循环 |
| 如果…那么 | `if (v1 > 5) { … }` | 判断 |

**积木不拖拽**，点一下调色板就插在选中块后面；「重复」「如果」会自动配一个
「结束」，孩子永远见不到落单的花括号。要取消循环就删循环块本身，口袋里的内容
会留在原地。想跳出循环继续往外搭，点一下那根「结束重复」横条即可。

#### 关卡：6 章 32 关（每章都以一个关底作品收尾）

关卡表在 `assets/codelab/lessons.json`，**改它就能加题、改讲解，不用重编译**：

```jsonc
{
  "id": "q-loop-1",
  "type": "task",                       // teach = 看演示；task = 自己搭并判题；
                                        // project = 每章关底的小项目（判题同 task）
  "chapter": "第二章 · 重复",
  "title": "开满花",
  "goal": "用「重复」积木打印 4 行「花花花」",
  "require": ["loop", "print"],         // 必须用到的积木
  "expect": ["花花花", "花花花", "花花花", "花花花"],   // 按顺序出现在输出里
  "hint": "先点「重复 N 次」，次数改成 4，再点「打印」",
  "starter": []                          // 进关时预置的积木
}
```

教学关还可以带一段 `interactive`，孩子在点「运行」之前先猜结果：

```jsonc
"interactive": {
  "question": "口袋里装了一行「我爱编程」，重复 3 次，屏幕上会出现几行？",
  "options": ["1 行", "3 行", "4 行"],
  "answer": 1,                           // options 里的下标
  "why": "重复几次就把口袋里的积木做几遍，所以是 3 行。"
}
```

先猜再跑，和直接点「运行」是两件事：前者要想，后者只是看。猜错的那一下，
比看十遍输出记得都牢。

关底作品（`type: "project"`）除了判题字段，还多一个 `project`：过关后弹一张
作品卡，把刚做出来的东西起个名字、给个徽章。没有这一步，「过关」只是右边一条
绿色提示条，孩子并不知道自己做出了什么。

```jsonc
"project": { "name": "存钱罐", "badge": "🐷", "pitch": "每存一次就报一次余额" }
```

现有章节：顺序 → 重复 → 变量 → 判断 → 队列（先进先出）→ 栈（后进先出）。
每章走法固定：**教学（看 + 猜）→ 练习（自己搭）→ 关底作品（做个小东西）**。
练习共 20 个：重复、变量两章各 4 个，其余四章各 3 个。一节教学只配一个练习太急，
孩子还没认全积木就进了作品关，最后往往是照着答案摆一遍。
数据结构那两章刻意不引入数组：孩子用「打印的顺序」就能看出队列和栈的区别，
`盘子3 → 盘子2 → 盘子1` 比任何图示都清楚。

**判题看输出，不看积木。** 同一个结果允许多种搭法 —— 编程本来就是这样。
`require` 只用来保证「该用循环的地方真用了循环」，否则直接印出答案也能过。
匹配用的是「按顺序逐行包含」：缺行、顺序反了都不算过，孩子多加一行点缀不影响。

进度存在 `~/.config/kidssafe/codelab.conf` 的 `passed=`，卸载应用不会删。

#### 安全：这不是「让孩子执行任意代码」

关键在于**代码不是孩子写的，是这个模块按模板拼出来的**：

- 文字参数经过 C 字符串转义（引号 / 反斜杠 / 换行），拼不出语句；数字夹紧到 ±999，
  循环次数夹紧到 1–20。没有「手写代码」的入口，也就没有注入面。
- 编译产物在 `~/.cache/kidssafe/codelab/run-XXXXXX`（`QTemporaryDir`）里，退出即删。
  不放 `/tmp`：那里可能被挂成 `noexec`，或被 systemd-tmpfiles 定时清掉，
  表现就是「编译通过、程序起不来」。
- 运行前在 `setChildProcessModifier` 里设 `RLIMIT_CPU=2s`、`RLIMIT_AS=256MB`、
  `RLIMIT_FSIZE=1MB`、`RLIMIT_CORE=0` —— 就算哪天模板出了问题，内核也会先把它按住。
- 外面还有 3 秒超时和 8 KB 输出上限；`while` 式的死循环会看到「程序跑得太久，
  被安全保护停下来了」，界面不会卡。

#### 编译器是可选依赖

没有 `gcc` 时页面照样打开、代码照样能看，只是点「运行」会提示
`sudo apt install gcc`。`debian/control` 把它放在 `Recommends` 而不是 `Depends`：
不是每个家庭都要用这一块，没必要因此拒装整个桌面。

启动日志里有一行可以直接确认：

```
[app] 小小程序员： gcc | 关卡 32 个
```

### 编程作业（选择 / 填空 / 排顺序）

关卡是「动手搭，编译器判」；作业是「纸面题，当场判」。入口在小小程序员关卡条
右端的「编程作业」按钮，题库在 `src/qml/HomeworkData.js`，改题目不用重编译。

| 题型 | 怎么答 | 怎么判 |
| --- | --- | --- |
| 选择题 | 点一个选项 | 立刻判对错，并亮出「为什么」 |
| 填空题 | 输入框 + 「确定」 | 抹掉空格、标点、大小写后比对；`alt` 里的写法也算对 |
| 排序题 | 点候选卡片按顺序排（可撤销） | 和 `items` 逐项比对，没排完不让交 |

三个取舍：

- **选择题点一下就判**，不等交卷。等五道题交完再告诉孩子错在哪，他早忘了当时怎么想的。
- **判题只认「抹平之后」的答案**（`normalize()`）：孩子在平板上打个中文逗号就判错，
  只会让人不想再做题。全角数字 `５` 也算对。
- **排序题点选而不是拖拽**：1280×800 上拖拽命中率低；点错了用「撤销」退一格。
  候选顺序是打乱过的，而且**刻意转不满一圈** —— 转满一圈就等于把答案原样摆出来了。

成绩按套记录，存 `~/.config/kidssafe/codelab.conf` 的 `homework` 组，同一套反复做
保留最高分（`best`），交卷时由 `CodeLab.saveHomeworkScore()` 写入。三套作业分别对应
顺序与重复、变量与判断、队列与栈，孩子在哪一章卡住就抽哪一套复查。

成绩在**家长后台的「小小程序员 · 学习情况」卡片**里能看到：每一章的过关数
（`3/4` 加一条进度条）、每套作业的最高分和最近一次交卷时间。分母只数要判题的
关卡（练习 + 作品关），教学关没有「过关」这一步，算进去只会让进度永远差几格。
卡片右下角可以清空作业成绩（重置复习记录），关卡进度不受影响 —— 两者是两条
独立的线：进度是「学到了哪」，成绩是「掌握得怎么样」。

如果填了「在线故事与内容」里的服务器地址、并且那个开关是打开的，同一份数据
还会上报给自建服务端（`POST /api/learn/report`，攒 1.5 秒再发，连过三关只发最后
一次），家长在外面用浏览器打开服务器网页也能看。上报走同一个开关是有意的：
把孩子的数据送出这台机器，得跟在线内容一样需要家长明确打开，不能偷偷发。
上报失败不影响孩子做题：成绩本来就已写进本机，客户端记一行「上报失败」并在
下次改动时重试。卡片底部那行小字就是它的状态，旁边还有「立即上报」，
家长刚填完地址不用等孩子再做一题。

### 项目工坊（拆现成的作品）

「小小程序员」是从零搭，这一块反过来：**三个写完的作品摆在那儿给孩子拆**。
两个入口：游戏乐园的「项目工坊」磁贴，以及小小程序员关卡条右端的「项目工坊 →」——
孩子搭到一半想看看「真正写出来的游戏长什么样」，不用退回大厅。
每个项目都配四样东西 —— 能玩、能看关键代码、能调参数、能重来。

| 项目 | 玩法 | 在教什么 |
| --- | --- | --- |
| 飞机大战（入门） | 左右飞、自动开火、躲敌机 | 每帧改一点坐标 = 动起来；两个矩形重叠 = 打中；`Timer` = 每秒 60 次的心跳 |
| 推箱子（入门） | 方向键推箱子到目标点 | 地图是一张**二维表**，`grid[y][x]` 定位格子；推箱子 =「人往哪走，箱子往更前面挪一格」 |
| 横板冒险（进阶） | 跑、跳、捡金币、躲尖刺 | 重力是每帧给竖直速度加一点；跳是「一瞬间给一个向上的速度」；摄像机跟着人走 |

三个都是**纯 QML + Canvas**，不依赖图片资源、不联网 —— 复制走一个 `.qml` 文件
就能跑，这一点对「给孩子看源码」很重要：打开就是一整份能读的东西，不用先解压素材包。

#### 调参数：让「程序的行为是数字决定的」变成看得见的事

右边三个滑块直接改运行时属性，松手立刻重画：

- 飞机大战：敌机速度 50–220、出机间隔 0.4–2.0 秒、子弹速度 260–900；
- 横板冒险：重力、跳跃力、跑速；
- 推箱子：没有滑块（它的乐趣在地图，代码里注释标了怎么改地图字符）。

把「出机间隔」拉到 0.4，屏幕立刻变成弹幕；拉到 2.0，孩子会自己说出「太慢了」——
这个瞬间比讲十遍「参数影响结果」有用。

#### 加第四个项目的接口

`ProjectLabPage.qml` 只认这六个约定，照着写就能加：

```qml
Item {
    property var    knobs: [ { key: "speed", label: "速度",
                              min: 50, max: 200, step: 10, value: 100 } ]
    property string howTo:      "怎么玩"
    property string teaches:    "在教什么"
    property string codeSample: "关键代码（多行字符串）"
    function setKnob(key, value) { /* 滑块动了 */ }
    function restart()           { /* 「重新开始」按钮 */ }
}
```

然后在 `projects/` 放文件、在 `ProjectLabPage.qml` 的 `projects` 模型里加一条
（`source` 写 `projects/你的.qml`），最后**别忘了在 `CMakeLists.txt` 的
`qt_add_resources` 里也加一行** —— 这几个页面走 qrc，漏加这一行编译期不报错，
运行时白屏 + `Cannot load ... projects/Xxx.qml`，是最容易漏的一步。
（`ProjectLabPage` 会盯 `Loader.status`，出错时中间显示「这一款没打开」而不是白板，
点右下角「重新开始」等同重试一次。）

键盘焦点也有一处要注意：项目根 `Item` 要 `focus: true`，并在
`Component.onCompleted` 里 `forceActiveFocus()`。Loader 换项目时旧焦点会丢，
所以「重新开始」按钮点完也顺手 `forceActiveFocus()` 一次，否则孩子会发现
「重来之后方向键不管用了」。

### 原生小游戏：华容道与国际象棋

游戏乐园里两款「要用脑子」的游戏。它们和数独一起放在大厅靠后的位置 ——
前面几个是练手速和眼力的，这两款得坐下来慢慢想。

#### 华容道（`KlotskiGamePage.qml`）

4×5 的盘，曹操是 2×2 的大块，挪到最下面中间的出口就算赢。棋子只能整格平移，
不能跳、不能斜走，和木制华容道一样。

| 难度 | 棋子数 | 最少步数 | 说明 |
| --- | --- | --- | --- |
| 简单 | 6 | 14 | 只留曹操、关羽和两个竖将，空位多，随便挪也能出去 |
| 中等 | 8 | 27 | 四将齐全，只剩最下面一排空位 |
| 困难 | 9 | 51 | 再加一个兵堵在左下角 |

三个布局都**先用 A\* 搜过一遍**（校验脚本没进仓，结论写在这儿）：第一步先确认
终局真能走到，第二步给出单格移动的最少步数，界面上「最少 N 步」那个数就是
这么来的。经典「横刀立马」要 116 步，对 8 岁的孩子太长，所以困难档砍掉三个兵，
51 步 —— 够琢磨，又不至于卡到放弃。

操作给了两条路：**按住棋子拖**（按格吸附，拖到哪一格就走哪一格，不会卡在半格），
或者**点一下棋子再用右边的方向键**一格一格挪。拖不动的时候（比如被别的棋子挡住），
方向键还能用，不会出现「点了半天没反应」。每一步都可以「退一步」。

#### 国际象棋（`ChessGamePage.qml` + `ChessEngine.js`）

孩子执白（在下面），电脑执黑。点自己的棋子 → 绿点标出能走的格子 → 点绿点落子 →
电脑应一手。

**规则和界面是分开的**，这是这一款唯一值得说的设计：

- `ChessEngine.js` 是纯函数，不 import 任何 Qt 类型，管走子、将军、将死、和棋、
  评估和搜索；
- `ChessGamePage.qml` 只管「点哪里、画什么」。

分开的收益很直接 —— 引擎能用 node 单独跑 **perft**（把某一局面往后数 N 层，
所有合法着法的总数），这是棋类引擎的「单元测试」：

| 局面 | perft(1) | perft(2) | perft(3) |
| --- | --- | --- | --- |
| 开局 | 20 | 400 | 8 902 |
| Kiwipete（易位 / 吃过路兵 / 牵制都在这张图上） | 48 | 2 039 | 97 862 |
| 升变密集局面 | 24 | 496 | — |

数字对得上，说明易位、吃过路兵、升变、牵制这些边界情况基本没写漏。放在 QML 里
是没法这么测的：一跑 `qmlscene` 就只能靠眼睛看。

跑法是 `node tests/chess_engine_test.js`（只用 node，不需要 Qt）。它加载的就是
`src/qml/ChessEngine.js` 本体，不是副本 —— 改完规则跑一遍，数字变了立刻知道。
除了 perft，里面还有傻瓜将死、逼和、一步杀、白吃子、以及「自对弈一整盘不能
走出非法着法」这几组用例。写这个测试的过程里抓到过一个真 bug：叶子节点只做
静态评估，导致「一步杀」被当成普通局面按子力算分，电脑会放着将死不走进而输棋。

三档难度只改电脑看几步，规则和棋盘完全一样：

| 难度 | 搜索深度 | 随机范围 | 感觉 |
| --- | --- | --- | --- |
| 简单 | 1 步 | ±150 分 | 会漏子，孩子能赢 |
| 中等 | 2 步 + 静止搜索 | ±45 分 | 会吃白送的子，但也会犯小错 |
| 困难 | 3 步 + 静止搜索 | ±10 分 | 每步都想占便宜 |

「随机范围」是故意的：同分或差不太多的着法里随机挑一个，不然每局开头都一模一样，
孩子第二次就背下来了。

给孩子做的几处让步：

- 兵到底线**自动升变成皇后**，不弹四选一 —— 这个选择孩子做不明白；
- 显示的是「轮到你走」「将军！先把王走到安全的地方」这种人话，不是 `Qh4#`；
- 被将军时王所在的格子整格泛红；
- 「悔棋」一次退两步（连电脑的应手一起退），不会退到「轮到电脑走」的尴尬局面；
- 输了也只是「电脑赢了这一局，再来一次吧」，不扣分、不记败场。

棋子是用 `ChessPiece.qml` 里的 Canvas 手绘的，没有用 `♔♕♖` 这些字符：字形靠字体，
精简发行版上缺字就是一排方框 —— 同一份代码在有的机器上正常、有的机器上全是豆腐块，
这种问题最难排查。

引擎的接口就四个：`legalMovesFrom(state, sq)` 给界面画落点、`applyMove(state, m)`
走一步、`status(state)` 判断将军/将死/和棋、`bestMove(state, level)` 给电脑选一步。
想改棋力只动 `bestMove` 里的 `depth` 和 `slack`。

### 联机对战：只认房间号

国际象棋和华容道可以和小伙伴联机，但**没有公开大厅，也没有聊天**：一方点
「开一个房间」拿到 4 位号码，念给对方，对方输进去，两个人就进了同一个房间。
号码是随机生成的、猜不到，房间 30 分钟没动静自动回收。

这不是没做完 —— 一个「点一下就随机匹配一个陌生人下棋」的儿童桌面，比没有联机
功能糟糕得多。孩子在这里只可能遇到家长或同学亲口给过号码的人。

两道门同时打开才能用（家长后台 → 在线内容）：

1. **在线故事与内容** —— 允许这台机器连服务器；
2. **允许联机对战** —— 允许和别人一起玩。

分成两个开关，是因为家长可能想让孩子看故事、但不想让他和外面的人下棋。

#### 两种游戏，两种玩法

| 游戏 | 玩法 | 同步什么 |
| --- | --- | --- |
| 国际象棋 | 回合制，房主执白、来客执黑 | 一串走法 |
| 华容道 | 竞速，同一道题同时开始 | 开始时刻 + 完成时间 |

华容道做成竞速而不是「一人一步」，是因为两个人轮流挪同一副棋子只会互相拆台：
你腾出来的空位对方立刻就堵上了，玩下来既不像合作也不像对战。竞速的规则公平
（同一道题），同步量也最小（只要知道什么时候开始、谁先完成）。对手先解出来时
**不打断还在解的孩子**，他这一盘可以继续解完，只是顶部多一句「小伙伴 1 分 20 秒
就解出来了」。

#### 怎么同步：存走法，不存棋盘

服务端房间里存的是 `[{f, t, p}, ...]` —— 从开局到现在每一步落在哪两个格子、
升变选了什么。两个客户端各自从开局重放一遍，局面必然一致。

这样做的三个好处：

- 不需要「当前棋盘快照」，也就不需要两边对某个快照达成一致；
- 网络只传 3 个字段，`piece / cap / castle / ep` 都能从局面推出来；
- 客户端本来就有 `legalMoves` / `applyMove`，重放不用写新代码。

重放时用 `Chess.findMove(state, from, to, promo)` 把三个字段换回完整的着法对象 ——
直接 `applyMove({from, to})` 会丢掉易位权和吃过路兵标记，两边迟早走岔。

要防的两个坑：

- **自己那步会被重放两次**：本地为了手感先落子，1.5 秒后轮询又收到同一步。
  所以页面记一个 `pendingMove`，轮询时认出来「这是我走的」，只把进度往前推；
- **迟到的旧响应会覆盖新状态**：轮询和落子会同时在飞。服务端每次改动都把房间的
  `version` 加一，客户端只接受 `version` 不小于当前值的响应。

服务端**不校验走子是否合规**，只校验「轮到你了吗」。在 Python 里再实现一套走子
规则等于维护两份，两份迟早不一致；而这一版服务端没有可复用的引擎。代价是孩子
能改客户端走假棋 —— 赢一盘棋的收益，远小于「必须先注册账号」把他挡在门外的
代价。真要防，正确做法是把 `ChessEngine.js` 原样搬到服务端用 node 跑。

服务端接口（完整说明见 `server/README.md`）：

    POST /api/game/create   {game, level}   → 房间号 + token
    POST /api/game/join     {code, token?}  → 进房（带自己的 token 就是重连）
    GET  /api/game/state?code=&token=       → 轮询整个房间（1.5 秒一次）
    POST /api/game/move     {code, token, from, to, promo}
    POST /api/game/finish   {code, token, result | steps}
    POST /api/game/leave    {code, token}

身份用随机 token 而不是座位号：座位号是自报家门的协议，知道对方是 a 还是 b 就能
替对面落子。每个房间只下发自己的 token。

#### 验证

`node tests/netplay_test.js`（需要本机有 python3，脚本会自己起服务端）：

- 随机 240 步「序列化 → findMove → applyMove」，结果必须与原局面完全一致
  （棋盘、轮次、易位权、吃过路兵标记，一项都不能差）；
- 两台客户端交替走 20 步，每一步之后局面都必须一模一样；
- 白方连走两步、假 token、房间结束后再走，都要被挡住；
- 华容道的倒计时、先完成者获胜、「两个人都交完才算结束」。

### 小游戏工坊（内置网页编辑器）

家长不写代码做小游戏的地方。编辑器本体是**四端共用的一份单文件 H5**
（`Resources/Raw/Maker/index.html`，Linux 侧同步到 `assets/maker/index.html`），
Qt 端只加宿主才做得到的三件事：调 `FileDialog` 选图、读 `.json` 作品、把作品写回文件。

| 能力 | 在哪 |
| --- | --- |
| 三种模板（接住掉落 / 躲开障碍 / 点一点）+ 6 类内置 emoji 素材 + 8 套背景 | 全在 `index.html` 里 |
| 选图导入 | `Maker::jsForImage()` → `window.KS.importAsset(...)` |
| 打开作品 | `Maker::jsForProject()` → `window.KS.loadProjectBase64(...)` |
| 导出作品 | `Maker::saveProjectBase64()`，落在下载目录（没有就家目录） |

入口在家长后台「小游戏工坊 → 打开工坊」，WebEngine 不可用时按钮变灰并说明原因。

**为什么编辑器装成文件、不编进 qrc**：页面要把家长的作品写进 localStorage，
而 `qrc:` 是自定义 scheme，Chromium 不给它 opaquestorage —— 表现是「当场存住了，
重开就没了」。所以 `install(DIRECTORY assets/maker)` 装到
`share/kidssafe/maker/`，用 `file://` 打开，origin 才稳定。
同理，**这个目录不能改名**：一改等于换 origin，所有作品一起消失。

**两个可选依赖**，缺了都只是降级：

- `qml6-module-qtwebengine`（Qt6::WebEngineQuick）：没有它整个入口不可用，
  构建时 CMake 会打印提示；
- `qml6-module-qtquick-dialogs`：`QtQuick.Dialogs` 在精简安装里是单独的包。
  缺了页面照常开、能画能试玩能导出，只有「导入素材 / 打开作品」点了会提示。
  `MakerFilePicker.qml` 因此单独拆成一个文件经 `Loader` 加载，加载失败也不影响主页。

作品文件是自带素材的 `.json`（图片存成 data URL），换设备直接能打开；
单张导入图上限 8 MB，超过劝退 —— 手机原图过 JS 桥会明显卡。

细节（接口约定、四端存储位置、改完怎么同步）见
[Docs/小游戏工坊.md](../Docs/小游戏工坊.md)。

### 意见反馈与问卷（`core/feedback`）

家长在家长页点「去反馈」，一页里三个标签：满意度调查 / 使用问卷 / 意见反馈。

| 文件 | 职责 |
|---|---|
| `src/core/feedback.cpp` | 组 JSON、POST `/api/feedback/submit`、失败入本机队列、启动后补传 |
| `src/qml/FeedbackPage.qml` | 三个标签的界面（星级、分项、NPS、标签、问卷、正文） |

- **地址固定官网**（`https://kids.siguangblog.cc`），不跟家长自建服务器走：反馈是给开发者的，指向家长自己的机器等于没发。联调用 `KIDSSAFE_FEEDBACK_URL` 覆盖。
- **先入队再发**：即使发送途中进程被杀，这条也不会丢；下一次启动 3 秒后自动补传。
- **4xx 直接丢弃**：服务端说内容不合格，重试一万次也不会成功，页面会显示具体原因。
- 版本号来自 CMake 的 `PROJECT_VERSION`（`KIDSSAFE_APP_VERSION` 宏），不会出现「CMake 升了版本、反馈里还是旧值」。

接口契约、队列位置、隐私边界与逐端验收见 `Docs/用户反馈与问卷.md`。

### 语言（简体中文 / English）

首次引导的第一步选语言，选完才起名字、设密码、挑应用。写在
`~/.config/kidssafe/kidssafe.conf` 的 `Language=`（`zh_CN` / `en_US`），
家长后台里有同样的两个按钮可改（老用户升级上来不经过引导，只能从这里切）。

**语言同时是一道内容闸门**，不只是文案：

| 语言 | 「故事剧场（吉里吉里）」入口 |
| --- | --- |
| `zh_CN`（默认） | **不出现**。`Settings.vnAllowed == false` |
| `en_US` | 出现，不限制 |

理由是吉里吉里游戏绝大多数是成人向视觉小说，中文分发版不能带这个入口，
所以它跟着语言走，而不是做成家长可随手打开的开关。三处一起拦：

1. `GamesHubPage` 在 `Component.onCompleted` 里把 `VnPage.qml` 那一项从
   模型里 `remove`（不是设成 invisible——Grid 会留个空洞）；
2. `openGame()` 里再判一次 `Settings.allowVn`；
3. `Main.qml` 的 `pushPage()` 最后兜底，URL 里含 `VnPage.qml` 就拒进并提示。

文案是两语字典，在 `settings.cpp` 的 `Settings::t()` 里，调用点写
`Settings.t("home.hello")` 这种 key。**已覆盖**：引导四步、语言页、首页
（问候语、五个磁贴、家长按钮、我的应用）、返回按钮。
**还没覆盖**：家长后台、故事屋、安全学堂、游戏页、媒体中心的细则文案 ——
这些仍是中文。没有引 `.ts/.qm`：只有两套文案、条目可控，字典放在同一文件里
改一句不用切工具；条目多到几百条时再迁，调用点不用动。

已知取舍：

- **`ScreenTime.remainingText` 仍然是中文**（C++ 拼的「45 分钟」）。
  英文模式下首页那颗时长胶囊会中英混排，这是目前最显眼的一处没翻。
- **切换语言不会重排已打开的页面**（QML 在页面构建时取文案）。回桌面再进
  就正常；不做「实时刷新」是因为要监听 `Settings.changed` 逐个重建页面，
  收益远小于风险。

## 四、Linux 与 Windows 的能力差异（重要）

这是移植时最容易踩空的地方。Windows 版的做法在 Wayland 下**根本无法实现**：

| 能力 | Windows 实现 | Linux 实现 |
|---|---|---|
| 屏蔽 Win / Alt+Tab / Alt+F4 / Ctrl+Shift+Esc | `SetWindowsHookEx` 低级键盘钩子 | **X11 能做到**：`XGrabKey` 独占组合键（`core/hotkeyguard`）。**Wayland 做不到**：协议不允许应用抢全局快捷键，只能靠会话裁剪 |
| 开机自启 / 被杀后自愈 | HKCU `Run` + `--kiosk-guard` 守护进程 | XDG autostart 或 systemd `--user` 单元（`Restart=always`），见 `core/startup` |
| 隐藏任务栏 | `FindWindow` + `ShowWindow(SW_HIDE)` | 不存在任务栏（会话里没有 shell） |
| 强制置顶 | `AppWindow` / `SetWindowPos` | 会话内唯一窗口，无需置顶 |
| 崩溃后自救 | 标记文件 + 退出钩子 + 恢复任务栏 | 会话脚本守护循环，非正常退出 1 秒重启 |
| 枚举已安装应用 | COM 枚举开始菜单快捷方式 | `KApplicationTrader` 扫 XDG `.desktop` |
| 启动应用 | `ShellExecute` | `KProcess` 持子进程（才能拿到退出信号） |
| 前台窗口监控 | `GetForegroundWindow` 轮询 | KWin 脚本 / KWindowSystem（待实现） |
| 护眼暖色滤镜 | 原生窗口层遮罩 | QML 顶层 `Rectangle`（`Main.qml` 第四层）。**只覆盖本窗口**，放行到外部应用时不生效，与 Windows 版行为一致；系统级滤镜要写 KWin effect，不在本版范围 |
| 故事内容服务 | 家长自建 KidsSafeServer（.NET） | `server/` 里的 Python 3 零依赖实现，接口契约相同（仅 story 四个接口） |
| TTS | SAPI | `speech-dispatcher`（`spd-say`） |
| 意见反馈 / 满意度 / 问卷 | `KidsSafeShared/Services/FeedbackService`（本机队列 + 官网接口） | `core/feedback`（同样的接口与本机队列），入口在家长页 |

**一句话原则**：Linux 侧的可靠性来自「会话裁剪」，不来自应用内的小动作。
不要在 `kiosk.cpp` 里试图复刻 Windows 的按键钩子——`hotkeyguard.cpp` 里那点
X11 抢占只是**锦上添花**，Wayland 下它会诚实地报告「不可用」。

### 开机自启、自愈与快捷键抢占（`core/startup`、`core/hotkeyguard`）

家长后台「系统守护」卡片对应三项：

| 开关 | 落地 | 说明 |
|---|---|---|
| ① 登录自动进入儿童桌面 | `~/.config/autostart/kidssafe.desktop` | 所有桌面环境都认；无崩溃重启 |
| ② 进程被结束后自动重启 | `~/.config/systemd/user/kidssafe-guard.service` | `Restart=always` `RestartSec=2`；被 kill 后 2 秒回来 |
| ③ 屏蔽系统快捷键 | `XGrabKey`（仅 X11） | Alt+Tab / Alt+F4 / Alt+Esc / Ctrl+Esc / Ctrl+Shift+Esc / Ctrl+Alt+Tab / Super |

① 与 ② **互斥**：同时开会起来两份全屏窗口互相抢焦点（画面不停闪）。
开启其中一项时，代码会自动删掉另一项的文件。

② 只 `enable` 不 `--now`：当前实例还活着，`--now` 会立刻起第二份。
本次会话的自愈仍由 `session/kidssafe-session` 的 `while` 循环负责。

③ 的两个限制（都在 `hotkeyguard.h` 顶部写了）：

- 普通桌面里 Alt+Tab 已被 KWin/Mutter 先 `XGrabKey`，我们拿到 `BadAccess`，
  静默跳过；所以这招在**没有窗口管理器的 kiosk 会话**里最有效。
- `Ctrl+Alt+Del` 与 `Ctrl+Alt+F1..F12`（VT 切换）由 X 服务器自己处理，抢不到。
  要堵 VT 只能在 `xorg.conf` 里写 `Option "DontVTSwitch" "true"`（需要 root）。

没装 `libx11-dev` 时 CMake 不定义 `KIDSSAFE_X11`，功能自动降级为「会话裁剪」，
`configure` 不会失败。

> 设计取向：宁可如实显示「Wayland 下无法抢占」，也不要让家长以为开关是开的就拦住了。


---

## 五、移植进度

已打通的主链路：**启动 → 引导 → 家长验证 → 桌面 → 启动应用 → 超时锁屏 → 家长延长**。

已移植：

- [x] 启动页 / 首次引导（昵称、家长密码、应用白名单）
- [x] 家长验证（数字键盘，带错误抖动）
- [x] 儿童桌面（内置内容入口 + 应用磁贴网格、剩余时间、外部应用遮罩）
- [x] 超时锁屏 + 家长延长
- [x] 家长后台（时长、临时加时、白名单、服务器地址与连接测试、护眼、
  小小程序员学习情况、退出）
- [x] 故事屋：分类浏览 / 搜索 / 分页 / 正文阅读 / 朗读
- [x] **安全学堂**：10 个主题 52 条贴士 + 40 道安全问答（完全离线，与三端同源）
- [x] **游戏乐园**：难度三档 + 8 个原生小游戏（数一数 / 拼图配对 / 找颜色 / 算术乐园 /
  打地鼠 / 数独 4×4 / 华容道 / 国际象棋）
- [x] **联机对战**：华容道竞速 + 国际象棋对战，凭 4 位房间号进熟人房间（没有公开匹配、
  没有聊天）；家长需同时打开「在线内容」和「联机对战」两个开关，默认都关
- [x] **网页游戏乐园**：15 款 HTML5 游戏（雷霆战机 / 五子棋 / 贪吃蛇 / 宝石消消乐 /
  斗兽棋 / 中国象棋 / 打企鹅 / 凑十 / 算术小飞行员 / 点字成诗 / 汉字合体 / 打气球 /
  象形字 / 小汽车 / 光线宝石），与 Windows、Android 端**同一份资源**，用 QtWebEngine
  原样运行；WebEngine 是可选依赖，没装时自动降级成一行安装提示
- [x] **眼保健操**：六节分步引导（放松 / 睛明穴 / 四白穴 / 太阳穴刮眼眶 / 转眼球 /
  远眺），倒计时环 + 语音口诀 + 暂停 / 下一节 / 重听
- [x] **红白机（NES）**：SimpleNes 核心静态链入，ROM 目录自管、键盘映射、
  暂停/重来、整机快照存档，声音走 QtMultimedia（可选依赖，缺了静音运行）
- [x] **故事剧场（吉里吉里）**：不链核心（GPLv2 + Win32 双重不合适），做启动器：
  自动挑 krkrsdl2 / wine / 自定义命令，游戏目录自管，退出后自动把窗口收回来
- [x] **媒体中心（听一听）**：8 首儿歌 + 3 段环境音随包播放（QtMultimedia 可选依赖），
  唐诗/三字经用 TTS 朗读；内容表 `assets/audio/library.json` 可改不需重编译
- [x] **启动语言选择（简体中文 / English）**：首次引导第一步选语言，家长后台可改；
  简体中文模式不显示「故事剧场」入口（内容合规），英文模式不限制
- [x] **小小程序员（积木 → C 语言）**：6 章 32 关（含队列 / 栈两章数据结构、
  20 个练习、6 个关底作品、「猜一猜」互动与编程作业），
  积木生成真实 C 源码交给系统 gcc 编译运行，按输出判题；关卡表
  `assets/codelab/lessons.json` 可改不需重编译，没装 gcc 时降级为只读
- [x] **项目工坊（内置项目素材）**：飞机大战 / 推箱子 / 横板冒险三个写完的作品，
  纯 QML + Canvas、无图片资源；每款配「怎么玩 / 在教什么 / 关键代码 / 可调参数」，
  滑块实时改运行参数（敌机速度、重力、跳跃力…），照六个约定的接口就能加第四款
- [x] **界面与 Windows 端对齐**：磁贴改成 squircle 色块 + 真实应用图标，所有 emoji 换成手绘矢量图标
- [x] **首页天气**：Open-Meteo 实况 + 逐小时，IP 定位或指定城市，离线回落到缓存
- [x] **安全学堂图标彩色化**：10 个主题各一张手绘彩色图标（红绿灯 / 手掌 / 火苗 /
  插头 / 水滴 / 苹果 / 雷云 / 笔记本 / 盾牌 / 听筒），贴士行改用主题色圆点
- [x] **头像与 Windows 端一致**：白底圆形 + 手绘动物头像（熊 / 虎 / 狗 / 狐 / 猪 /
  猫 / 兔 / 熊猫）。Windows 那边是彩色 emoji 贴纸，deepin 上没有 emoji 字体，
  所以按同一套配色手绘；设置里填其它 emoji 时仍退回文本显示
- [x] **护眼模式**：常开 / 定时（支持跨零点）/ 强度调节，首页有生效徽章
- [x] **故事服务端**：Python 3 零依赖实现，含家长后台页面、systemd 用户服务、16 项自测
- [x] 7 个平台服务 + 无界面自检（`--self-test`）

待移植（按优先级）：

- [ ] 媒体中心的**视频播放器**（音频部分已上，见上；视频要 `VideoOutput` +
  解码后端，工作量比音频大得多）
- [ ] 电子课本（PDF 渲染是主要工作量）
- [ ] 学习世界（口算练习 / 模拟考试，题库同 Windows 端）
- [ ] 原生小游戏补齐（看图识字 / 连连看 / 迷宫 / 大鱼吃小鱼 / 打气球…）
- [ ] 身体百科 / 操作帮助 / 口诀乐园
- [ ] 家长后台剩余页：内容审核、排行榜、服务器同步
- [ ] 前台窗口监控（KWin 脚本，用于「孩子在别的应用里待了多久」）
- [x] **打包：deb 发行版包**（`debian/` + `.desktop` + 会话文件 + 图标，见第七节）

---

## 六、已知限制

- **已在 deepin 25 / Qt 6.8 上编译并跑通主链路**（X11 会话）。其他发行版
  （Debian 13、Fedora、Arch）尚未实测，构建依赖与坑见上面第三节。
- **故事屋需要家长自建服务端**。没填地址时页面显示「家长还没开启在线故事」，
  这是预期行为。服务端用本仓库的 `server/`（`./install-server.sh`），
  契约见 `storyservice.h` 头部注释。
- **护眼遮罩只覆盖儿童桌面自己的窗口**。孩子被放行到外部应用时那个窗口不受
  控制（与 Windows 版一致）。要做到系统级，需要写 KWin effect。
- **安全学堂的内容是编译进 qrc 的**（`SafetyData.js` / `SafetyQuiz.js`），
  与三端文案保持一致。改内容要重新构建，这点与故事屋不同——它故意不联网，
  因为安全知识不能因为家里没搭服务器就消失。
- **天气需要联网**：离线时只显示缓存（最多显示上一次的数据，并标注「离线」）。
  完全没联过网的机器上这条天气是隐藏的，不会出现「--°」这种半截状态。
- **Wayland 会话尚未在真机验证**。`kidssafe-wayland-session` 里合成器的
  具体行为随 KWin 版本变化，必须在目标发行版上实测后再定稿。
- **中文语音包可能需要手动安装**：`sudo apt install speech-dispatcher-espeak-ng`
  并确认 `spd-say -l zh "测试"` 能出声，否则 TTS 静默降级。
- **语音可用性是「探测」出来的，不是猜的**：只查 `spd-say` 在不在 PATH 里会把
  「装了没配」误判成可用——按钮亮着却一点声音都没有。`Speech` 启动时会异步跑
  一次 `spd-say -O`（列出输出模块），二进制、非 dummy 输出模块两项都过才算可用，
  结果（含原因）显示在家长后台「语音朗读」卡片里，旁边有「试听」和「重新检测」。
  眼保健操的「重听口诀」、故事页的「朗读」在不可用时置灰，并把文案换成「无语音」，
  眼保健操页底部还有一行说明——比让孩子对着坏按钮反复点要好。
- **应用磁贴优先显示真实图标**（`image://theme/<Icon 字段>`，由 `ThemeIconProvider`
  渲染）。图标主题里查不到时退回「色块 + 名称首字」，这是兜底而不是常态。
  底色按应用 id 稳定分配，不会每次启动都换色。
- 家长密码是 4 位数字，加了随机盐后存 KConfig。安全性足够防孩子，
  不足以防拿到磁盘的人——这是产品定位决定的，不是实现缺陷。

---

## 七、应用分发

### 结论：发 `.deb`，不要发 AppImage / Flatpak

| 形式 | 能不能用 | 说明 |
| --- | --- | --- |
| **deb**（主推） | ✅ | deepin / UOS / Debian / Ubuntu 全都 apt。要往 `/usr/share/xsessions` 写会话、往 `/usr/bin` 放启动脚本，只有包管理器干得了 |
| 源码 + `scripts/install.sh` | ✅ 开发机 | 装到 `/usr/local`，升级靠 `git pull` + 重装，适合自己用 |
| AppImage | ⚠️ 只能试用 | 单文件很香，但**没有安装步骤**：写不进 xsessions，登录界面里选不到「儿童安全桌面」，而且 QtWebEngine 的沙箱还要 `--no-sandbox`，和「锁死」的目标相反 |
| Flatpak / Snap | ❌ | 沙箱里不能替换会话、不能拉起用户选的应用、写不了 `/usr`。kiosk 和沙箱本质冲突 |

一句话：这个程序装的是**一个登录会话**，不是普通应用，所以必须是系统级安装。

### 打一个包

```sh
sudo apt install debhelper dpkg-dev        # 只在构建机需要
./scripts/build-deb.sh
# → dist/kidssafe_1.19.0_amd64.deb
```

脚本会做三件事：把 `CMakeLists.txt` 的 `VERSION` 同步进 `debian/changelog`（版本对不上
时 apt 的升级比较会出错，是最难查的一类 bug）、`dpkg-buildpackage -b` 构建、
把产物收进 `dist/`。

> 若 `debian/rules` 从 Windows 拷过来丢了执行位，脚本会自己 `chmod +x`；
> `build-deb.sh` 本身也可以用 `sh scripts/build-deb.sh` 直接跑。

#### 打包报 `Unmet build dependencies` 怎么办

`dpkg-checkbuilddeps` 只判断「装没装」，**不区分「源里没有这个包」和「没装」**，
所以报错分两种：

- **工程真的要用**（`qt6-base-dev`、`libkf6config-dev` 这类）：`sudo apt install` 就行；
- **只是某个发行版上 Qt 自己顺带要的**：从 `debian/control` 里删掉。
  例如 `libxkbcommon-dev` / `libxkbcommon-x11-dev`——本工程一行 xkbcommon 代码都没有，
  写进 Build-Depends 只会在 deepin 25（源里没这两个包）上把打包卡死。

判据很简单：`grep -rn "xkbcommon" src/` 搜不到，就不该出现在构建依赖里。
构建依赖写多了的痛苦是「换台机器就编不过」，写少了的痛苦是编译时报错——后者好查得多。

#### `debian/kidssafe.docs: README.md: not found`

和依赖无关，是**文件权限**：debhelper 会把带执行位的 `debian/*.docs`（以及
`*.install`、`*.links`、`*.dirs` 这些同名配置文件）当成「可执行配置脚本」直接运行，
于是 `kidssafe.docs` 里那行 `README.md` 被当成命令，报 `not found`（退出码 127）
然后打包中断，日志里还会先给一条 `is marked executable but does not appear to an
executable config`。

从 Windows / U 盘 / FAT 分区拷过来的文件常常个个都带 x 位。`build-deb.sh` 现在会
自动清理，手工修就一行：

```sh
chmod -x debian/kidssafe.docs      # 或 find debian -maxdepth 1 -type f ! -name rules -exec chmod -x {} +
```

#### 装包时提示「无法被用户'_apt'访问，已脱离沙盒并提权为根用户」

这是 `N:` 开头的**通知**，不是错误，包已经装上了。

原因：`apt` 读包时默认降权到 `_apt` 用户跑在沙盒里；`sudo apt install ./dist/xxx.deb`
传的是 `/home/<你>/...` 下的路径，而家目录通常是 750/700，`_apt` 进不去，
于是 apt 放弃沙盒、改用 root 直接读。影响只是这一次少了一层隔离——包是你自己
刚构建的，可以忽略。

想去掉这条提示，把包放到 `_apt` 读得到的地方（`/tmp` 是 1777）：

```sh
cp dist/kidssafe_*.deb /tmp/ && sudo apt install /tmp/kidssafe_*.deb
```

不要为了这条提示去 `chmod o+rx ~`：为了消一条通知把家目录开放给所有本地用户，
是拿真正的安全换一行日志安静。

### 包里到底装了什么

| 路径 | 作用 |
| --- | --- |
| `/usr/bin/kidssafe` | 主程序 |
| `/usr/bin/kidssafe-session` | X11 会话入口：守护循环 + 崩溃 1 秒重启 |
| `/usr/bin/kidssafe-wayland-session` | Wayland 会话入口（kwin_wayland / cage） |
| `/usr/share/kidssafe/playland/` | 15 款网页小游戏（走文件系统，改一版不用重编译） |
| `/usr/share/xsessions/kidssafe.desktop` | 登录界面的会话菜单项（X11） |
| `/usr/share/wayland-sessions/kidssafe-wayland.desktop` | 同上（Wayland） |
| `/usr/share/icons/hicolor/scalable/apps/kidssafe.svg` | 会话菜单图标 |

**故意不放 `/usr/share/applications`**：那个目录是给应用菜单用的。孩子（或家长误点）
在已有桌面里启动它，会得到一个全屏、无边框、关不掉的窗口——正是它想防的事。
入口只在登录界面。

### 用户机上的安装、升级、卸载

```sh
sudo apt install ./kidssafe_1.19.0_amd64.deb   # 装
sudo apt remove  kidssafe                       # 卸（程序 + 会话入口，保留配置）
sudo apt purge   kidssafe                       # 卸（连 /etc 下的系统级配置一起删）
```

- 注销后，登录界面的会话菜单里选「儿童安全桌面（X11）」。想原地试跑不加会话：
  `KIDSSAFE_KIOSK_SESSION=1 kidssafe --windowed`（**不是真正的锁定**）。
- **用户数据不在包里**，升级、卸载都不会动：
  `~/.config/kidssafe/kidssafe.conf`（密码、时长、白名单、天气城市）、
  `~/.local/share/kidssafe/`（`roms/`、`saves/`、`kidssafe.log`、`.session_exit` 标记）。
  包没有写 `postrm` 去删它们——删用户数据必须是显式操作。
- 依赖是自动算的：`${shlibs:Depends}` 由 `dpkg-shlibdeps` 从 ELF 里读，换发行版不用改。
  QML 运行期模块（`qml6-module-qtquick*`）是显式写的，因为 `.so` 里看不出 QML 的 import。

### 彻底卸载（含源码安装、用户数据）

**顺序很重要：先离开儿童会话，再卸。** 在 kiosk 会话里 `apt remove` 会把正在运行
的程序从手底下抽走，会话脚本随即进入崩溃重启循环——它按 `[ -x "$APP_BIN" ]`
找不到二进制就 `exit 1`，看起来就是「屏幕卡住然后掉回登录界面」。正常做法：

1. 家长 → 输密码 → 「退出儿童桌面」→ **再点一次确认** → 回到登录界面；
2. 在登录界面换成普通会话登进去（或者 `Ctrl+Alt+F3` 切到 TTY 登录）；
3. 再执行下面的卸载命令。

```sh
# ① deb 装的
sudo apt purge kidssafe

# ② 源码装的（scripts/install.sh 装到 /usr/local）
sudo ./scripts/install.sh --uninstall
#   脚本读 build/install_manifest.txt 逐条删；build/ 没了就按已知路径删。
#   另一种写法：PREFIX=/usr/local sudo -E ./scripts/install.sh --uninstall
```

卸载后确认干净：

```sh
dpkg -l | grep kidssafe                       # 应该没有输出
ls /usr/share/xsessions /usr/share/wayland-sessions | grep kidssafe   # 应该没有
command -v kidssafe kidssafe-session          # 应该没有
```

会话列表是登录界面（deepin 上是 lightdm）启动时读的，**注销重登**即可消失；
没消失就重启一次显示管理器 `sudo systemctl restart lightdm`（不确定服务名就直接重启）。

**用户数据要单独删**，它不在卸载范围内（`roms/` 里可能还有孩子的存档，
建议先备份再删）：

```sh
cp -r ~/.local/share/kidssafe/saves ~/kidssafe-saves-backup    # 想留存档才做
rm -rf ~/.config/kidssafe ~/.local/share/kidssafe
```

如果在 kiosk 会话里彻底出不来（家长密码忘了、界面点不动），从 TTY 兜底：

```sh
# Ctrl+Alt+F3 登录，然后：
pkill -f kidssafe-session        # 停掉守护循环，否则它 1 秒后重启程序
pkill -f kidssafe                # 停掉主程序
sudo apt purge kidssafe          # 或 sudo ./scripts/install.sh --uninstall
sudo systemctl restart lightdm   # 回登录界面
```

（`pkill` 要在**卸包之前**执行。先删二进制、后杀进程的话，守护循环会先看到
「程序不见了」再 exit，中间那 1 秒会闪一下黑屏，不影响结果，只是难看。）

### 多机部署：自建 apt 源（可选）

三五十台机器靠 U 盘拷 `.deb` 很难维护版本，搭个静态源最省事：

```sh
mkdir -p repo/pool repo/dists/stable/main/binary-amd64
cp dist/*.deb repo/pool/
cd repo

dpkg-scanpackages --multiversion pool /dev/null \
  > dists/stable/main/binary-amd64/Packages
gzip -9c dists/stable/main/binary-amd64/Packages \
  > dists/stable/main/binary-amd64/Packages.gz

apt-ftparchive release dists/stable > dists/stable/Release
gpg --default-key "$KEYID" -abs -o dists/stable/Release.gpg dists/stable/Release
gpg --default-key "$KEYID" --clearsign -o dists/stable/InRelease dists/stable/Release
```

客户端加一行：

```
deb [signed-by=/usr/share/keyrings/kidssafe.gpg] https://your.host/kidssafe stable main
```

之后 `sudo apt update && sudo apt install kidssafe`，升级就是 `apt upgrade`。
内网没有 https 也能用 `file:///srv/kidssafe` 或 `http://`。**必须签名**
（`InRelease` 或 `Release + Release.gpg`），否则 apt 会拒绝这个源；实在不想签名
就得在 sources.list 里写 `[trusted=yes]`，等于把整条链路的安全让掉。
（`apt-ftparchive` 来自 `apt-utils` 包。）

### 发版纪律（比打包本身更重要）

1. 改 `CMakeLists.txt` 的 `project(kidssafe VERSION x.y.z)` → 打 tag → 跑 `build-deb.sh`
   （changelog 会自动补顶条）。**不要**只改代码不发版本：apt 只比 `Version`，
   版本没变，用户机上 `apt upgrade` 什么都不会发生。
2. 进任何公开/共享仓库前先 `lintian dist/kidssafe_*.deb`，并决定许可证——
   `debian/copyright` 里现在是 `Proprietary`，公开分发会被 lintian 判 E 级。
3. 正式渠道用 `debsign` 签 `.changes`（`dpkg-buildpackage` 去掉 `-us -uc` 换成
   `-k$KEYID`），自用可以一直不签。

### 想上 deepin 应用商店 / 公开源

商店收的就是 deb，所以上面这套流程直接能用，另外还要准备：

- **许可证**：`debian/copyright` 现在是 `Proprietary`，公开分发必须先定许可证；
- **图标与截图**：图标已有 `assets/kidssafe.svg`（512×512 可缩放）；截图拍
  首页、游戏乐园、家长后台三张即可；
- **「怎么退出」要写清楚**：kiosk 类应用审核一定会问。答案是家长后台 →
  「退出儿童桌面」→ 再点一次确认，之后会话结束、回到登录界面；
- **维护者邮箱**：把 `debian/control` 与 `debian/changelog` 里的
  `kidssafe@localhost` 换成真实地址，否则商店不会收。
