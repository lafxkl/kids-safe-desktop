cmake_minimum_required(VERSION 3.20)

project(kidssafe VERSION 1.19.0 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_AUTOMOC ON)
set(CMAKE_INCLUDE_CURRENT_DIR ON)

find_package(Qt6 6.5 REQUIRED COMPONENTS Core Gui Widgets Quick QuickControls2 Network)
# Service 提供 KApplicationTrader（枚举 .desktop），Config 存配置，
# Kirigami 提供 QML 组件与主题，I18n 提供 i18n() 与 KLocalizedString
find_package(KF6 6.0 REQUIRED COMPONENTS CoreAddons Config Service Kirigami I18n)

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
    src/core/kiosk.cpp
    src/core/kiosk.h
    src/core/speech.cpp
    src/core/speech.h
    src/core/storyservice.cpp
    src/core/storyservice.h
)

qt_add_executable(kidssafe ${KIDSSAFE_SOURCES})

target_include_directories(kidssafe PRIVATE src)

target_link_libraries(kidssafe PRIVATE
    Qt6::Core
    Qt6::Gui
    Qt6::Widgets
    Qt6::Quick
    Qt6::QuickControls2
    Qt6::Network
    KF6::CoreAddons
    KF6::ConfigCore
    KF6::Service
    KF6::Kirigami
    KF6::I18n
)

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
        src/qml/SplashPage.qml
        src/qml/OnboardingPage.qml
        src/qml/ParentGatePage.qml
        src/qml/HomePage.qml
        src/qml/LockoutPage.qml
        src/qml/ParentDashboardPage.qml
        src/qml/StoryHousePage.qml
        src/qml/StoryReaderPage.qml
)

include(GNUInstallDirs)

install(TARGETS kidssafe RUNTIME DESTINATION ${CMAKE_INSTALL_BINDIR})
install(PROGRAMS
        session/kidssafe-session
        session/kidssafe-wayland-session
        DESTINATION ${CMAKE_INSTALL_BINDIR})

# X11 与 Wayland 两套会话入口都装上，显示管理器按当前会话类型挑一个
install(FILES session/kidssafe.desktop
        DESTINATION ${CMAKE_INSTALL_DATADIR}/xsessions)
install(FILES session/kidssafe-wayland.desktop
        DESTINATION ${CMAKE_INSTALL_DATADIR}/wayland-sessions)
