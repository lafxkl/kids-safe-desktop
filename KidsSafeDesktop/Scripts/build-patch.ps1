<#
.SYNOPSIS
    生成增量更新补丁：比对两个发布目录，只挑变化的文件，签名成一份清单。

.DESCRIPTION
    客户端只认「签过名的清单」，所以这个脚本的核心不是复制文件，而是三件事：

      1. 只打我们自己的代码（KidsSafe*.dll）与内容文件，框架/运行时一律不碰
         —— 那些文件在两次发版之间不会变，打进补丁只会把 2 MB 撑成 60 MB；
      2. 按 SHA-256 比对，只复制「新增或内容变了」的文件；
      3. 用私钥签 manifest.json 的**原始字节**，签名单独存 manifest.sig。

    服务端只负责把这两个文件原样转给客户端，不持有私钥
    （方案见 Docs/增量更新方案.md 第三节与第七节）。

.PARAMETER Platform
    windows / android。决定允许打哪些文件。

.PARAMETER OldDir
    上一版的发布目录（用来做 diff）。首次发补丁时可以指向一个空目录。

.PARAMETER NewDir
    这一版的发布目录。

.PARAMETER Version
    目标版本号，如 1.21。

.PARAMETER VersionCode
    目标构建号，必须比上一版大（客户端会拒绝更小的）。

.PARAMETER Base
    可以从哪些版本升上来，如 1.20。不传表示「任何版本都适用」（只建议纯内容补丁这么发）。

.PARAMETER MinHost
    宿主最低版本，默认等于 Base 的第一项。壳工程接口变了就必须提高它。

.PARAMETER OutDir
    输出目录，默认 ..\KidsSafeServer\wwwroot\patches\<平台>\current。

.EXAMPLE
    .\Scripts\build-patch.ps1 -Platform windows `
        -OldDir ..\_release\v1.20\win `
        -NewDir bin\Release\net10.0-windows10.0.19041.0\win-x64\publish `
        -Version 1.21 -VersionCode 22 -Base 1.20 -Notes "修复积木编程启动失败"

.EXAMPLE
    # 首次使用前生成一对密钥（私钥请离线备份，丢了就只能整包发版）
    openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:4096 -out Keys\update-signing-private.pem
    openssl pkey -in Keys\update-signing-private.pem -pubout -out Keys\update-signing-public.pem
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('windows', 'android')][string]$Platform,
    [Parameter(Mandatory)][string]$OldDir,
    [Parameter(Mandatory)][string]$NewDir,
    [Parameter(Mandatory)][string]$Version,
    [Parameter(Mandatory)][int]$VersionCode,
    [string[]]$Base = @(),
    [string]$MinHost = '',
    [int]$ExpireDays = 90,
    [string]$OutDir = '',
    [string]$KeyPath = '',
    [string]$Notes = ''
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

function Fail([string]$message) {
    Write-Host ""
    Write-Host "  ✗ $message" -ForegroundColor Red
    Write-Host ""
    exit 1
}

function Resolve-OpenSsl {
    $cmd = Get-Command openssl -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    foreach ($candidate in @(
            'C:\Program Files\Git\usr\bin\openssl.exe',
            'C:\Program Files\OpenSSL-Win64\bin\openssl.exe',
            'C:\Program Files (x86)\OpenSSL-Win32\bin\openssl.exe')) {
        if (Test-Path $candidate) { return $candidate }
    }
    return $null
}

# ── 参数整理 ────────────────────────────────────────────────────────────────
$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $OutDir) { $OutDir = Join-Path $repoRoot "..\KidsSafeServer\wwwroot\patches\$Platform\current" }
if (-not $KeyPath) { $KeyPath = Join-Path $repoRoot 'Keys\update-signing-private.pem' }
$publicKeyPath = Join-Path (Split-Path -Parent $KeyPath) 'update-signing-public.pem'

if (-not (Test-Path $NewDir)) { Fail "找不到这一版的发布目录：$NewDir" }
if (-not (Test-Path $KeyPath)) { Fail "找不到签名私钥：$KeyPath`n  生成方法见脚本头部示例（openssl genpkey …）" }

