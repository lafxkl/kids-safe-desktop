#!/bin/sh
#
# 打一个 .deb（deepin / UOS / Debian / Ubuntu 通用）。
#
#   ./scripts/build-deb.sh
#
# 产物落在 dist/：kidssafe_<version>_<arch>.deb
#
# 前置依赖（只在构建机需要，用户机不需要）：
#   sudo apt install debhelper cmake extra-cmake-modules \
#        qt6-base-dev qt6-declarative-dev libxkbcommon-dev \
#        qt6-webengine-dev qt6-multimedia-dev \
#        libkf6config-dev libkf6coreaddons-dev libkf6i18n-dev \
#        libkf6kirigami-dev        # deepin 25 上叫 libkirigami-dev
#
# 关于网页小游戏：这不是「构建机有就带上」的可选功能。构建机上没有
# qt6-webengine-dev，编出来的二进制里就没有 WebEngine 的初始化代码，用户
# 事后装 qml6-module-qtwebengine 也打不开 —— 只能重新编译。所以 CMake 默认
# 缺了就失败，这个脚本也会先检查一遍，避免在 dpkg-buildpackage 的几百行输出
# 里去找那一行原因。真要精简包，加 -DKIDSSAFE_MINIMAL=ON 显式关掉。

set -eu

SRC_DIR="$(cd "$(dirname "$0")/.." && pwd)"
cd "$SRC_DIR"

for tool in dpkg-buildpackage dpkg-parsechangelog; do
    if ! command -v "$tool" >/dev/null 2>&1; then
        echo "缺少 $tool：先 sudo apt install dpkg-dev debhelper" >&2
        exit 1
    fi
done

# ---- 构建依赖预检 ----
# 见文件头：网页小游戏和音频默认是必编进包里的，缺了 -dev 包不是「少个功能」，
# 而是打出来的包装什么都补不回来。这里先拦一道，省得在几百行 dpkg 输出里找原因。
MISSING_DEV=""
for pkg in qt6-webengine-dev qt6-multimedia-dev; do
    if ! dpkg-query -W -f='${Status}' "$pkg" 2>/dev/null | grep -q "install ok installed"; then
        MISSING_DEV="$MISSING_DEV $pkg"
    fi
done
if [ -n "$MISSING_DEV" ]; then
    cat >&2 <<EOF
缺少构建依赖：$MISSING_DEV

  这两个包不是可选项：网页小游戏和音频要编进二进制才生效，用户拿到包以后
  再装 QML 模块是补不回来的（只能重新编译整个应用）。

  先装：  sudo apt install$MISSING_DEV
  或者：  cmake -DKIDSSAFE_MINIMAL=ON   # 明确要一个不含这些功能的小包
EOF
    exit 1
fi

if [ ! -x debian/rules ]; then
    echo "==> debian/rules 没有执行位，补一下（从 Windows 拷过来会丢）"
    chmod +x debian/rules
fi
chmod +x scripts/build-deb.sh 2>/dev/null || true

# ---- 权限归一 ----
# debian/ 下除 rules 以外的文件必须**不可执行**。
# debhelper 把带执行位的 debian/*.docs / *.install / *.links 当成「可执行配置文件」
# 直接运行：kidssafe.docs 里只有一行 "README.md"，于是它去执行 README.md，
# 报 "README.md: not found"（退出码 127）并中断整个打包。
# 从 Windows / U 盘 / FAT 分区拷过来的文件经常个个都带 x 位，这里统一清掉。
stray="$(find debian -maxdepth 1 -type f ! -name rules -perm /111 2>/dev/null || true)"
if [ -n "$stray" ]; then
    echo "==> 清理 debian/ 下误带的执行位（debhelper 会把它当可执行配置去跑）"
    printf '%s\n' "$stray" | sed 's/^/    /'
    printf '%s\n' "$stray" | while IFS= read -r f; do
        if [ -n "$f" ]; then
            chmod -x "$f"
        fi
    done
fi

