# =============================================================================
#  build-installer.ps1 —— 打 Windows 安装包（Inno Setup）
#
#  前置：已用 release.bat 构建过 Windows 产物；已安装 Inno Setup 6。
#  产出：Website\downloads\KidsSafeDesktop-<版本>-setup.exe
#
#  与免安装版的关系：
#   · 免安装版是 release.bat 打的 -win.zip，解压即用，不写注册表 → 没有桌面右键；
#   · 安装版由本脚本产出，安装时按用户勾选注册桌面右键与开始菜单，可正常卸载。
#
#  用法：
#    powershell -File Scripts\build-installer.ps1
#    powershell -File Scripts\build-installer.ps1 -Version 1.19
# =============================================================================

param(
    [string]$Version = ''
)

$ErrorActionPreference = 'Stop'

function Step($t) { Write-Host ""; Write-Host "== $t ==" -ForegroundColor Cyan }
function Ok($t)   { Write-Host "[OK] $t" -ForegroundColor Green }
function Fail($t) { Write-Host "[X] $t" -ForegroundColor Red; exit 1 }

$Root = Split-Path -Parent $PSScriptRoot
$Csproj = Join-Path $Root 'KidsSafeDesktop.csproj'
$Iss = Join-Path $Root 'Installer\KidsSafeDesktop.iss'
$BuildRoot = Join-Path $Root 'bin\Release\publish-win-x64'


# ── 1. 版本号 ────────────────────────────────────────────────
Step '读取版本号'
if (-not $Version) {
    $xml = [xml](Get-Content -Raw -Encoding UTF8 $Csproj)
    $Version = $xml.Project.PropertyGroup.ApplicationDisplayVersion | Where-Object { $_ } | Select-Object -First 1
}
if (-not $Version) { Fail 'csproj 里没有 ApplicationDisplayVersion，请用 -Version 指定' }
Ok "版本 v$Version"

# ── 2. 检查 Windows 产物 ─────────────────────────────────────
Step '检查 Windows 产物'
if (-not (Test-Path $BuildRoot)) {
    Fail "没有找到 $BuildRoot，请先运行 release.bat 构建 Windows 版"
}
$exe = Join-Path $BuildRoot 'KidsSafeDesktop.exe'
if (-not (Test-Path $exe)) { Fail '产物目录里没有 KidsSafeDesktop.exe' }
# 安装包必须来自自包含发布目录：缺 coreclr.dll 说明是 framework-dependent 输出，
# 装到没装 .NET 桌面运行时的电脑上会起不来
if (-not (Test-Path (Join-Path $BuildRoot 'coreclr.dll'))) {
    Fail "产物目录不是自包含发布（缺 coreclr.dll）：$BuildRoot，请先跑 Scripts\release.ps1"
}
Ok 'Windows 自包含产物存在'

# Inno Setup 不自带中文语言文件，这个 .isl 是随项目一起提交的
$isl = Join-Path $Root 'Installer\ChineseSimplified.isl'
if (-not (Test-Path $isl)) { Fail "缺少中文语言文件 Installer\ChineseSimplified.isl，安装界面会退回英文或直接编译失败" }
Ok '中文语言文件存在'

# ── 3. 重新生成隐私政策文本 ──────────────────────────────────
Step '导出隐私政策'
& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $Root 'Scripts\export-privacy.ps1')
if ($LASTEXITCODE -ne 0) { Fail '隐私政策导出失败' }

# ── 4. 找 Inno Setup ─────────────────────────────────────────
Step '查找 Inno Setup'
$isccCandidates = @(
    (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
    (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe'),
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe')
) | Where-Object { $_ -and (Test-Path $_) }

$iscc = $isccCandidates | Select-Object -First 1
if (-not $iscc) {
    Fail '没有找到 ISCC.exe。请先安装 Inno Setup 6：winget install --id JRSoftware.InnoSetup --source winget'
}
Ok $iscc

# ── 5. 编译安装包 ────────────────────────────────────────────
Step '编译安装包'
Write-Host '  lzma2/max 压缩 200MB 左右的内容，通常 1-3 分钟…'
& $iscc "/DAppVersion=$Version" $Iss
if ($LASTEXITCODE -ne 0) { Fail 'ISCC 编译失败，往上翻日志找 error' }

$setup = Join-Path $Root "Website\downloads\KidsSafeDesktop-$Version-setup.exe"
if (-not (Test-Path $setup)) { Fail "没有生成安装包：$setup" }

$len = (Get-Item $setup).Length
$hash = (Get-FileHash $setup -Algorithm SHA256).Hash

Step '完成'
Ok ("安装包  " + $setup)
Write-Host ("        " + $len.ToString('N0') + " 字节")
Write-Host ("        SHA-256  " + $hash)
Write-Host ''
Write-Host '提醒：安装包会注册桌面右键菜单；免安装 zip 不会。' -ForegroundColor DarkGray
