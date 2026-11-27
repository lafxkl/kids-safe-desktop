#!/bin/sh
#
# 打一个「自带运行时」的包：把 Qt、KF6、QtWebEngine（含 Chromium 的 helper
# 进程、ICU 数据、pak 资源、QML 模块）全部收进同一棵树里，用户机器上不需要
# 装任何 qt6-* 包。
#
#   ./scripts/bundle-runtime.sh              # 生成 dist/kidssafe-<ver>-linux-x86_64/
#   ./scripts/bundle-runtime.sh --deb        # 再打成 dist/kidssafe-bundled_<ver>_amd64.deb
#   ./scripts/bundle-runtime.sh --tgz        # 再打成一个 tar.gz（绿色版）
#   ./scripts/bundle-runtime.sh --verify     # 只体检已有的包
#
# 为什么需要它：
#   发「薄包」（debian/ 那套）时，网页小游戏靠系统里的 QtWebEngine。只要目标
#   机器没装 qt6-webengine-dev / qml6-module-qtwebengine，或者装的版本和 Qt
#   基础库对不上，孩子点开游戏乐园就只看到一行「模块未安装」。而这件事用户
#   自己补不了 —— QML 模块能装，编进二进制的初始化和宿主页装不进去。
#   所以这里换一条路：把构建机上的整套运行时原样搬走，和系统 Qt 彻底解耦。
#
# 代价（写清楚，不藏着）：
#   · 体积：解压后约 300MB，deb 约 120MB（压缩后）。薄包只有 3MB。
#   · Chromium 的 setuid 沙箱在用户可写的目录里无法工作，只能用
#     QTWEBENGINE_DISABLE_SANDBOX=1。内容是随包带的本地 HTML，不加载外部页面，
#     风险面有限，但这是真实的降级，不是「等价替代」。
#   · glibc / libstdc++ / 显卡驱动（libGL、libEGL）**不打包**：它们必须和用户
#     内核、驱动匹配，搬过去只会更坏。
#
# 依赖的构建机工具：cmake、patchelf（可选，缺了照样能用，只是少一层保险）、
# dpkg-deb（只在 --deb 时需要）。
#
# 关于仓库里已有的 debian/ 打包：那个产出的是**薄包**，依赖系统 Qt，适合
# 进发行版仓库。这里产出的是**自带包**，适合丢给家长双击安装。两者包名不同
# （kidssafe / kidssafe-bundled），互不冲突，也不要同时装。

set -eu

SRC_DIR="$(cd "$(dirname "$0")/.." && pwd)"
cd "$SRC_DIR"

DO_DEB=0
DO_TGZ=0
DO_VERIFY=0
BUILD_DIR=""
for arg in "$@"; do
    case "$arg" in
        --deb)     DO_DEB=1 ;;
        --tgz)     DO_TGZ=1 ;;
        --verify)  DO_VERIFY=1 ;;
        --build=*) BUILD_DIR="${arg#--build=}" ;;
        -h|--help)
            sed -n '2,30p' "$0" | sed 's/^# \{0,1\}//'
            exit 0 ;;
        *)
            echo "未知参数：$arg（--deb / --tgz / --verify / --build=DIR）" >&2
            exit 2 ;;
    esac
done

say()  { printf '\033[1;36m==>\033[0m %s\n' "$*"; }
warn() { printf '\033[1;33m[!]\033[0m %s\n' "$*" >&2; }
die()  { printf '\033[1;31m[x]\033[0m %s\n' "$*" >&2; exit 1; }

VERSION="$(sed -n 's/^project(kidssafe VERSION \([0-9.]*\).*/\1/p' CMakeLists.txt | head -n1)"
[ -n "$VERSION" ] || die "读不到 CMakeLists.txt 里的版本号"

MACHINE="$(uname -m)"
if command -v dpkg >/dev/null 2>&1 && dpkg --print-architecture >/dev/null 2>&1; then
    ARCH="$(dpkg --print-architecture)"
