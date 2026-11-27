#!/bin/sh
#
# 装齐「儿童安全桌面」在 Debian / Ubuntu 上的构建依赖。
#
# 用法：
#   sh scripts/install-deps.sh
#
# 为什么单独写这个脚本，而不是让人照 README 敲命令：
# 原来 README 和 scripts/install.sh 顶部那张包名表混进了 Fedora / Arch 的写法。
# `libkf6kirigami-dev` 和 `qqc2-breeze-style` 在 Ubuntu 源里**都不存在**，
# 而 apt 遇到「无法定位软件包」时不会装其余部分，整条命令直接中止 ——
# 一条命令里只要有一个名字写错，等于一个包都没装上。Debian 系的真实名字是：
#
#   libkf6kirigami-dev  →  libkirigami-dev   （KF6 里只有 Kirigami 不带 kf6 前缀）
#   qqc2-breeze-style   →  qml6-module-org-kde-breeze
#
# 另外本脚本会先用 apt-cache show 把当前源里确实没有的包挑出来跳过，
# 再一次性安装剩下的，避免又被一个名字拖垮整条命令。
#
# 装完用 scripts/check-host.sh 检查运行环境（那个是只读的，不动系统）。

set -eu

bold() { printf '\n\033[1m%s\033[0m\n' "$1"; }
ok()   { printf '  \033[32m✓\033[0m %s\n' "$1"; }
warn() { printf '  \033[33m!\033[0m %s\n' "$1"; }
bad()  { printf '  \033[31m✗\033[0m %s\n' "$1"; }

if ! command -v apt-get >/dev/null 2>&1; then
    cat <<'EOF'
这个脚本只处理 Debian / Ubuntu 系（apt-get）。其它发行版的包名是：

  Fedora：
    sudo dnf install cmake gcc-c++ extra-cmake-modules \
         qt6-qtbase-devel qt6-qtdeclarative-devel \
         qt6-qtwebengine-devel qt6-qtmultimedia-devel \
         kf6-kconfig-devel kf6-kcoreaddons-devel kf6-ki18n-devel \
         kf6-kirigami-devel

  Arch：
    sudo pacman -S base-devel cmake extra-cmake-modules \
         qt6-base qt6-declarative qt6-webengine qt6-multimedia \
         kconfig kcoreaddons ki18n kirigami breeze

  （qt6-webengine / qt6-multimedia 不是可选项：网页小游戏和音频要编进二进制
    才算数，构建机上缺了，用户装任何东西都补不回来。完整清单见
    ../wiki/09-依赖清单.md）
EOF
    exit 1
fi

if [ "$(id -u)" -eq 0 ]; then
    SUDO=""
elif command -v sudo >/dev/null 2>&1; then
    SUDO="sudo"
else
    bad "当前不是 root，系统里也没有 sudo，请用 root 执行或先装 sudo"
    exit 1
fi

# ── 包清单 ──────────────────────────────────────────────────────
# 分组只是为了装的时候看得清楚，最终都是一条 apt 命令装完。
# 内容与 CMakeLists.txt 里的 find_package 严格对应：
#   Qt6 Core/Gui/Widgets/Quick/QuickControls2/Network
#   KF6 CoreAddons / Config / Kirigami / I18n
# 注意这里没有 libkf6service-dev：应用清单是自己遍历 XDG 目录读 .desktop，
# 不走 KService（见 src/core/appcatalog.cpp 的注释）。

PKGS_BUILD="build-essential cmake extra-cmake-modules"

PKGS_QT="qt6-base-dev qt6-declarative-dev"

PKGS_KF6="libkf6config-dev libkf6coreaddons-dev libkf6i18n-dev libkirigami-dev"

# 网页小游戏 + 音频。这两样**不是可选项**：CMake 找不到 WebEngineQuick 会
# 直接失败，因为「编出来的包缺功能」用户那边补不回来（装上 QML 模块也没用，
# 二进制里没有那段初始化代码）。所以放在构建依赖里，跟 Qt 基础库一起装。
PKGS_WEB="qt6-webengine-dev qt6-multimedia-dev"

# QML 运行时模块。缺 Controls / Layouts 的话窗口能起、界面是空的。
PKGS_QML="
qml6-module-qtquick
qml6-module-qtquick-window
qml6-module-qtquick-controls
qml6-module-qtquick-layouts
qml6-module-qtquick-templates
qml6-module-qtqml-workerscript
qml6-module-org-kde-kirigami
"

# 可选：Breeze 控件样式。缺了不影响功能，main.cpp 会先探测再回退到内置样式，
# 只是观感和 KDE 桌面不一致。Ubuntu 上叫 qml6-module-org-kde-breeze。
PKGS_STYLE="qml6-module-org-kde-breeze"

