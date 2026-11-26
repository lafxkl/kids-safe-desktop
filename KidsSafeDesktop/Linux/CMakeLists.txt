cmake_minimum_required(VERSION 3.20)

project(kidssafe VERSION 1.19.0 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_AUTOMOC ON)
set(CMAKE_INCLUDE_CURRENT_DIR ON)

find_package(Qt6 6.5 REQUIRED COMPONENTS Core Gui Widgets Quick QuickControls2 Network
             OPTIONAL_COMPONENTS WebEngineQuick Multimedia)

# 网页小游戏（15 款 HTML5）靠 QtWebEngine 原样内嵌，与 Windows / Android 用同一份资源。
# 但 WebEngine 不是每个发行版都装（deepin 25 要单独装 qt6-webengine-dev，
# 而且版本必须和 Qt 基础库一致），所以列为 OPTIONAL：
#   找到 → 游戏乐园多一个「网页游戏乐园」入口，15 款全齐；
#   没找到 → 只保留原生小游戏，其余功能完全不受影响，configure 也不会失败。
# Config 存配置，Kirigami 提供 QML 组件与主题，I18n 提供 i18n() 与 KLocalizedString。
# 刻意不依赖 KF6Service：它的 KApplicationTrader 要靠 ksycoca 缓存与
# kbuildsycoca6 进程，在没有完整会话总线的环境（kiosk / 容器 / 精简系统）里
# 首次查询可能长时间不返回。应用清单改为直接读 XDG 目录下的 .desktop，
# 见 src/core/appcatalog.cpp。
# 各发行版对 KF6 的打包方式不一样：Arch / Fedora / Ubuntu 有「总入口」
# KF6Config.cmake（由 extra-cmake-modules 提供），Debian / deepin 上这个文件
# 可能缺失，只能逐个 find_package(KF6Xxx)。先试总入口，失败再退回去，
# 这样换机器不用改构建脚本。
find_package(KF6 6.0 QUIET COMPONENTS CoreAddons Config Kirigami I18n)
if(NOT KF6_FOUND)
    message(STATUS "未找到 KF6 总入口 (KF6Config.cmake)，改为逐个查找组件")
    find_package(KF6CoreAddons 6.0 REQUIRED)
    find_package(KF6Config 6.0 REQUIRED)
    find_package(KF6Kirigami 6.0 REQUIRED)
    find_package(KF6I18n 6.0 REQUIRED)
endif()

qt_standard_project_setup()

set(KIDSSAFE_SOURCES
    src/main.cpp
    src/core/settings.cpp
    src/core/settings.h
    src/core/screentime.cpp
    src/core/screentime.h
    src/core/appcatalog.cpp
    src/core/appcatalog.h
    src/core/applauncher.cpp
    src/core/applauncher.h
    src/core/eyecare.cpp
    src/core/eyecare.h
    src/core/kiosk.cpp
    src/core/kiosk.h
    src/core/startup.cpp
    src/core/startup.h
    src/core/hotkeyguard.cpp
    src/core/hotkeyguard.h
    src/core/feedback.cpp
    src/core/feedback.h
    src/core/speech.cpp
    src/core/speech.h
    src/core/storyservice.cpp
    src/core/storyservice.h
    src/core/themeicon.cpp
    src/core/themeicon.h
    src/core/weather.cpp
    src/core/weather.h
    src/core/playland.cpp
    src/core/playland.h
    src/core/vnengine.cpp
    src/core/vnengine.h
    src/core/mediaplayer.cpp
    src/core/mediaplayer.h
    src/core/codelab.cpp
    src/core/codelab.h
    # 把学习情况（关卡进度 + 作业成绩）上报到家长后台：复用故事服务器地址，
    # 没填地址时整个模块静默待命。
    src/core/learnreport.cpp
    src/core/learnreport.h
    src/core/maker.cpp
    src/core/maker.h
    src/nes/nesemulator.cpp
    src/nes/nesemulator.h
    src/nes/nesview.cpp
    src/nes/nesview.h
)

