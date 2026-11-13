#!/bin/sh
#
# 在目标 Linux 机器上跑这个脚本，检查「儿童安全桌面会话」能不能正常拉起。
#
# 用法：  ./scripts/check-host.sh
# 退出码：0 = 全部通过；1 = 有致命项失败
#
# 设计原则：只读、不修改系统。所有检查都是「能读到 / 能执行」，
# 不会替你改配置；需要改的地方会打印出建议命令。

set -u

PASS=0
FAIL=0

ok()   { printf '  \033[32m✓\033[0m %s\n' "$1"; PASS=$((PASS + 1)); }
bad()  { printf '  \033[31m✗\033[0m %s\n' "$1"; FAIL=$((FAIL + 1)); }
warn() { printf '  \033[33m!\033[0m %s\n' "$1"; }
head_() { printf '\n\033[1m%s\033[0m\n' "$1"; }

printf '\n儿童安全桌面 · 宿主环境检查\n'
printf '系统: %s\n' "$(uname -srm)"
printf '会话: %s\n' "${XDG_SESSION_TYPE:-未知}"

# ── 1. 可执行文件 ────────────────────────────────────────────────
head_ "1. 程序"

APP="$(command -v kidssafe 2>/dev/null || true)"
if [ -n "$APP" ]; then
    ok "kidssafe 已安装：$APP"
else
    bad "找不到 kidssafe，先跑 ./scripts/install.sh"
fi

for s in kidssafe-session kidssafe-wayland-session; do
    if command -v "$s" >/dev/null 2>&1; then
        ok "$s 已安装"
    else
        bad "$s 缺失（会话拉不起来）"
    fi
done

# ── 2. 会话入口 ──────────────────────────────────────────────────
head_ "2. 显示管理器能看到的会话入口"

for f in /usr/share/xsessions/kidssafe.desktop /usr/local/share/xsessions/kidssafe.desktop; do
    [ -f "$f" ] && ok "X11 会话入口：$f" && break
done
[ -f /usr/share/xsessions/kidssafe.desktop ] || \
[ -f /usr/local/share/xsessions/kidssafe.desktop ] || bad "没有 X11 会话入口文件"

for f in /usr/share/wayland-sessions/kidssafe.desktop /usr/local/share/wayland-sessions/kidssafe.desktop; do
    [ -f "$f" ] && ok "Wayland 会话入口：$f" && break
done
if [ ! -f /usr/share/wayland-sessions/kidssafe.desktop ] && \
   [ ! -f /usr/local/share/wayland-sessions/kidssafe.desktop ]; then
    warn "没有 Wayland 会话入口（只用 X11 会话的话可以忽略）"
fi

# ── 3. 依赖库与 QML 模块 ─────────────────────────────────────────
head_ "3. 运行时依赖"

QMLDIR="$(qtpaths6 --query QT_INSTALL_QML 2>/dev/null || qtpaths --query QT_INSTALL_QML 2>/dev/null || echo /usr/lib/qt6/qml)"
if [ -d "$QMLDIR/org/kde/kirigami" ]; then
    ok "Kirigami QML 模块：$QMLDIR/org/kde/kirigami"
else
    bad "缺少 Kirigami QML 模块（libkf6kirigami-dev / kf6-kirigami）"
fi

if [ -d "$QMLDIR/QtQuick/Controls" ] || [ -d "$QMLDIR/QtQuick/Controls/Basic" ]; then
    ok "QtQuick.Controls 就绪"
else
    bad "缺少 QtQuick.Controls（qml6-module-qtquick-controls）"
fi

if ldconfig -p 2>/dev/null | grep -q 'libKF6Service'; then
    ok "libKF6Service 已加载（.desktop 枚举依赖它）"
else
    warn "ldconfig 里没看到 libKF6Service，应用启动时可能报错，用 ldd 检查"
fi

# ── 4. 合成器（Wayland 会话必需）────────────────────────────────
head_ "4. Wayland 合成器"

if command -v kwin_wayland >/dev/null 2>&1; then
    ok "kwin_wayland 可用（推荐：与系统观感一致）"
elif command -v cage >/dev/null 2>&1; then
    warn "只有 cage 可用：能锁死，但没有 KDE 外观"
else
    bad "kwin_wayland 与 cage 都没有，Wayland 会话无法启动"
fi

# ── 5. 语音与防休眠 ──────────────────────────────────────────────
head_ "5. 辅助能力（缺失只降级，不阻断）"

if command -v spd-say >/dev/null 2>&1; then
    ok "spd-say 可用（故事朗读 / 语音提示）"
    if spd-say -l zh "测试" >/dev/null 2>&1; then
        ok "中文语音包可用"
    else
        warn "中文语音可能缺失：sudo apt install speech-dispatcher-espeak-ng"
    fi
else
    warn "没有 spd-say，朗读会静默降级（speech-dispatcher）"
fi

if command -v xset >/dev/null 2>&1; then
    ok "xset 可用（X11 下关屏保）"
else
    warn "没有 xset，X11 会话下屏幕可能自动黑屏"
fi

# ── 6. 应用清单 ──────────────────────────────────────────────────
head_ "6. 可加入白名单的桌面应用"

COUNT=$(find /usr/share/applications ~/.local/share/applications \
        -maxdepth 1 -name '*.desktop' 2>/dev/null | wc -l | tr -d ' ')
if [ "$COUNT" -gt 0 ]; then
    ok "找到 $COUNT 个 .desktop 条目"
else
    bad "一个 .desktop 都没有，家长后台会是空的"
fi

# ── 7. 应用自检 ──────────────────────────────────────────────────
head_ "7. 应用自检"

if [ -n "$APP" ]; then
    # 注意：不能写成 if cmd | sed ...，管道的退出码取的是 sed，
    # 那样无论应用自检结果如何都会「通过」。必须先存状态再打印。
    SELF_OUT="$("$APP" --self-test 2>&1)"
    SELF_RC=$?
    printf '%s\n' "$SELF_OUT" | sed 's/^/    /'
    if [ "$SELF_RC" -eq 0 ]; then
        ok "kidssafe --self-test 通过"
    else
        bad "kidssafe --self-test 失败，看上面的输出"
    fi
else
    warn "跳过（程序未安装）"
fi

# ── 结论 ────────────────────────────────────────────────────────
head_ "结论"
printf '  通过 %d 项，失败 %d 项\n\n' "$PASS" "$FAIL"

if [ "$FAIL" -gt 0 ]; then
    printf '  有致命项未通过，先修完再验证会话。\n\n'
    exit 1
fi

cat <<'EOF'
  环境检查全过，接下来验证真正的锁定效果（这一步脚本代替不了）：

    1. 注销当前会话
    2. 登录界面的会话下拉里选「儿童安全桌面」
    3. 进去后逐项确认：
         □ 全屏、没有任务栏、没有面板
         □ Alt+Tab / Super 键切不出去
         □ Ctrl+Alt+T 打不开终端
         □ 在终端里 kill 掉 kidssafe，1 秒后自己回来
         □ 家长后台点「退出」，回到登录界面而不是又弹回来

  第 4、5 项是这套方案和 Windows 版最大的差别，务必单独验一遍。

EOF
exit 0
