#!/bin/sh
#
# 构建 + 安装 Linux 版儿童安全桌面。
#
# 依赖（Debian/Ubuntu 系）：
#   sudo apt install build-essential cmake extra-cmake-modules \
#        qt6-base-dev qt6-declarative-dev \
#        libkf6config-dev libkf6coreaddons-dev libkf6service-dev \
#        libkf6kirigami-dev libkf6i18n-dev \
#        qml6-module-qtquick qml6-module-qtquick-controls qml6-module-qtquick-layouts \
#        qqc2-breeze-style
#
# Fedora 系：dnf install cmake gcc-c++ extra-cmake-modules \
#        qt6-qtbase-devel qt6-qtdeclarative-devel \
#        kf6-kconfig-devel kf6-kcoreaddons-devel kf6-kservice-devel \
#        kf6-kirigami-devel kf6-ki18n-devel
#
# Arch：pacman -S base-devel cmake extra-cmake-modules qt6-base qt6-declarative \
#        kconfig kcoreaddons kservice kirigami ki18n breeze

set -eu

SRC_DIR="$(cd "$(dirname "$0")/.." && pwd)"
BUILD_DIR="${BUILD_DIR:-${SRC_DIR}/build}"
PREFIX="${PREFIX:-/usr/local}"

# 源码安装没有包管理器记账，卸载只能靠这份脚本。用 CMake 装完后生成的
# install_manifest.txt 逐条删最准；那份清单不在（比如 build/ 被删了）时
# 退回按已知路径删 —— 路径清单跟着 CMakeLists.txt 的 install() 一起维护。
if [ "${1:-}" = "--uninstall" ] || [ "${1:-}" = "-u" ]; then
    echo "==> 卸载 $PREFIX 下的儿童安全桌面"
    manifest="${BUILD_DIR}/install_manifest.txt"
    if [ -f "$manifest" ]; then
        # 用 if 而不是 `cmd && cmd`：后者在 set -e 下，条件不成立时
        # 整条 AND 列表返回 1，循环最后一条会连带把脚本带走。
        while IFS= read -r f; do
            if [ -n "$f" ]; then
                sudo rm -f "$f"
            fi
        done < "$manifest"
        echo "    按 $manifest 删除完成"
    else
        echo "    没有 $manifest，按已知路径删除"
        for f in \
            "$PREFIX/bin/kidssafe" \
            "$PREFIX/bin/kidssafe-session" \
            "$PREFIX/bin/kidssafe-wayland-session" \
            "$PREFIX/share/xsessions/kidssafe.desktop" \
            "$PREFIX/share/wayland-sessions/kidssafe-wayland.desktop" \
            "$PREFIX/share/icons/hicolor/scalable/apps/kidssafe.svg"
        do
            if [ -e "$f" ]; then
                sudo rm -f "$f"
            fi
        done
        case "$PREFIX/share/kidssafe" in
            */share/kidssafe)
                if [ -d "$PREFIX/share/kidssafe" ]; then
                    sudo rm -rf "$PREFIX/share/kidssafe"
                fi
                ;;
        esac
    fi

    # /usr/bin/kidssafe 是安装时手工建的软链，不在 CMake 清单里。
    if [ -L /usr/bin/kidssafe ]; then
        sudo rm -f /usr/bin/kidssafe
    fi

    echo
    echo "程序已卸载。用户数据**没有动**（删不删由你决定）："
    echo "  ~/.config/kidssafe/kidssafe.conf   密码、时长、白名单"
    echo "  ~/.local/share/kidssafe/           roms/、saves/、kidssafe.log"
    echo "确认不要了再执行："
    echo "  rm -rf ~/.config/kidssafe ~/.local/share/kidssafe"
    echo
    echo "注销后登录界面就不再出现「儿童安全桌面」这一项。"
    exit 0
fi

echo "==> 配置 $BUILD_DIR"
cmake -S "$SRC_DIR" -B "$BUILD_DIR" \
      -DCMAKE_BUILD_TYPE=RelWithDebInfo \
      -DCMAKE_INSTALL_PREFIX="$PREFIX"

echo "==> 编译"
cmake --build "$BUILD_DIR" --parallel "$(nproc)"

echo "==> 安装到 $PREFIX"
sudo cmake --install "$BUILD_DIR"

# 会话脚本里默认找 /usr/bin/kidssafe，装到 /usr/local 时需要让会话能找到。
# 用一个软链解决，比改脚本更透明。
# 会话脚本会自己在 /usr/local/bin 里找二进制，所以这个软链只是图方便。
# deepin 25 这类「不可变系统」把 /usr 挂成只读，建不了软链是正常现象，
# 不能让它中断安装流程（以前这里会把后面的提示整段吞掉）。
if [ "$PREFIX" != "/usr" ]; then
    if [ -x "${PREFIX}/bin/kidssafe" ] && [ ! -e /usr/bin/kidssafe ]; then
        echo "==> 尝试链接 ${PREFIX}/bin/kidssafe -> /usr/bin/kidssafe"
        if sudo ln -s "${PREFIX}/bin/kidssafe" /usr/bin/kidssafe 2>/dev/null; then
            echo "    完成"
        else
            echo "    /usr 只读，跳过软链（会话脚本会自己找 ${PREFIX}/bin/kidssafe）"
        fi
    fi
fi

echo
echo "完成。两种进入方式："
echo "  1) 注销后，在登录界面的会话菜单里选「儿童安全桌面」"
echo "  2) 不注销直接试跑：KIDSSAFE_KIOSK_SESSION=1 kidssafe"
echo
echo "提示：方式 2 只是全屏运行，没有会话裁剪，不是真正的锁定状态。"
echo "      要验证锁定效果必须走方式 1。"