# ---- 红白机核心（SimpleNes，MIT）----
# 纯 C++17，不依赖 Qt，单独编成静态库：这样它的编译选项、警告、日志
# 都和界面代码隔离开，将来换模拟器核心也只动这一个 target。
# 注意 SaveBundle.h 的文件名大小写：上游仓库里是 savebundle.h，
# Windows 上能编，Linux 上会直接「找不到头文件」。
set(KIDSSAFE_NES_CORE
    src/nes/core/Devices.cpp
    src/nes/core/Devices.h
    src/nes/core/Famicom.cpp
    src/nes/core/Famicom.h
    src/nes/core/FamicomEvents.h
    src/nes/core/Logger.cpp
    src/nes/core/Logger.h
    src/nes/core/NesCPU.cpp
    src/nes/core/NesCPU.h
    src/nes/core/NesCPU_Illegal.cpp
    src/nes/core/NesCPUBus.cpp
    src/nes/core/NesCPUBus.h
    src/nes/core/NesCartridge.cpp
    src/nes/core/NesCartridge.h
    src/nes/core/NesGamePad.cpp
    src/nes/core/NesGamePad.h
    src/nes/core/NesPAPU.cpp
    src/nes/core/NesPAPU.h
    src/nes/core/NesPPU.cpp
    src/nes/core/NesPPU.h
    src/nes/core/Rom.h
    src/nes/core/SaveBundle.cpp
    src/nes/core/SaveBundle.h
    src/nes/core/common.h
    src/nes/core/cpu/CPUFlags.h
    src/nes/core/cpu/FlagRegister.h
    src/nes/core/cpu/InterruptFlags.h
    src/nes/core/mapper/Mapper.cpp
    src/nes/core/mapper/Mapper.h
    src/nes/core/mapper/Mapper000.cpp
    src/nes/core/mapper/Mapper000.h
    src/nes/core/mapper/Mapper001.cpp
    src/nes/core/mapper/Mapper001.h
    src/nes/core/mapper/Mapper002.cpp
    src/nes/core/mapper/Mapper002.h
    src/nes/core/mapper/Mapper003.cpp
    src/nes/core/mapper/Mapper003.h
    src/nes/core/mapper/Mapper004.cpp
    src/nes/core/mapper/Mapper004.h
    src/nes/core/mapper/Mapper007.cpp
    src/nes/core/mapper/Mapper007.h
    src/nes/core/mapper/Mapper023.cpp
    src/nes/core/mapper/Mapper023.h
    src/nes/core/mapper/Mapper066.cpp
    src/nes/core/mapper/Mapper066.h
    src/nes/core/mapper/Mapper074.cpp
    src/nes/core/mapper/Mapper074.h
    src/nes/core/mapper/Mapper245.cpp
    src/nes/core/mapper/Mapper245.h
    src/nes/core/mapper/MapperFactory.h
    src/nes/core/mapper/MapperHost.h
)

add_library(kidssafe_nes_core STATIC ${KIDSSAFE_NES_CORE})
target_include_directories(kidssafe_nes_core PUBLIC
    ${CMAKE_CURRENT_SOURCE_DIR}/src/nes/core)

qt_add_executable(kidssafe ${KIDSSAFE_SOURCES})

target_include_directories(kidssafe PRIVATE src src/nes)

target_link_libraries(kidssafe PRIVATE
    Qt6::Core
    Qt6::Gui
    Qt6::Widgets
    Qt6::Quick
    Qt6::QuickControls2
    Qt6::Network
    KF6::CoreAddons
    KF6::ConfigCore
    KF6::Kirigami
    KF6::I18n
    kidssafe_nes_core
)

# ---- 系统快捷键抢占（可选）----
# kiosk 会话里想让孩子按 Alt+Tab / Ctrl+Shift+Esc 没反应，X11 下的办法是
# XGrabKey 把组合键独占过来。Wayland 完全没这个能力（见 core/hotkeyguard.h 注释）。
# 没装 libx11-dev 时不编译这段，功能自动降级为「靠会话裁剪」，configure 不会失败。
# 上报反馈时带上真实版本号：出问题时能一眼看出是哪个版本。
# 不写死字符串，避免 CMake 里升了版本、反馈里还是旧值。
target_compile_definitions(kidssafe PRIVATE KIDSSAFE_APP_VERSION="${PROJECT_VERSION}")

find_package(X11 QUIET)
if(X11_FOUND)
    message(STATUS "X11 可用：kiosk 会话可抢占 Alt+Tab / Alt+F4 / Ctrl+Shift+Esc 等组合键")
    target_link_libraries(kidssafe PRIVATE X11::X11)
    target_compile_definitions(kidssafe PRIVATE KIDSSAFE_X11=1)
else()
    message(STATUS "未找到 X11 开发包：不抢占系统快捷键"
                   "（Debian/deepin: sudo apt install libx11-dev 可启用）")
endif()