# ---- 版本同步 ----
# 版本号有两份：CMakeLists.txt 的 project(VERSION) 和 debian/changelog 的顶条。
# 两边不一致时 dpkg-buildpackage 不会报错，只会打出一个「版本对不上 CMake」的包，
# 以后升级时 apt 会按 debian 版本比较大小 —— 是那种最难查的 bug。
# 这里以 CMakeLists.txt 为准，自动补一条 changelog。
ver_cmake="$(sed -n 's/^project(kidssafe VERSION \([0-9][0-9.]*\).*/\1/p' CMakeLists.txt | head -n 1)"
if [ -z "$ver_cmake" ]; then
    echo "没能从 CMakeLists.txt 读出 project(VERSION)" >&2
    exit 1
fi

ver_deb="$(dpkg-parsechangelog -S Version 2>/dev/null || true)"
[ -n "$ver_deb" ] || ver_deb="$(sed -n '1s/^kidssafe (\([^)]*\)).*/\1/p' debian/changelog)"

if [ "$ver_cmake" != "$ver_deb" ]; then
    who="$(git config user.name 2>/dev/null || true)"
    mail="$(git config user.email 2>/dev/null || true)"
    [ -n "$who" ] || who="KidsSafe Desktop"
    [ -n "$mail" ] || mail="kidssafe@localhost"
    when="$(date -R)"

    echo "==> 版本不一致（CMakeLists=$ver_cmake / changelog=$ver_deb），补一条 changelog"
    tmp="$(mktemp)"
    {
        printf 'kidssafe (%s) unstable; urgency=medium\n\n' "$ver_cmake"
        printf '  * 与 CMakeLists.txt 的 VERSION 同步。\n\n'
        printf ' -- %s <%s>  %s\n\n' "$who" "$mail" "$when"
        cat debian/changelog
    } > "$tmp"
    mv "$tmp" debian/changelog
fi

# ---- 构建依赖自检 ----
# dpkg-buildpackage 自己也会查，但它的报错是一行裸的 "Unmet build dependencies"，
# 第一次看到容易以为是源码问题。这里单独查一次，把两种原因分开说清楚。
if ! deps_out="$(dpkg-checkbuilddeps 2>&1)"; then
    echo "$deps_out" >&2
    echo >&2
    echo "构建依赖没装齐。两种可能：" >&2
    echo "  · 工程真要用 → sudo apt install <上面列出的包>" >&2
    echo "  · apt 说「无法定位软件包」→ 这个发行版没有该包名，" >&2
    echo "    把 debian/control 里对应那行删掉（多半是 libxkbcommon-* 这类" >&2
    echo "    某个发行版上 Qt 顺带要的依赖，本工程代码里并没有用到）" >&2
    exit 1
fi

# ---- 构建 ----
# 上次的产物先清掉：下面按 glob 找「最新的 deb」，留旧的会拿错包。
rm -f ../kidssafe_*.deb ../kidssafe_*.buildinfo

echo "==> dpkg-buildpackage $ver_cmake"
#  -b      只出二进制包（不生成源码包，不需要 orig.tar）
#  -us -uc 不签名 .dsc/.changes（本机自用；进仓库前再 debsign）
#  -j      并行编译
dpkg-buildpackage -b -us -uc -j"$(nproc 2>/dev/null || echo 2)"

# ---- 收产物 ----
mkdir -p dist
mv ../kidssafe_*.deb ../kidssafe_*.buildinfo dist/ 2>/dev/null || true

deb="$(ls -1t dist/kidssafe_*.deb 2>/dev/null | head -n 1)"
if [ -z "$deb" ]; then
    echo "没找到生成的 deb，看看上面的构建日志" >&2
    exit 1
fi

echo
echo "==> 完成：$deb"
dpkg-deb -I "$deb" | sed -n '1,20p'
echo
echo "装机："
echo "  sudo apt install ./$deb"
echo "  （家目录对 _apt 用户不可读时，apt 会提示「已脱离沙盒并提权为根用户」——"
echo "    不影响安装结果；想去掉这条提示就先拷到 /tmp："
echo "      cp $deb /tmp/ && sudo apt install /tmp/$(basename "$deb") ）"
echo "卸载："
echo "  sudo apt remove kidssafe"
echo "注销后，在登录界面的会话菜单里选「儿童安全桌面（X11）」即可进入。"
