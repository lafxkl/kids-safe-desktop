#!/usr/bin/env bash
#
# 把故事服务端装成「用户级 systemd 服务」。
#
#   ./install-server.sh              # 装到 ~/.local/share/kidssafe-story，开机自启
#   PORT=9000 ./install-server.sh    # 换端口
#   ./install-server.sh --uninstall  # 停服务并删除程序（保留内容目录）
#
# 为什么用用户级服务而不是系统级：服务只读本地内容文件、只监听局域网，
# 不需要 root。孩子桌面也是以同一用户登录运行的，用户级服务随登录启动即可，
# 家长不需要 sudo，也不会在系统里留下一个以 root 跑的网络进程。

set -eu

HERE="$(cd "$(dirname "$0")" && pwd)"
PREFIX="${PREFIX:-$HOME/.local/share/kidssafe-story}"
UNIT_DIR="$HOME/.config/systemd/user"
UNIT_NAME="kidssafe-story.service"
PORT="${PORT:-8080}"

say() { printf '%s\n' "$*"; }
die() { printf '错误：%s\n' "$*" >&2; exit 1; }

# ── 卸载 ────────────────────────────────────────────────────────

if [ "${1:-}" = "--uninstall" ]; then
    if command -v systemctl >/dev/null 2>&1; then
        systemctl --user disable --now "$UNIT_NAME" 2>/dev/null || true
    fi
    rm -f "$UNIT_DIR/$UNIT_NAME"
    command -v systemctl >/dev/null 2>&1 && systemctl --user daemon-reload 2>/dev/null || true
    rm -rf "$PREFIX"
    say "已卸载。内容目录 $PREFIX/data 已一并删除；如果需要保留，请从备份恢复。"
    exit 0
fi

# ── 前置检查 ────────────────────────────────────────────────────

PYTHON="$(command -v python3 || true)"
[ -n "$PYTHON" ] || die "找不到 python3。Debian/deepin：sudo apt install python3（通常系统自带）"

[ -f "$HERE/kidssafe_story_server.py" ] || die "缺少 kidssafe_story_server.py，请在 Linux/server 目录里运行本脚本"
[ -d "$HERE/data" ] || die "缺少 data 目录（内容库）"

"$PYTHON" - "$HERE/data/stories.json" <<'PY' || die "data/stories.json 不是合法的 JSON，先修好再装"
import json, sys
try:
    with open(sys.argv[1], encoding="utf-8") as handle:
        data = json.load(handle)
    print("内容库：%d 篇 / %d 类" % (len(data.get("stories") or []),
                                 len(data.get("categories") or [])))
except Exception as error:
    print("解析失败：%s" % error)
    sys.exit(1)
PY

# ── 复制程序 ────────────────────────────────────────────────────

say "==> 安装到 $PREFIX"
mkdir -p "$PREFIX"
install -m 0755 "$HERE/kidssafe_story_server.py" "$PREFIX/kidssafe_story_server.py"
# 注意：set -e 下 `[ -f x ] && cmd` 在条件为假时会让整条命令返回 1 并直接退出脚本，
# 这里必须写成 if。
if [ -f "$HERE/README.md" ]; then
    install -m 0644 "$HERE/README.md" "$PREFIX/README.md"
fi
rm -rf "$PREFIX/web"
cp -r "$HERE/web" "$PREFIX/web"

# 内容库只在首次安装时复制：升级程序不能覆盖家长已经改过的故事。
if [ -f "$PREFIX/data/stories.json" ]; then
    # 新版自带的示例故事不覆盖用户内容，只提示可以对比
    if ! cmp -s "$HERE/data/stories.json" "$PREFIX/data/stories.json"; then
        say "    已有内容库保留不变（新版本示例未覆盖）：$PREFIX/data/stories.json"
    fi
else
    mkdir -p "$PREFIX/data"
    cp "$HERE/data/stories.json" "$PREFIX/data/stories.json"
    say "    已复制示例内容库"
fi

# 家长自编目录：存在即保留
mkdir -p "$PREFIX/data/stories"

# ── systemd 用户服务 ────────────────────────────────────────────

if ! command -v systemctl >/dev/null 2>&1; then
    say ""
    say "这台机器没有 systemctl，直接手动运行："
    say "    $PYTHON $PREFIX/kidssafe_story_server.py --port $PORT"
    exit 0
fi

mkdir -p "$UNIT_DIR"
cat > "$UNIT_DIR/$UNIT_NAME" <<EOF
[Unit]
Description=KidsSafe story server (儿童安全桌面 · 故事服务端)
Documentation=file://$PREFIX/README.md
After=network-online.target
Wants=network-online.target

[Service]
Type=simple
ExecStart=$PYTHON $PREFIX/kidssafe_story_server.py --port $PORT
Restart=on-failure
RestartSec=3
NoNewPrivileges=yes

[Install]
WantedBy=default.target
EOF

say "==> 注册用户服务 $UNIT_DIR/$UNIT_NAME"
systemctl --user daemon-reload
systemctl --user enable "$UNIT_NAME" >/dev/null 2>&1 || true
systemctl --user restart "$UNIT_NAME"

# 用户服务默认在未登录时不启动。开启 linger 后，开机即跑（不需要 sudo 之外的交互）。
if command -v loginctl >/dev/null 2>&1; then
    if ! loginctl show-user "$USER" 2>/dev/null | grep -q "Linger=yes"; then
        say "    提示：要让服务在未登录时也运行，执行一次：sudo loginctl enable-linger $USER"
    fi
fi

sleep 1
if systemctl --user is-active --quiet "$UNIT_NAME"; then
    say "==> 服务已启动"
else
    say "!! 服务没起来，查看日志："
    say "    journalctl --user -u $UNIT_NAME -n 30 --no-pager"
fi

# ── 打印该填给孩子的地址 ────────────────────────────────────────

say ""
say "家长后台：  http://127.0.0.1:$PORT/"
say "孩子端填："
if command -v hostname >/dev/null 2>&1; then
    for ip in $(hostname -I 2>/dev/null || true); do
        case "$ip" in
            127.*) continue ;;
        esac
        say "            http://$ip:$PORT"
    done
fi
say ""
say "把上面任意一个「孩子端填」的地址，填进儿童安全桌面的"
say "「家长 → 家长后台 → 在线故事与内容 → 服务器地址」，再打开开关即可。"
say ""
say "改内容：编辑 $PREFIX/data/stories.json，或在 $PREFIX/data/stories/<分类名>/ 里放 txt，"
say "然后在家长后台点「重新载入内容」，不用重启服务。"