# ---- 红白机声音（可选）----
# 44100Hz 单声道 16 位直通 QAudioSink。没装 qt6-multimedia-dev 也能编译，
# 只是游戏静音 —— 界面会提示「没有声音」，不隐藏入口。
if(TARGET Qt6::Multimedia)
    message(STATUS "QtMultimedia 可用：红白机有声，媒体中心可播放")
    target_link_libraries(kidssafe PRIVATE Qt6::Multimedia)
    target_compile_definitions(kidssafe PRIVATE
        KIDSSAFE_NES_AUDIO=1
        # 媒体中心（听一听）的播放器。没有它时页面照样能打开，
        # 只是音频卡片点下去会提示「这个版本放不了音频」。
        KIDSSAFE_MEDIA=1)
else()
    message(STATUS "未找到 QtMultimedia：红白机静音、媒体中心不能播放"
                   "（deepin 上执行 sudo apt install qt6-multimedia-dev 可启用）")
endif()

# 媒体中心的内容目录：从 build 目录直接跑二进制时，不用先 install 也能找到
# assets/audio/library.json。装到系统后仍以 share/kidssafe/audio 为准。
target_compile_definitions(kidssafe PRIVATE
    KIDSSAFE_AUDIO_SRC="${CMAKE_CURRENT_SOURCE_DIR}/assets/audio")

# 小小程序员的关卡表：同一套兜底逻辑，从 build 目录跑时直接读源码树的
# assets/codelab，装上以后读 share/kidssafe/codelab。
target_compile_definitions(kidssafe PRIVATE
    KIDSSAFE_CODELAB_SRC="${CMAKE_CURRENT_SOURCE_DIR}/assets/codelab")

# ---- 网页小游戏（可选）----
# 只有 WebEngineQuick 找到时才编进 WebGamePage.qml 与 Playland 的探测逻辑。
# KIDSSAFE_PLAYLAND_SRC 是源码树里的资源目录：从 build 目录直接跑二进制时，
# 不用先 make install 也能打开网页大厅。
# 判据用 TARGET 而不是 Qt6WebEngineQuick_FOUND：只要组件真的找到了，
# 导入目标就一定存在；变量名在不同 Qt 版本/发行版打包下有出入，目标名不会。
if(TARGET Qt6::WebEngineQuick)
    message(STATUS "QtWebEngine 可用：网页小游戏入口已启用")
    target_link_libraries(kidssafe PRIVATE Qt6::WebEngineQuick)
    target_compile_definitions(kidssafe PRIVATE
        KIDSSAFE_WEBENGINE=1
        KIDSSAFE_PLAYLAND_SRC="${CMAKE_CURRENT_SOURCE_DIR}/assets/playland"
        KIDSSAFE_MAKER_SRC="${CMAKE_CURRENT_SOURCE_DIR}/assets/maker")
    set(KIDSSAFE_QML_WEB src/qml/WebGamePage.qml
                         src/qml/MakerPage.qml
                         src/qml/MakerFilePicker.qml)
else()
    message(STATUS "未找到 QtWebEngine：网页小游戏不可用"
                   "（deepin 上执行 sudo apt install qt6-webengine-dev 可启用）")
    set(KIDSSAFE_QML_WEB "")
endif()