else
    # 没有 dpkg 的发行版（Fedora / Arch）上给个合理的猜测，仅供 --deb 用
    case "$MACHINE" in
        x86_64)  ARCH=amd64 ;;
        aarch64) ARCH=arm64 ;;
        *)       ARCH="$MACHINE" ;;
    esac
fi
NAME="kidssafe-$VERSION-linux-$MACHINE"
ROOT="$SRC_DIR/dist/$NAME"

# ── 0. 只体检 ────────────────────────────────────────────────────────────
if [ "$DO_VERIFY" -eq 1 ]; then
    [ -d "$ROOT" ] || die "还没有包：先跑一次不带 --verify 的本脚本"
    exec sh "$ROOT/kidssafe.sh" --self-test
fi

# ── 1. 定位构建产物 ──────────────────────────────────────────────────────
if [ -z "$BUILD_DIR" ]; then
    for cand in build build-release; do
        if [ -x "$cand/kidssafe" ]; then BUILD_DIR="$cand"; break; fi
    done
fi
if [ -z "$BUILD_DIR" ] || [ ! -x "$BUILD_DIR/kidssafe" ]; then
    say "没有现成的构建产物，先编译"
    BUILD_DIR="build-bundle"
    cmake -S "$SRC_DIR" -B "$BUILD_DIR" -DCMAKE_BUILD_TYPE=Release >/dev/null
    cmake --build "$BUILD_DIR" --parallel "$(nproc)"
fi
BIN="$BUILD_DIR/kidssafe"
[ -x "$BIN" ] || die "找不到可执行文件：$BIN"

# 自带包的前提是二进制里真的有 WebEngine。没有的话打出来的「自带包」也是残的，
# 而且事后补不了 —— 直接停下来说清楚。
# 用 grep -a 而不是 strings：binutils 精简安装里不一定有 strings，而这个特征串
# 在 .rodata 里一定找得到（环境变量名会被编进去）。
if ! grep -qa "QTWEBENGINEPROCESS_PATH" "$BIN"; then
    warn "这个二进制里没有 QtWebEngine（用了 -DKIDSSAFE_MINIMAL=ON？）"
    warn "自带包的意义就是把网页小游戏一起带走，先装 qt6-webengine-dev 重新编译"
    die  "或者改用 ./scripts/build-deb.sh 打薄包"
fi

# ── 2. 找 Qt 的安装路径 ─────────────────────────────────────────────────
qpath() {
    # $1 = qtpaths6 的查询键，例如 QT_INSTALL_QML
    if command -v qtpaths6 >/dev/null 2>&1; then
        qtpaths6 --query "$1" 2>/dev/null && return 0
    fi
    if command -v qmake6 >/dev/null 2>&1; then
        qmake6 -query "$1" 2>/dev/null && return 0
    fi
    return 1
}

QT_LIBS="$(qpath QT_INSTALL_LIBS || true)"
QT_PLUGINS="$(qpath QT_INSTALL_PLUGINS || true)"
QT_QML="$(qpath QT_INSTALL_QML || true)"
QT_LIBEXECS="$(qpath QT_INSTALL_LIBEXECS || true)"
QT_DATA="$(qpath QT_INSTALL_DATA || true)"
QT_TRANS="$(qpath QT_INSTALL_TRANSLATIONS || true)"

for pair in "LIBS:$QT_LIBS" "PLUGINS:$QT_PLUGINS" "QML:$QT_QML"; do
    [ -d "${pair#*:}" ] || die "qtpaths6 查不到 ${pair%%:*}（装 qt6-base-dev-tools / qt6-declarative-dev-tools？）"
done

say "Qt 安装位置：$QT_LIBS"
say "版本 $VERSION，构建目录 $BUILD_DIR"

# ── 3. 建目录 ───────────────────────────────────────────────────────────
rm -rf "$ROOT"
mkdir -p "$ROOT/bin" "$ROOT/lib" "$ROOT/lib/qt6/plugins" "$ROOT/lib/qt6/qml" \
         "$ROOT/libexec" "$ROOT/resources" "$ROOT/translations" \
         "$ROOT/share/kidssafe" "$ROOT/share/icons/hicolor/scalable/apps"

