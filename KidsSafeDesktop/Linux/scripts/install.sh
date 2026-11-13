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
if [ "$PREFIX" != "/usr" ]; then
    if [ -x "${PREFIX}/bin/kidssafe" ] && [ ! -e /usr/bin/kidssafe ]; then
        echo "==> 链接 ${PREFIX}/bin/kidssafe -> /usr/bin/kidssafe"
        sudo ln -s "${PREFIX}/bin/kidssafe" /usr/bin/kidssafe
    fi
fi

echo
echo "完成。两种进入方式："
echo "  1) 注销后，在登录界面的会话菜单里选「儿童安全桌面」"
echo "  2) 不注销直接试跑：KIDSSAFE_KIOSK_SESSION=1 kidssafe"
echo
echo "提示：方式 2 只是全屏运行，没有会话裁剪，不是真正的锁定状态。"
echo "      要验证锁定效果必须走方式 1。"
