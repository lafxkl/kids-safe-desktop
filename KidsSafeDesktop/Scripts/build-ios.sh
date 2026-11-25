#!/usr/bin/env bash
# ════════════════════════════════════════════════════════════════════════════
# iOS 构建脚本（必须在 macOS 上执行；Windows 只能做编译检查，产不出 ipa）
#
# 用法：
#   bash Scripts/build-ios.sh sim       模拟器包，直接拖进 Simulator 跑（不需要签名）
#   bash Scripts/build-ios.sh device    真机 Debug（需要签名，Xcode 自动签名也可）
#   bash Scripts/build-ios.sh ipa       Release 归档并导出 ipa（TestFlight / App Store）
#
# 前置条件：
#   · Xcode 16+（.NET 10 for iOS 要求），xcode-select 指向 Xcode 而不是 CommandLineTools
#   · dotnet workload install ios maui（Windows 侧已装，Mac 上需重装一次）
#   · ipa：Signing.iOS.props 里填好 IosCodesignKey / IosCodesignProvision，
#          或命令行传 -p:CodesignKey=... -p:CodesignProvision=...
# ════════════════════════════════════════════════════════════════════════════
set -euo pipefail

MODE="${1:-sim}"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJ="$ROOT/KidsSafeDesktop/KidsSafeDesktop.csproj"

say() { printf '\n\033[1;36m▶ %s\033[0m\n' "$*"; }
die() { printf '\n\033[1;31m✗ %s\033[0m\n' "$*" >&2; exit 1; }

# ── 环境检查 ────────────────────────────────────────────────────────────────
[[ "$(uname -s)" == "Darwin" ]] || die "iOS 只能在 macOS 上构建（当前：$(uname -s)）。Windows 上可执行编译检查：dotnet build -f net10.0-ios -p:EnableIosTarget=true"

if ! xcode-select -p 2>/dev/null | grep -q "Xcode"; then
  die "xcode-select 没有指向 Xcode。执行：sudo xcode-select -s /Applications/Xcode.app"
fi

say "Xcode：$(xcodebuild -version | head -1)"
say "dotnet：$(dotnet --version)"

# ── 构建 ────────────────────────────────────────────────────────────────────
case "$MODE" in
  sim)
    # Apple Silicon 用 arm64 模拟器；Intel Mac 用 x64
    if [[ "$(uname -m)" == "arm64" ]]; then RID="iossimulator-arm64"; else RID="iossimulator-x64"; fi
    say "编译模拟器包（$RID）"
    dotnet build "$PROJ" -f net10.0-ios -c Debug -p:EnableIosTarget=true -p:RuntimeIdentifier="$RID"
    APP="$ROOT/KidsSafeDesktop/bin/Debug/net10.0-ios/$RID/KidsSafeDesktop.app"
    say "产物：$APP"
    cat <<EOF

  安装到已启动的模拟器：
    xcrun simctl boot "iPhone 16" 2>/dev/null || true
    open -a Simulator
    xcrun simctl install booted "$APP"
    xcrun simctl launch booted cc.siguangblog.kidsafe
EOF
    ;;

  device)
    say "编译真机 Debug（签名走 Xcode 自动或 Signing.iOS.props）"
    dotnet build "$PROJ" -f net10.0-ios -c Debug -p:EnableIosTarget=true \
      -p:RuntimeIdentifier=ios-arm64
    APP="$ROOT/KidsSafeDesktop/bin/Debug/net10.0-ios/ios-arm64/KidsSafeDesktop.app"
    say "产物：$APP"
    echo "  用 Xcode 的 Devices and Simulators 窗口把它拖进手机，或用 ios-deploy 安装。"
    ;;

  ipa)
    say "编译 Release 归档 + 导出 ipa"
    dotnet publish "$PROJ" -f net10.0-ios -c Release \
      -p:EnableIosTarget=true \
      -p:RuntimeIdentifier=ios-arm64 \
      -p:BuildIpa=true \
      -p:ArchiveOnBuild=true

    OUT="$ROOT/KidsSafeDesktop/bin/Release/net10.0-ios/ios-arm64/publish"
    say "发布目录：$OUT"
    ls -lh "$OUT" 2>/dev/null | sed 's/^/    /' || true

    IPA=$(find "$OUT" -maxdepth 1 -name '*.ipa' | head -1 || true)
    if [[ -n "$IPA" ]]; then
      mkdir -p "$ROOT/dist"
      cp "$IPA" "$ROOT/dist/"
      say "已复制到：$ROOT/dist/$(basename "$IPA")"
      echo
      echo "  上传 TestFlight 的两种方式："
      echo "    xcrun altool --upload-app -f \"$IPA\" -t ios --apiKey <KEY_ID> --apiIssuer <ISSUER_ID>"
      echo "    或用 Xcode → Window → Organizer 上传 .xcarchive"
    else
      die "没有生成 ipa。看上面的日志：多半是签名（CodesignKey/描述文件）没配对。"
    fi
    ;;

  *)
    die "未知模式：$MODE（可用：sim / device / ipa）"
    ;;
esac