MISSING_ALL=""

install_set() {
    label="$1"
    shift

    # 先按当前源筛一遍，再决定这一组装什么。
    avail=""
    absent=""
    for p in "$@"; do
        if apt-cache show "$p" >/dev/null 2>&1; then
            avail="$avail $p"
        else
            absent="$absent $p"
        fi
    done

    bold "安装 $label"
    for p in $absent; do
        warn "当前源里没有 $p，跳过"
        MISSING_ALL="$MISSING_ALL $p"
    done

    if [ -z "$avail" ]; then
        warn "$label 一个都没装到"
        return 0
    fi

    # shellcheck disable=SC2086  # 这里就是要按空格拆成多个包名
    if ! $SUDO apt-get install -y $avail; then
        bad "$label 安装失败，看上面的 apt 输出"
        exit 1
    fi
    ok "$label 完成"
}

bold "更新软件包索引"
$SUDO apt-get update

install_set "编译工具" $PKGS_BUILD
install_set "Qt 6"     $PKGS_QT
install_set "KF6 框架" $PKGS_KF6
install_set "QML 模块" $PKGS_QML
install_set "网页小游戏 / 音频（必需）" $PKGS_WEB

# 运行期 QML 模块：不打 .deb、直接从 build 目录跑的时候也必须有，
# 否则打开网页页会报 module "QtWebEngine" is not installed。
install_set "网页小游戏运行期模块" qml6-module-qtwebengine
install_set "Breeze 样式（可选）" $PKGS_STYLE

# ── 校验 ────────────────────────────────────────────────────────
bold "校验"

QTVER="$(qtpaths6 --query QT_VERSION 2>/dev/null || true)"
if [ -n "$QTVER" ]; then
    ok "Qt $QTVER"
fi

QMLDIR="$(qtpaths6 --query QT_INSTALL_QML 2>/dev/null || true)"
if [ -z "$QMLDIR" ]; then
    # qtpaths6 是 qt6-base-dev 里的工具，理论上上面装完就有了；兜底走默认路径。
    for d in /usr/lib/x86_64-linux-gnu/qt6/qml /usr/lib/qt6/qml /usr/lib64/qt6/qml; do
        [ -d "$d" ] && QMLDIR="$d" && break
    done
fi

check_module() {
    path="$1"
    name="$2"
    if [ -f "$QMLDIR/$path/qmldir" ]; then
        ok "$name"
    else
        bad "$name 缺失（$QMLDIR/$path/qmldir）"
        return 1
    fi
}

FAIL=0
check_module "org/kde/kirigami" "Kirigami QML 模块" || FAIL=1
check_module "QtQuick/Controls" "QtQuick.Controls" || FAIL=1
check_module "QtQuick/Layouts"  "QtQuick.Layouts"  || FAIL=1
# 这两项缺失时 CMake 根本过不去，但报错在「find_package」那一行，
# 不如在这里直接说清楚少了什么。
check_module "QtWebEngine"      "QtWebEngine（网页小游戏）" || FAIL=1

# 样式只提示，不算失败。
if [ -f "$QMLDIR/org/kde/breeze/qmldir" ]; then
    ok "Breeze 控件样式"
else
    warn "没有 org.kde.breeze，程序会回退到内置样式（功能不受影响）"
fi

if [ -n "$MISSING_ALL" ]; then
    bold "这些包在当前源里没有"
    printf '%s\n' "$MISSING_ALL" | tr ' ' '\n' | sed '/^$/d' | sed 's/^/    /'
fi

# Kirigami 的 dev 包缺失是硬失败：CMake 的 find_package(KF6Kirigami) 过不去。
case " $MISSING_ALL " in
    *" libkirigami-dev "*)
        cat <<'EOF'

  Kirigami 6 只在 Ubuntu 26.04（resolute）及更新的官方源里才有。
  24.04 LTS 上 apt 只会给出 KF5 的 kirigami2-dev，装不了这个 Qt6 工程。
  三条路：升级系统 / 加 KDE 的 PPA / 用 kdesrc-build 自己编 KF6。
  查看当前版本：  lsb_release -a
EOF
        exit 1
        ;;
esac

if [ "$FAIL" -ne 0 ]; then
    printf '\n  依赖没装全，先解决上面带 ✗ 的项。\n\n'
    exit 1
fi

bold "依赖齐了，下一步"
cat <<'EOF'
    ./scripts/install.sh              # 编译并安装到 /usr/local
    ./scripts/check-host.sh           # 装完检查运行环境（只读，不改系统）

  只想先看一眼界面，不装进系统：
    cmake -S . -B build && cmake --build build --parallel "$(nproc)"
    ./build/kidssafe --windowed

EOF
exit 0