cp "$BIN" "$ROOT/bin/kidssafe"
chmod +x "$ROOT/bin/kidssafe"

# ── 4. 收集 ELF 依赖 ────────────────────────────────────────────────────
#
# 刻意不打包的系统库：
#   ld-linux / libc / libm / libpthread / libdl / librt —— glibc 的一部分，
#     换掉就是「两个 libc 在一个进程里」，必崩；
#   libstdc++ / libgcc_s —— 系统那份足够新即可，包一份老的反而限制别人；
#   libGL / libEGL / libGLX / libdrm —— 和显卡驱动绑定，搬过去等于替用户
#     选驱动。
# 其余（Qt、KF6、Chromium 依赖的 nss / 字体 / X11 客户端库等）全部带走，
# 这正是「不依赖系统装了哪些 Qt」的关键。
#
# 用 case 而不是正则：POSIX sh 的 case 只认 glob，把 | 写成正则的分组反而
# 一个都匹配不上，结果就是把 libc 也抄进包里 —— 那种包一启动就段错误。
skip_lib() {
    case "$1" in
        ld-linux*|ld64.so*)                                          return 0 ;;
        libc.so*|libc-*.so|libm.so*|libmvec.so*|libpthread.so*)      return 0 ;;
        libdl.so*|librt.so*|libutil.so*|libanl.so*|libresolv.so*)    return 0 ;;
        libstdc++.so*|libgcc_s.so*)                                  return 0 ;;
        libGL.so*|libEGL.so*|libGLX.so*|libGLdispatch.so*)           return 0 ;;
        libdrm.so*|libdrm_*.so*)                                     return 0 ;;
    esac
    return 1
}

SKIP_TOOL=""
if ! command -v patchelf >/dev/null 2>&1; then
    warn "没有 patchelf：RPATH 不会改，纯靠启动器里的 LD_LIBRARY_PATH 兜底"
    warn "想更稳：sudo apt install patchelf"
    SKIP_TOOL=1
fi

SEEN="$ROOT/.seen"
QUEUE="$ROOT/.queue"
: > "$SEEN"
: > "$QUEUE"

enqueue() {
    for f in "$@"; do
        [ -f "$f" ] || continue
        grep -qxF "$f" "$SEEN" 2>/dev/null && continue
        printf '%s\n' "$f" >> "$SEEN"
        printf '%s\n' "$f" >> "$QUEUE"
    done
}

