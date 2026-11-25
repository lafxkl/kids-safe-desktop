<#
.SYNOPSIS
    打安卓的「国内版」与「海外版」两个包。

.DESCRIPTION
    两个包不是同一份 APK 改个名，差别在工程层面：

      cn     国内版   包名 com.companyname.kidssafedesktop（沿用，老用户可升级）
                      **不包含** Singular Kids SDK：AAR 不引用、代码不编译
      global 海外版   包名 cc.siguangblog.kidsafe
                      包含 Singular Kids SDK，启动时按地区判断，中国大陆设备不上报

    两个渠道共用 obj 目录，脚本按顺序串行构建，不并行。

.PARAMETER Channel
    cn / global / both（默认 both）

.PARAMETER Configuration
    Release（默认）或 Debug

.EXAMPLE
    .\Scripts\build-android-channels.ps1
    两个包都打，产物复制到 dist\

.EXAMPLE
    .\Scripts\build-android-channels.ps1 -Channel global
    只打海外包（例如这次只上 Google Play）
#>
[CmdletBinding()]
param(
    [ValidateSet('cn', 'global', 'both')]
    [string]$Channel = 'both',

    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'

# 直接读 APK 里的 dex，看有没有被打进某个 Java 类。
# 为什么不用「有没有引用 AAR」来判断：AAR 不引用时，上一轮解包的 class
# 仍可能留在 obj 里并被 dex 收进去，清单里也不会有对应权限，属于静默出错。
function Test-ApkHasClass {
    param(
        [Parameter(Mandatory)] [string] $ApkPath,
        [Parameter(Mandatory)] [string] $JniPath   # 例如 Lcom/singular/sdk/
    )

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $latin1 = [System.Text.Encoding]::GetEncoding(28591)

    $zip = [System.IO.Compression.ZipFile]::OpenRead($ApkPath)
    try {
        foreach ($entry in $zip.Entries) {
            if ($entry.FullName -notlike '*.dex') { continue }

            $stream = $entry.Open()
            try {
                $ms = New-Object System.IO.MemoryStream
                $stream.CopyTo($ms)
                $text = $latin1.GetString($ms.ToArray())
                if ($text.Contains($JniPath)) { return $true }
            } finally {
                $stream.Dispose()
            }
        }
    } finally {
        $zip.Dispose()
    }

    return $false
}

$root = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $root 'KidsSafeDesktop.csproj'
if (-not (Test-Path $proj)) { throw "找不到工程文件：$proj" }

# 版本号从 csproj 读，避免脚本里写死一个数、跟工程脱节
$csprojText = Get-Content -Path $proj -Raw -Encoding UTF8
$version = ([regex]'<ApplicationDisplayVersion>([^<]+)</ApplicationDisplayVersion>').Match($csprojText).Groups[1].Value.Trim()
$versionCode = ([regex]'<ApplicationVersion>([^<]+)</ApplicationVersion>').Match($csprojText).Groups[1].Value.Trim()
if (-not $version) { throw 'csproj 里没读到 ApplicationDisplayVersion' }

$appIds = @{
    cn     = 'com.companyname.kidssafedesktop'
    global = 'cc.siguangblog.kidsafe'
}

$channels = if ($Channel -eq 'both') { @('cn', 'global') } else { @($Channel) }

# 海外版没配密钥时给一句提醒：包能打，但统计是空操作
if ($channels -contains 'global') {
    $hasProps = Test-Path (Join-Path $root 'Singular.props')
    $hasEnv = -not [string]::IsNullOrWhiteSpace($env:SINGULAR_API_KEY)
    if (-not $hasProps -and -not $hasEnv) {
        Write-Host '提示：没找到 Singular.props，也没有 SINGULAR_API_KEY 环境变量。' -ForegroundColor Yellow
        Write-Host '      海外包照常能打，但启动后不会初始化统计 SDK（空操作）。' -ForegroundColor Yellow
    }
}

$dist = Join-Path $root 'dist'
New-Item -ItemType Directory -Force -Path $dist | Out-Null

$results = @()

foreach ($ch in $channels) {
    Write-Host ''
    Write-Host "==> 构建 $ch（$Configuration，v$version / $versionCode）" -ForegroundColor Cyan

    & dotnet build $proj -f net10.0-android -c $Configuration -v q "-p:AppChannel=$ch"
    if ($LASTEXITCODE -ne 0) { throw "$ch 构建失败，退出码 $LASTEXITCODE" }

    # 两个渠道的产物目录不同（见 Directory.Build.props）：
    # 国内版沿用 bin\<Configuration>\net10.0-android，
    # 海外版在 bin\global\<Configuration>\net10.0-android。
    $binDir = if ($ch -eq 'global') {
        Join-Path $root "bin\global\$Configuration\net10.0-android"
    } else {
        Join-Path $root "bin\$Configuration\net10.0-android"
    }
    $appId = $appIds[$ch]

    # 优先取已签名的那份；MAUI 在配了 Signing.props 时会产出 -Signed.apk
    $apk = Get-ChildItem -Path $binDir -Filter "$appId*.apk" -ErrorAction SilentlyContinue |
        Sort-Object @{ Expression = { $_.Name -like '*-Signed.apk' }; Descending = $true },
                    @{ Expression = { $_.LastWriteTime }; Descending = $true } |
        Select-Object -First 1

    if (-not $apk) { throw "$ch 构建成功但没找到 APK（在 $binDir 里找 $appId*.apk）" }

    $target = Join-Path $dist "KidsSafeDesktop-$version-$ch.apk"
    Copy-Item -Path $apk.FullName -Destination $target -Force

    # 自检：国内包不能有 Singular 的类，海外包必须有
    $hasSingular = Test-ApkHasClass -ApkPath $target -JniPath 'Lcom/singular/sdk/'
    if ($ch -eq 'cn' -and $hasSingular) {
        throw '国内包里发现了 Singular 的类：obj 里有上一轮的解包残留，请删掉 obj\ 与 bin\ 后重打'
    }
    if ($ch -eq 'global' -and -not $hasSingular) {
        throw '海外包里没有 Singular 的类：检查 libs 下的两个 AAR 是否还在，以及 AppChannel 是否真的传进来了'
    }

    $hash = (Get-FileHash -Path $target -Algorithm SHA256).Hash
    $sizeMb = [math]::Round((Get-Item -LiteralPath $target).Length / 1MB, 1)

    $results += [pscustomobject]@{
        渠道     = $ch
        包名     = $appId
        大小MB   = $sizeMb
        Singular = if ($hasSingular) { '有' } else { '无' }
        文件     = $target
        SHA256   = $hash
    }
}

Write-Host ''
Write-Host '==> 完成，产物在 dist\' -ForegroundColor Green
$results | Format-Table -AutoSize | Out-String | Write-Host

foreach ($r in $results) {
    Write-Host ("{0,-7} {1}" -f $r.渠道, $r.文件)
    Write-Host ("        SHA256 {0}" -f $r.SHA256)
}