$openssl = Resolve-OpenSsl
if (-not $openssl) { Fail "没找到 openssl。签名要用它；装了 Git for Windows 的话通常在 C:\Program Files\Git\usr\bin\openssl.exe" }

$OldDir = if (Test-Path $OldDir) { (Resolve-Path $OldDir).Path } else { $null }
if (-not $OldDir) { Write-Host "  ! 上一版目录不存在，按「全部都是新文件」处理" -ForegroundColor Yellow }

# 只打我们自己的东西：框架 dll、运行时、原生库都不参与增量
$includePattern = if ($Platform -eq 'windows') {
    '^(KidsSafe[^/]*\.dll|Assets/.+)$'
} else {
    '^(KidsSafe[^/]*\.dll)$'
}
$allowedExtensions = @(
    '.dll', '.json', '.dat', '.bin',
    '.png', '.jpg', '.jpeg', '.webp', '.gif', '.svg',
    '.ttf', '.otf', '.wav', '.mp3', '.ogg', '.m4a',
    '.html', '.css', '.js', '.txt', '.csv'
)

function Get-RelativeFiles([string]$root) {
    $result = @{}
    if (-not $root) { return $result }
    $fullRoot = (Resolve-Path $root).Path.TrimEnd('\', '/')
    Get-ChildItem -Path $fullRoot -Recurse -File -ErrorAction SilentlyContinue | ForEach-Object {
        $relative = $_.FullName.Substring($fullRoot.Length).TrimStart('\', '/') -replace '\\', '/'
        $result[$relative] = $_
    }
    return $result
}

Write-Host ""
Write-Host "  增量补丁 · $Platform · v$Version ($VersionCode)" -ForegroundColor Cyan
Write-Host "  新版本目录  $((Resolve-Path $NewDir).Path)"
Write-Host "  上一版目录  $(if ($OldDir) { $OldDir } else { '(无)' })"
Write-Host "  输出目录    $OutDir"
Write-Host ""

# ── 选文件 ──────────────────────────────────────────────────────────────────
$newFiles = Get-RelativeFiles $NewDir
$oldFiles = Get-RelativeFiles $OldDir

$changed = @()
foreach ($relative in ($newFiles.Keys | Sort-Object)) {
    if ($relative -notmatch $includePattern) { continue }
    if ($allowedExtensions -notcontains [System.IO.Path]::GetExtension($relative).ToLowerInvariant()) { continue }

    $newFile = $newFiles[$relative]
    $newHash = (Get-FileHash $newFile.FullName -Algorithm SHA256).Hash.ToLowerInvariant()

    if ($oldFiles.ContainsKey($relative)) {
        $oldHash = (Get-FileHash $oldFiles[$relative].FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($oldHash -eq $newHash) { continue }
    }

    $changed += [pscustomobject]@{
        Path   = $relative
        Source = $newFile.FullName
        Sha256 = $newHash
        Size   = $newFile.Length
    }
}

if ($changed.Count -eq 0) {
    Write-Host "  没有文件变化 —— 这一次不需要发补丁（版本号变了但内容没变？）" -ForegroundColor Yellow
    exit 0
}

# 顺带找出被删掉的文件（上一版有、这一版没有），写到 removed 里让客户端清理
$removed = @()
foreach ($relative in ($oldFiles.Keys | Sort-Object)) {
    if ($relative -notmatch $includePattern) { continue }
    if ($allowedExtensions -notcontains [System.IO.Path]::GetExtension($relative).ToLowerInvariant()) { continue }
    if (-not $newFiles.ContainsKey($relative)) { $removed += $relative }
}

# ── 复制到输出目录 ──────────────────────────────────────────────────────────
if (Test-Path $OutDir) { Remove-Item $OutDir -Recurse -Force }
$filesDir = Join-Path $OutDir 'files'
New-Item -ItemType Directory -Path $filesDir -Force | Out-Null

$totalBytes = 0
foreach ($item in $changed) {
    $target = Join-Path $filesDir ($item.Path -replace '/', '\')
    New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
    Copy-Item $item.Source $target -Force
    $totalBytes += $item.Size
    Write-Host ("    + {0,-52} {1,8:N0} KB" -f $item.Path, ($item.Size / 1KB))
}
foreach ($relative in $removed) {
    Write-Host ("    - {0}" -f $relative) -ForegroundColor DarkGray
}

# ── 写清单 ──────────────────────────────────────────────────────────────────
if (-not $MinHost) { $MinHost = if ($Base.Count -gt 0) { $Base[0] } else { $Version } }
$now = (Get-Date).ToUniversalTime()
$manifest = [ordered]@{
    schema      = 1
    platform    = $Platform
    channel     = 'stable'
    version     = $Version
    versionCode = $VersionCode
    minHost     = $MinHost
    base        = $Base
    issuedAt    = $now.ToString('yyyy-MM-ddTHH:mm:ssZ')
    expiresAt   = $now.AddDays($ExpireDays).ToString('yyyy-MM-ddTHH:mm:ssZ')
    notes       = $Notes
    files       = @($changed | ForEach-Object {
            [ordered]@{ path = $_.Path; sha256 = $_.Sha256; size = $_.Size }
        })
    removed     = @($removed)
}

$manifestPath = Join-Path $OutDir 'manifest.json'
$json = $manifest | ConvertTo-Json -Depth 6
[System.IO.File]::WriteAllText($manifestPath, $json, (New-Object System.Text.UTF8Encoding($false)))

# ── 签名（对清单的原始字节签名）────────────────────────────────────────────
$sigPath = Join-Path $OutDir 'manifest.sig'
& $openssl dgst -sha256 -sign $KeyPath -sigopt rsa_padding_mode:pss -sigopt rsa_pss_saltlen:digest `
    -out $sigPath $manifestPath
if ($LASTEXITCODE -ne 0) { Fail "签名失败（openssl 返回 $LASTEXITCODE）" }

# 立刻验一次：签名这一步出错，发出去的补丁在客户端全部会被拒，现场很难查
if (Test-Path $publicKeyPath) {
    & $openssl dgst -sha256 -verify $publicKeyPath -sigopt rsa_padding_mode:pss `
        -sigopt rsa_pss_saltlen:digest -signature $sigPath $manifestPath | Out-Null
    if ($LASTEXITCODE -ne 0) { Fail "签名自检失败：公钥与私钥不匹配？" }
}

# ── 总结 ────────────────────────────────────────────────────────────────────
$fullSize = ($newFiles.Values | Where-Object { $_.FullName -match '\.(dll|exe)$' } | Measure-Object Length -Sum).Sum
Write-Host ""
Write-Host ("  完成：{0} 个文件，{1:N1} MB" -f $changed.Count, ($totalBytes / 1MB)) -ForegroundColor Green
if ($fullSize -gt 0) {
    Write-Host ("  整包里的程序文件约 {0:N0} MB —— 这次家长只需要下 {1:N1} MB" -f ($fullSize / 1MB), ($totalBytes / 1MB))
}
Write-Host ""
Write-Host "  清单  $manifestPath"
Write-Host "  签名  $sigPath"
Write-Host "  文件  $filesDir"
Write-Host ""
Write-Host "  下一步：把整个 current 目录同步到服务器（wwwroot\patches\$Platform\current），" -ForegroundColor Cyan
Write-Host "         客户端下次启动会自动检查；/api/health 里的 patchVersion 能看到发到哪一版。" -ForegroundColor Cyan
Write-Host ""