# 把 $1 的依赖抄进 lib/，并把新抄进来的继续排队（广度优先，避免递归爆栈）
collect() {
    for elf in $(printf '%s\n' "$@"); do
        [ -f "$elf" ] || continue
        ldd "$elf" 2>/dev/null | while read -r line; do
            case "$line" in
                *"=>"*) dep="$(printf '%s' "$line" | awk '{print $3}')" ;;
                /*)     dep="$(printf '%s' "$line" | awk '{print $1}')" ;;
                *)      continue ;;
            esac
            case "$dep" in
                ''|'not'*|*'=>') continue ;;
            esac
            base="$(basename "$dep")"
            skip_lib "$base" && continue
            # 已经在包里的同 soname 不再重复抄（不同路径同名的取先到的）
            if [ -e "$ROOT/lib/$base" ]; then continue; fi
            cp -L "$dep" "$ROOT/lib/$base"
            printf '%s\n' "$dep" >> "$ROOT/.new"
        done
    done
}

: > "$ROOT/.new"
enqueue "$ROOT/bin/kidssafe"
while [ -s "$QUEUE" ]; do
    head -n 1 "$QUEUE" > "$ROOT/.cur"
    sed -i '1d' "$QUEUE"
    : > "$ROOT/.new"
    collect "$(cat "$ROOT/.cur")"
    if [ -s "$ROOT/.new" ]; then
        enqueue $(cat "$ROOT/.new")
    fi
done
rm -f "$QUEUE" "$ROOT/.cur" "$ROOT/.new" "$SEEN"

say "依赖库：$(ls "$ROOT/lib" | wc -l) 个，$(du -sh "$ROOT/lib" | cut -f1)"

# ── 5. Qt 插件 ─────────────────────────────────────────────────────────
#
# 平台插件是硬需求（没有 libqxcb.so 就起不来）；tls 插件决定 HTTPS 能不能用
# （故事服务端是 https）；platformthemes 让对话框长得像系统；wayland 那几个
# 只在 Wayland 会话里用到，带上以防用户切换会话。
PLUGIN_DIRS="platforms platforminputcontexts platformthemes imageformats iconengines tls \
xcbglintegrations egldeviceintegrations \
wayland-decoration-client wayland-graphics-integration-client wayland-shell-integration"

for d in $PLUGIN_DIRS; do
    [ -d "$QT_PLUGINS/$d" ] || continue
    mkdir -p "$ROOT/lib/qt6/plugins/$d"
    cp -a "$QT_PLUGINS/$d/." "$ROOT/lib/qt6/plugins/$d/"
done

# 插件自己也依赖 Qt 库，必须再把它们的依赖过一遍（缺了会在启动时报
# "Cannot load library ... libqxcb.so: cannot open shared object"）
: > "$ROOT/.new"
collect $(find "$ROOT/lib/qt6/plugins" -name '*.so' -type f)
if [ -s "$ROOT/.new" ]; then
    while read -r extra; do
        base="$(basename "$extra")"
        skip_lib "$base" && continue
        [ -e "$ROOT/lib/$base" ] && continue
        cp -L "$extra" "$ROOT/lib/$base"
    done < "$ROOT/.new"
fi
rm -f "$ROOT/.new"

# ── 6. QML 模块 ────────────────────────────────────────────────────────
#
# 只抄用得上的那些。整个 qml/ 目录三百多兆，里面有 QtQuick3D、Charts、
# VirtualKeyboard 之类本项目一行都没 import 的东西。
#
# 值得单独说一句 org/kde/breeze：Qt 6 的 QtQuick Controls 找不到样式模块时
# 会在启动阶段直接报 "No style available"，而 Breeze 是 KDE 下的默认样式，
# 也是本工程界面看起来「不像毛坯 Qt」的原因。漏抄它，界面会退成 Fusion。
QML_MODULES="QtQuick QtQml QtWebEngine QtWebChannel QtPositioning QtSensors \
QtLabsPlatform org/kde/kirigami org/kde/breeze org/kde/qqc2desktopstyle"
for m in $QML_MODULES; do
    [ -d "$QT_QML/$m" ] || continue
    mkdir -p "$ROOT/lib/qt6/qml/$(dirname "$m")"
    cp -a "$QT_QML/$m" "$ROOT/lib/qt6/qml/$(dirname "$m")/"
done

# 控件样式：QtQuick/Controls 下按样式分目录，Breeze 在 org/kde/breeze。
# 一个都没抄进来的话，QML 会在启动时报 "No style available"。
if [ ! -d "$ROOT/lib/qt6/qml/QtQuick/Controls" ]; then
    die "没抄到 QtQuick/Controls —— QML 起不来，检查 $QT_QML 是否正确"
fi

say "QML 模块：$(find "$ROOT/lib/qt6/qml" -maxdepth 2 -mindepth 1 -type d | wc -l) 个"

# ── 7. QtWebEngine 的「运行期三件套」 ───────────────────────────────────
#
# 这三样缺任何一样，症状都是「网页区域全白、日志里只有一行
# Could not find QtWebEngineProcess」—— 而且它们不在 lib/ 里，ldd 抄不到，
# 必须单独搬。这也是自带包最容易漏的地方。
WEBPROC=""
for cand in "$QT_LIBEXECS/QtWebEngineProcess" "$QT_LIBS/QtWebEngineProcess" \
            "$QT_LIBS/qt6/libexec/QtWebEngineProcess"; do
    if [ -x "$cand" ]; then WEBPROC="$cand"; break; fi
done
[ -n "$WEBPROC" ] || die "找不到 QtWebEngineProcess（qt6-webengine-dev 装了吗？）"
cp "$WEBPROC" "$ROOT/libexec/QtWebEngineProcess"
chmod +x "$ROOT/libexec/QtWebEngineProcess"

RES_DIR=""
for cand in "$QT_DATA/resources" "$QT_QML/../resources" "$QT_LIBS/qt6/resources"; do
    if [ -f "$cand/icudtl.dat" ]; then RES_DIR="$cand"; break; fi
done
[ -n "$RES_DIR" ] || die "找不到 WebEngine 资源目录（icudtl.dat / *.pak）"
cp -a "$RES_DIR/." "$ROOT/resources/"

LOCALES=""
for cand in "$QT_TRANS/qtwebengine_locales" "$QT_DATA/translations/qtwebengine_locales"; do
    if [ -d "$cand" ]; then LOCALES="$cand"; break; fi
done
if [ -n "$LOCALES" ]; then
    mkdir -p "$ROOT/translations/qtwebengine_locales"
    # 只带中文和英文：一个 locale 一两兆，一百多个全带上纯属浪费。
    # 缺的 locale Chromium 会退回 en-US，不会崩。
    for loc in zh-CN zh-TW en-US; do
        # 用 if 而不是 `[ -f ] && cp`：后者的写法在最后一个 locale 缺失时会让
        # for 返回非零，set -e 直接把脚本带走 —— 而「少一个语言包」根本不该
        # 中断打包。
        if [ -f "$LOCALES/$loc.pak" ]; then
            cp "$LOCALES/$loc.pak" "$ROOT/translations/qtwebengine_locales/"
        fi
    done
else
    warn "没找到 qtwebengine_locales：界面仍能显示，只是 Chromium 的右键菜单等提示是英文"
fi

say "WebEngine：helper + $(ls "$ROOT/resources" | wc -l) 个资源文件"

# ── 8. 应用自己的资源 ───────────────────────────────────────────────────
for d in playland maker codelab audio; do
    [ -d "assets/$d" ] || continue
    cp -a "assets/$d" "$ROOT/share/kidssafe/"
done
cp "assets/kidssafe.svg" "$ROOT/share/icons/hicolor/scalable/apps/kidssafe.svg"

# ── 9. 改 RPATH ────────────────────────────────────────────────────────
#
# --force-rpath 很关键：默认写的是 DT_RUNPATH，它**不传递给依赖的依赖**。
# 用 DT_RPATH（老式，可传递）才能让 libKF6CoreAddons.so 去找
# libQt6Core.so.6 时也落在我们自己的 lib/ 里。这一步没做对的表现是：
# 程序起得来，但偷偷加载了系统 Qt，版本一混就崩。
if [ -z "$SKIP_TOOL" ]; then
    patchelf --force-rpath --set-rpath '$ORIGIN:$ORIGIN/../lib' "$ROOT/bin/kidssafe"
    for so in "$ROOT"/lib/*.so*; do
        [ -f "$so" ] || continue
        patchelf --force-rpath --set-rpath '$ORIGIN' "$so" 2>/dev/null || \
            warn "patchelf 失败：$(basename "$so")"
    done
    for so in $(find "$ROOT/lib/qt6/plugins" -name '*.so'); do
        patchelf --force-rpath --set-rpath '$ORIGIN:$ORIGIN/../../..' "$so" 2>/dev/null || true
    done
    # helper 进程是独立的可执行文件，不跟着主程序的 RPATH 走
    patchelf --force-rpath --set-rpath '$ORIGIN:$ORIGIN/../lib' \
        "$ROOT/libexec/QtWebEngineProcess" 2>/dev/null || \
        warn "patchelf 失败：QtWebEngineProcess"
fi

# ── 10. 启动器 ─────────────────────────────────────────────────────────
cat > "$ROOT/kidssafe.sh" <<'LAUNCHER'
#!/bin/sh
#
# 自带运行时的启动器。由 scripts/bundle-runtime.sh 生成，改它没用 —— 改脚本。
#
# 这里设的每个变量都对应一个「不设就白屏/起不来」的具体故障，见脚本注释。
set -u

HERE="$(cd "$(dirname "$0")" && pwd)"

# Qt 库、插件、QML：让 Qt 只认包里的这一套，不碰 /usr/lib 里的同名库
LD_LIBRARY_PATH="$HERE/lib${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
QT_PLUGIN_PATH="$HERE/lib/qt6/plugins"
QML_IMPORT_PATH="$HERE/lib/qt6/qml"
QML2_IMPORT_PATH="$QML_IMPORT_PATH"

# WebEngine 的三件套（helper 进程 / ICU 与 pak 资源 / 语言包）。
# 不设这三个，Qt 会去编译时写死的系统路径找，找不到就是纯白页。
QTWEBENGINEPROCESS_PATH="$HERE/libexec/QtWebEngineProcess"
QTWEBENGINE_RESOURCES_PATH="$HERE/resources"
QTWEBENGINE_LOCALES_PATH="$HERE/translations/qtwebengine_locales"

# Chromium 的 setuid 沙箱要求 chrome-sandbox 属于 root 且带 4755 位。
# 包安装在用户可写/可移动的位置，做不到这一点，所以只能关掉沙箱。
# 换来的是「装上就能用、不用 root 改权限」，代价见脚本头部说明。
QTWEBENGINE_DISABLE_SANDBOX=1

# 应用自己的资源目录（playland / maker / codelab / audio）：程序会先看
# <bindir>/../share/kidssafe，这里再显式给一份，免得有人把 bin/ 单独拷走。
XDG_DATA_DIRS="$HERE/share:${XDG_DATA_DIRS:-/usr/local/share:/usr/share}"

export LD_LIBRARY_PATH QT_PLUGIN_PATH QML_IMPORT_PATH QML2_IMPORT_PATH \
       QTWEBENGINEPROCESS_PATH QTWEBENGINE_RESOURCES_PATH \
       QTWEBENGINE_LOCALES_PATH QTWEBENGINE_DISABLE_SANDBOX XDG_DATA_DIRS

exec "$HERE/bin/kidssafe" "$@"
LAUNCHER
chmod +x "$ROOT/kidssafe.sh"

# ── 11. 说明文件 ───────────────────────────────────────────────────────
cat > "$ROOT/README.txt" <<EOF
儿童安全桌面 $VERSION · 自带运行时（linux/x86_64）

直接运行：
    ./kidssafe.sh              # 全屏（kiosk）
    ./kidssafe.sh --windowed   # 窗口模式，调试用
    ./kidssafe.sh --self-test  # 只做环境自检，不开界面

这个目录是自包含的：Qt、KF6、QtWebEngine 全在里面（$VERSION 打包时从构建机
搬过来的），不需要系统装任何 qt6-* 包。唯一需要的是系统基础库和显卡驱动。

装到系统里（进登录界面的会话菜单）：
    sudo ./scripts/bundle-runtime.sh --deb
    sudo apt install ./dist/kidssafe-bundled_${VERSION}_${ARCH}.deb

已知取舍：
  · Chromium 沙箱关闭（QTWEBENGINE_DISABLE_SANDBOX=1）。原因是 setuid 沙箱
    不能在用户可写的目录里工作。网页内容随包提供，不加载外部页面。
  · 只带 zh-CN / zh-TW / en-US 三种 Chromium 语言包。
EOF

say "目录就绪：$ROOT（$(du -sh "$ROOT" | cut -f1)）"

# ── 12. 体检 ───────────────────────────────────────────────────────────
#
# 不体检就发出去，最常见的结果是「在我这儿好好的」——因为构建机上系统装得
# 全，缺的东西被系统的库悄悄补上了。所以这里查两件事：
#   1. 包里每个 ELF 的 Qt/KF6 依赖是否都落在包里（而不是 /usr/lib）；
#   2. 用 runner 真跑一次 --self-test。
if [ -z "$SKIP_TOOL" ]; then
    say "检查依赖是否漏抄"
    LEAK=0
    for so in "$ROOT"/lib/*.so* "$ROOT/bin/kidssafe"; do
        [ -f "$so" ] || continue
        missing="$(patchelf --print-needed "$so" 2>/dev/null | while read -r need; do
            [ -e "$ROOT/lib/$need" ] || [ -e "$ROOT/bin/$need" ] || echo "$need"
        done)"
        if [ -n "$missing" ]; then
            # 系统库（glibc / 驱动）本来就不在包里，只报 Qt 与 KF6
            echo "$missing" | grep -E '^libQt6|^libKF6' | while read -r m; do
                warn "漏抄：$(basename "$so") 需要 $m"
            done
            if echo "$missing" | grep -qE '^libQt6|^libKF6'; then LEAK=1; fi
        fi
    done
    [ "$LEAK" -eq 0 ] || die "有 Qt/KF6 库没抄进来，包不可用（见上面的警告）"

    # 最后确认一次「真的没在用系统 Qt」。ldd 会按 RPATH 解析，所以如果这里
    # 出来的是 /usr/lib 下的路径，说明 RPATH 没写对 —— 那种包在构建机上看着
    # 好好的，到了用户机器就缺库。
    CORE_LINE="$(ldd "$ROOT/bin/kidssafe" 2>/dev/null | grep 'libQt6Core' | head -n1 || true)"
    case "$CORE_LINE" in
        '') warn "ldd 里看不到 libQt6Core，检查构建产物是否正常" ;;
        *"$ROOT"*) : ;;
        *)  warn "libQt6Core 解析到了包外：$CORE_LINE" ;;
    esac
fi

say "跑一次自检（不需要显示器）"
if sh "$ROOT/kidssafe.sh" --self-test > "$ROOT/.selftest.log" 2>&1; then
    grep -E "网页小游戏|故事服务器|小小程序员" "$ROOT/.selftest.log" || true
    rm -f "$ROOT/.selftest.log"
    say "自检通过"
else
    warn "自检没过，日志留在 $ROOT/.selftest.log"
    tail -20 "$ROOT/.selftest.log" >&2 || true
    die "先修好再打包"
fi

# ── 13. 打 tar.gz / deb ────────────────────────────────────────────────
if [ "$DO_TGZ" -eq 1 ]; then
    TGZ="$SRC_DIR/dist/$NAME.tar.gz"
    say "打包 $TGZ"
    tar -C "$SRC_DIR/dist" -czf "$TGZ" "$NAME"
    say "完成：$TGZ（$(du -h "$TGZ" | cut -f1)）"
fi

if [ "$DO_DEB" -eq 1 ]; then
    command -v dpkg-deb >/dev/null 2>&1 || die "没有 dpkg-deb：sudo apt install dpkg"
    command -v fakeroot >/dev/null 2>&1 || die "没有 fakeroot：sudo apt install fakeroot"

    PKGROOT="$SRC_DIR/dist/.deb-root"
    rm -rf "$PKGROOT"
    mkdir -p "$PKGROOT/opt" "$PKGROOT/usr/bin" \
             "$PKGROOT/usr/share/xsessions" "$PKGROOT/usr/share/wayland-sessions" \
             "$PKGROOT/usr/share/icons/hicolor/scalable/apps" \
             "$PKGROOT/DEBIAN"

    say "组装 deb 内容"
    cp -a "$ROOT" "$PKGROOT/opt/kidssafe"

    # /usr/bin/kidssafe 是个转发器，不是二进制本体：会话脚本（kidssafe-session）
    # 只认这个路径，真正的程序和 Qt 都在 /opt 里。
    cat > "$PKGROOT/usr/bin/kidssafe" <<'WRAP'
#!/bin/sh
# 转发到 /opt/kidssafe 里的自带运行时。由 bundle-runtime.sh 生成。
exec /opt/kidssafe/kidssafe.sh "$@"
WRAP
    chmod 755 "$PKGROOT/usr/bin/kidssafe"

    # 会话脚本原样带过来：它们自己会按顺序找 /usr/local/bin、/usr/bin。
    for f in kidssafe-session kidssafe-wayland-session; do
        install -m 755 "session/$f" "$PKGROOT/usr/bin/$f"
    done
    install -m 644 session/kidssafe.desktop "$PKGROOT/usr/share/xsessions/kidssafe.desktop"
    install -m 644 session/kidssafe-wayland.desktop \
        "$PKGROOT/usr/share/wayland-sessions/kidssafe-wayland.desktop"
    install -m 644 assets/kidssafe.svg \
        "$PKGROOT/usr/share/icons/hicolor/scalable/apps/kidssafe.svg"

    # Depends 只留真正的系统底层：glibc、C++ 运行时、OpenGL 装载器、字体、
    # dbus、xkb。其余（Qt / KF6 / WebEngine / nss / fontconfig 的客户端库）
    # 都在 /opt 里。写多了会在精简系统上装不上，写少了会在启动时报缺库 ——
    # 所以宁可少写几个，第一句话就把「缺什么装什么」写进说明。
    cat > "$PKGROOT/DEBIAN/control" <<EOF
Package: kidssafe-bundled
Version: $VERSION
Section: x11
Priority: optional
Architecture: $ARCH
Depends: libc6 (>= 2.34), libstdc++6, libgl1, libx11-6, libfontconfig1,
 libfreetype6, libdbus-1-3, libxkbcommon0, libglib2.0-0
Recommends: fonts-noto-cjk, libasound2t64 | libasound2
Conflicts: kidssafe
Replaces: kidssafe
Maintainer: KidsSafeDesktop <kidssafe@localhost>
Description: 儿童安全桌面（自带 Qt / QtWebEngine 运行时）
 给小朋友的专属桌面会话：故事屋、安全世界、游戏乐园（含 15 款网页小游戏）、
 儿童桌面（文件管理器 / 终端 / 任务管理器）。
 .
 与 kidssafe 包的区别：本包把 Qt 6、KF6 和 QtWebEngine 全部装在 /opt/kidssafe
 里，不依赖系统里的任何 qt6-* 包，适合离线机器、精简系统、以及发行版源里
 没有 qt6-webengine 的情况。代价是体积大（约 120MB）。
 .
 同时装了系统 Qt 的 kidssafe 也可以，但两个包互斥（Conflicts），不要同时装。
EOF

    cat > "$PKGROOT/DEBIAN/postinst" <<'POSTINST'
#!/bin/sh
set -e
if command -v update-desktop-database >/dev/null 2>&1; then
    update-desktop-database -q /usr/share/xsessions 2>/dev/null || true
fi
if command -v gtk-update-icon-cache >/dev/null 2>&1; then
    gtk-update-icon-cache -qtf /usr/share/icons/hicolor 2>/dev/null || true
fi
echo "儿童安全桌面已安装：注销后，在登录界面的会话菜单里选「儿童安全桌面」。"
exit 0
POSTINST
    chmod 755 "$PKGROOT/DEBIAN/postinst"

    DEB="$SRC_DIR/dist/kidssafe-bundled_${VERSION}_${ARCH}.deb"
    rm -f "$DEB"
    say "打包 $DEB"
    # --root-owner-group：不需要 fakeroot 也能让包里的文件属主是 root。
    if dpkg-deb --help 2>/dev/null | grep -q -- '--root-owner-group'; then
        dpkg-deb -Zxz --root-owner-group -b "$PKGROOT" "$DEB" >/dev/null
    else
        fakeroot dpkg-deb -Zxz -b "$PKGROOT" "$DEB" >/dev/null
    fi
    rm -rf "$PKGROOT"
    say "完成：$DEB（$(du -h "$DEB" | cut -f1)）"
    echo
    echo "  安装：sudo apt install $DEB"
    echo "  验证：/opt/kidssafe/kidssafe.sh --self-test"
fi

say "全部完成"
echo "  目录：$ROOT"
echo "  启动：$ROOT/kidssafe.sh --windowed"