# QML 全部放在同一目录：同目录 QML 文件之间无需 import 即可互相引用，
# 这是 qrc 打包下最不容易出错的组织方式。
qt_add_resources(kidssafe "qml"
    PREFIX "/qml"
    BASE "${CMAKE_CURRENT_SOURCE_DIR}/src/qml"
    FILES
        src/qml/Main.qml
        src/qml/Backdrop.qml
        src/qml/LargeButton.qml
        src/qml/PinPad.qml
        src/qml/AppTile.qml
        src/qml/HomeBanner.qml
        src/qml/WeatherCard.qml
        src/qml/GlyphIcon.qml
        src/qml/SplashPage.qml
        src/qml/OnboardingPage.qml
        src/qml/ParentGatePage.qml
        src/qml/HomePage.qml
        src/qml/LockoutPage.qml
        src/qml/ParentDashboardPage.qml
        # 意见反馈 / 满意度 / 问卷（数据进官网，见 core/feedback）
        src/qml/FeedbackPage.qml
        src/qml/StoryHousePage.qml
        src/qml/StoryReaderPage.qml
        src/qml/SafetyWorldPage.qml
        src/qml/SafetyTopicPage.qml
        src/qml/SafetyQuizPage.qml
        # 小游戏：外壳 + 结算 + 大厅 + 五个游戏页
        src/qml/GameShell.qml
        src/qml/GameResultOverlay.qml
        src/qml/GamesHubPage.qml
        src/qml/CountingGamePage.qml
        src/qml/MatchingGamePage.qml
        src/qml/FindColorGamePage.qml
        src/qml/ArithmeticGamePage.qml
        src/qml/WhackMoleGamePage.qml
        src/qml/SudokuGamePage.qml
        src/qml/EyeExercisePage.qml
        src/qml/NesPage.qml
        # 故事剧场：起外部吉里吉里引擎，不链核心（见 core/vnengine.h）
        src/qml/VnPage.qml
        # 媒体中心（听一听）：儿歌 / 环境音 / 国学朗读
        src/qml/MediaHubPage.qml
        # 小小程序员：积木 → C 源码 → 系统 GCC 编译（见 core/codelab.h）
        src/qml/CodeStudioPage.qml
        # 编程作业：纸面题（选择 / 填空 / 排顺序），判题在 JS 里，不需要编译器。
        # 漏加 HomeworkData.js 的表现是打开作业页就报
        # 「HomeworkData is not defined」，编译期不报错。
        src/qml/HomeworkPage.qml
        src/qml/HomeworkData.js
        # 关卡统计（通关数 / 每章进度）：小小程序员页顶的「x / y」和家长后台的
        # 学习情况卡共用，避免两处各算一遍、口径不一样。
        src/qml/LessonStats.js
        # 项目工坊：三份「现成的作品」给孩子拆（飞机大战 / 推箱子 / 横板冒险）。
        # 子目录的 alias 相对 BASE 生成，所以 Loader.source 写 "projects/Xxx.qml"
        # 正好落在 qrc:/qml/projects/Xxx.qml —— 漏加这四个文件的表现是
        # 运行时白屏 + 「Cannot load ... projects/PlaneWar.qml」，编译期不报错。
        src/qml/ProjectLabPage.qml
        src/qml/projects/PlaneWar.qml
        src/qml/projects/Sokoban.qml
        src/qml/projects/Platformer.qml
        # 网页小游戏宿主页：没找到 WebEngine 时这个变量是空的，
        # 页面就不会进 qrc，游戏乐园也只显示原生小游戏。
        ${KIDSSAFE_QML_WEB}
        # 安全内容用 JS 文件而不是 QML 单例/JSON：.pragma library 让整个应用
        # 共享一份，编辑文案时也不用改 C++ 或重新设计数据结构。
        src/qml/SafetyData.js
        src/qml/SafetyQuiz.js
)

include(GNUInstallDirs)

install(TARGETS kidssafe RUNTIME DESTINATION ${CMAKE_INSTALL_BINDIR})

# 网页小游戏的 HTML/CSS/JS/图片：按目录装，不走 qrc。
# 走文件系统的好处是改一版游戏不用重编译，家长也能直接替换资源排障；
# Playland 会在 <prefix>/share/kidssafe/playland 找 index.html。
if(TARGET Qt6::WebEngineQuick)
    install(DIRECTORY assets/playland
            DESTINATION ${CMAKE_INSTALL_DATADIR}/kidssafe)
    # 小游戏工坊的编辑器（单文件 H5，四端共用）。装成文件而不是编进 qrc：
    # 编辑器要写 localStorage 存作品，qrc: 是自定义 scheme 拿不到 storage。
    install(DIRECTORY assets/maker
            DESTINATION ${CMAKE_INSTALL_DATADIR}/kidssafe)
endif()

# 媒体中心的音频与内容表（library.json）。同样按目录装：
# 家长想换内容表里的文案，改完重启就行，不用重编译。
install(DIRECTORY assets/audio
        DESTINATION ${CMAKE_INSTALL_DATADIR}/kidssafe)

# 小小程序员的关卡表（lessons.json）。按目录装的理由同上：
# 老师/家长想加一道题、改一句讲解，改 JSON 就行，不必重编译。
install(DIRECTORY assets/codelab
        DESTINATION ${CMAKE_INSTALL_DATADIR}/kidssafe)
install(PROGRAMS
        session/kidssafe-session
        session/kidssafe-wayland-session
        DESTINATION ${CMAKE_INSTALL_BINDIR})

# X11 与 Wayland 两套会话入口都装上，显示管理器按当前会话类型挑一个
install(FILES session/kidssafe.desktop
        DESTINATION ${CMAKE_INSTALL_DATADIR}/xsessions)
install(FILES session/kidssafe-wayland.desktop
        DESTINATION ${CMAKE_INSTALL_DATADIR}/wayland-sessions)

# 图标装进 hicolor 主题：两个 .desktop 里写的都是 Icon=kidssafe，
# 名字对不上（或干脆没装）时，登录界面的会话菜单会退化成一张空白方块。
install(FILES assets/kidssafe.svg
        DESTINATION ${CMAKE_INSTALL_DATADIR}/icons/hicolor/scalable/apps)
