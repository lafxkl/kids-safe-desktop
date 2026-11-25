<#
    儿童安全桌面 · 发布版一键构建

    这个脚本把「发一个版本」的十几个手工步骤固化成一条命令，起因是手工做
    很容易漏：版本号四处不同步、APK 忘了复制到服务端目录、官网大小/日期没更新。

    用法（在项目根目录或任意位置）：
        release.bat                 用当前 csproj 的版本号重新出包（不改版本）
        release.bat 1.17 18         升到 v1.17 / versionCode 18，改版本号并出包
        release.bat 1.17            只给版本号，versionCode 自动 +1
        release.bat -SkipBuild      不重新编译，只用现有产物打包归位
        release.bat -Clean          构建前清理 bin/obj

    脚本会自动做：
        1. 校验并写入版本号（KidsSafeDesktop.csproj）
        2. 构建 Android Release（签名 APK）+ Windows Release
        3. 打包成 KidsSafeDesktop-<版本>.apk / -win.zip
        4. 把上一版安装包归档到 ..\_release\v<旧版本>\
        5. 复制到 Website/downloads 与 KidsSafeServer/wwwroot/downloads
        6. 计算文件大小与 SHA-256
        7. 回填官网 index.html（徽章 / 版本行 / 大小 / 日期 / 下载链接 / 页脚）
        8. 回填 permissions.html 的「适用版本」
        9. 回填 changelog.html 的「当前版本 / 共 N 个版本 / 最近更新」
       10. 检查 Releases.cs 与 changelog.html 是否漏了本版条目，漏了就警告
       11. 回填 sitemap.xml 的 lastmod 与 index.html 里 JSON-LD 的 softwareVersion

    不自动做（内容要人来写，脚本猜不了）：
        - KidsSafeServer/Releases.cs 的本版要点
        - changelog.html 的本版 <article> 段落
#>

[CmdletBinding()]
param(
    [string]$Version,
    [int]$Build = 0,
    [switch]$SkipBuild,
    [switch]$Clean,
    [Alias('h')]
    [switch]$Help
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

if ($Help) {
    Write-Host @'

儿童安全桌面 · 发布版构建

  用法
    release.bat                  用 csproj 里的当前版本重新出包
    release.bat 1.17 18          升到 v1.17 / versionCode 18
    release.bat 1.17             只给版本号，versionCode 自动 +1
    release.bat -SkipBuild       不编译，只用现有产物打包归位
    release.bat -Clean           构建前清理 bin / obj
    release.bat --help           显示这段说明

  参数
    1  版本号，格式「主.次」，如 1.17
    2  versionCode，必须是递增的整数；省略时自动 +1

  会自动完成
    写版本号 → 双端 Release 构建 → 打包 → 归档旧包 → 复制到官网与服务端
    → 计算 SHA-256 → 回填 index.html / permissions.html / changelog.html
    → 检查 Releases.cs 与 changelog 是否漏了本版条目

  需要手工补的
    changelog.html 的本版 <article> 段落、Releases.cs 的本版要点

'@
    exit 0
}

$Root = Split-Path -Parent $PSScriptRoot
Set-Location $Root

$CsprojPath    = Join-Path $Root 'KidsSafeDesktop.csproj'
$WebsiteDir    = Join-Path $Root 'Website'
$IndexPath     = Join-Path $WebsiteDir 'index.html'
$DownloadPath  = Join-Path $WebsiteDir 'download.html'
$ChangelogPath = Join-Path $WebsiteDir 'changelog.html'
$DownloadsDir  = Join-Path $WebsiteDir 'downloads'
$ServerDlDir   = Join-Path (Split-Path -Parent $Root) 'KidsSafeServer\wwwroot\downloads'
$ReleasesPath  = Join-Path (Split-Path -Parent $Root) 'KidsSafeServer\Releases.cs'
$ArchiveRoot   = Join-Path (Split-Path -Parent $Root) '_release'

$Utf8NoBom = New-Object System.Text.UTF8Encoding $false
$Today = Get-Date -Format 'yyyy-MM-dd'

$script:WarnCount = 0

# ── 输出小工具 ──────────────────────────────────────────────
function Step($msg) { Write-Host ''; Write-Host "=== $msg ===" -ForegroundColor Cyan }
function Ok($msg)   { Write-Host "  [OK] $msg"   -ForegroundColor Green }
function Warn($msg) {
    $script:WarnCount++
    Write-Host "  [!]  $msg"   -ForegroundColor Yellow
}
function Fail($msg) { Write-Host "  [X]  $msg"   -ForegroundColor Red; Write-Host ""; exit 1 }

function Read-Text($path)  { [System.IO.File]::ReadAllText($path) }
function Write-Text($path, $text) {
    [System.IO.File]::WriteAllText($path, $text, $Utf8NoBom)
}

# ── 0. 前置检查 ─────────────────────────────────────────────
Step '检查环境'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Fail '没找到 dotnet 命令，请先安装 .NET 10 SDK 并加入 PATH'
}
Ok ("dotnet " + (& dotnet --version))

foreach ($p in @($CsprojPath, $IndexPath, $ChangelogPath)) {
    if (-not (Test-Path $p)) { Fail "缺少文件：$p" }
}

$signing = Join-Path $Root 'Signing.props'
if (-not (Test-Path $signing)) {
    Warn 'Signing.props 不存在：Android 包会用默认调试签名，与已发布版本签名不一致，老用户无法覆盖安装'
} else {
    Ok 'Signing.props 已就位'
}

if (-not (Test-Path $DownloadsDir)) { New-Item -ItemType Directory -Path $DownloadsDir -Force | Out-Null }

# ── 1. 解析与写入版本号 ─────────────────────────────────────
Step '版本号'

$csproj = Read-Text $CsprojPath

$mVer  = [regex]::Match($csproj, '<ApplicationDisplayVersion>([^<]+)</ApplicationDisplayVersion>')
$mCode = [regex]::Match($csproj, '<ApplicationVersion>(\d+)</ApplicationVersion>')
if (-not $mVer.Success -or -not $mCode.Success) { Fail 'csproj 里找不到 ApplicationDisplayVersion / ApplicationVersion' }

$oldVer  = $mVer.Groups[1].Value
$oldCode = [int]$mCode.Groups[1].Value

if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = $oldVer
    if ($Build -eq 0) { $Build = $oldCode }
    Ok "沿用当前版本 v$Version / $Build"
} else {
    if ($Version -notmatch '^\d+\.\d+$') { Fail "版本号格式应为 主.次（如 1.17），收到：$Version" }
    if ($Build -eq 0) {
        $Build = $oldCode + 1
        Warn "未指定 versionCode，自动递增为 $Build"
    }
    if ($Build -le $oldCode -and $Version -ne $oldVer) {
        Fail "versionCode 必须递增：当前 $oldCode，收到 $Build"
    }
    Ok "目标版本 v$Version / $Build（原 v$oldVer / $oldCode）"
}

if ($Version -ne $oldVer -or $Build -ne $oldCode) {
    $patched = $csproj -replace '<ApplicationDisplayVersion>[^<]*</ApplicationDisplayVersion>',
                            "<ApplicationDisplayVersion>$Version</ApplicationDisplayVersion>"
    $patched = $patched -replace '<ApplicationVersion>\d+</ApplicationVersion>',
                              "<ApplicationVersion>$Build</ApplicationVersion>"
    Write-Text $CsprojPath $patched
    Ok "已写入 csproj：v$Version / $Build"
} else {
    Ok 'csproj 版本号无需改动'
}

# ── 2. 构建 ─────────────────────────────────────────────────
$ApkName = "KidsSafeDesktop-$Version.apk"
$ZipName = "KidsSafeDesktop-$Version-win.zip"
$ApkPath = Join-Path $DownloadsDir $ApkName
$ZipPath = Join-Path $DownloadsDir $ZipName

$AndroidOut = Join-Path $Root 'bin\Release\net10.0-android'
$WindowsOut = Join-Path $Root 'bin\Release\net10.0-windows10.0.19041.0\win-x64'
# 自包含发布单独放一个目录，避免和 dotnet build 的 framework-dependent 输出混在一起
$WindowsPublishDir = Join-Path $Root 'bin\Release\publish-win-x64'

if ($SkipBuild) {
    Step '跳过构建（-SkipBuild）'
    if (-not (Test-Path $AndroidOut)) { Fail "没有可用的 Android 产物：$AndroidOut" }
    if (-not (Test-Path (Join-Path $WindowsPublishDir 'coreclr.dll'))) {
        Fail "没有可用的 Windows 自包含产物（缺 coreclr.dll）：$WindowsPublishDir，先跑一次完整 release"
    }
} else {
    if ($Clean) {
        Step '清理 bin / obj'
        foreach ($d in @('bin', 'obj')) {
            $p = Join-Path $Root $d
            if (Test-Path $p) { Remove-Item $p -Recurse -Force; Ok "已删除 $d" }
        }
    }

    Step '构建 Android Release（签名 APK）'
    Write-Host '  这一步通常 4-7 分钟，请让命令行窗口保持打开…'
    $sw = [Diagnostics.Stopwatch]::StartNew()
    & dotnet build $CsprojPath -f net10.0-android -c Release -v m
    if ($LASTEXITCODE -ne 0) { Fail 'Android 构建失败，往上翻日志找 error' }
    Ok ("Android 完成，用时 " + [int]$sw.Elapsed.TotalSeconds + " 秒")

    Step '构建 Windows Release'
    $sw = [Diagnostics.Stopwatch]::StartNew()
    & dotnet build $CsprojPath -f net10.0-windows10.0.19041.0 -c Release -v m
    if ($LASTEXITCODE -ne 0) { Fail 'Windows 构建失败，往上翻日志找 error' }
}

# 免安装版必须是「自包含」的：家长机器上不一定装了 .NET 桌面运行时，
# 依赖系统运行时的包双击就是没反应。
#
# 这里曾经踩过一次：先 dotnet build 已经建好了 bin\...\win-x64 目录，
# 后面「目录不存在才补一次 publish」的判断永远为假，于是悄悄打出 framework-dependent 的包
# —— 安装包小了几十 MB，但在没装运行时的电脑上根本起不来。所以现在只要有构建，
# 就无条件 publish 到独立目录，并且把「有没有 coreclr.dll」当作发布成功的硬标准。
#
# -p:UseMonoRuntime=false 不能省：MAUI 的 workload 会把 RID 解析成 Mono 运行时包
# （Microsoft.NETCore.App.Runtime.Mono.win-x64），那个版本在 nuget.org 上根本不存在，
# 于是 publish 直接 NU1102 失败。Windows 本来就走 CoreCLR，显式关掉即可。
if (-not $SkipBuild) {
    Step '发布 Windows（自包含，免安装即用）'
    if (Test-Path $WindowsPublishDir) { Remove-Item $WindowsPublishDir -Recurse -Force }
    $sw = [Diagnostics.Stopwatch]::StartNew()
    & dotnet publish $CsprojPath -f net10.0-windows10.0.19041.0 -c Release -r win-x64 `
        --self-contained true -p:WindowsPackageType=None -p:UseMonoRuntime=false -o $WindowsPublishDir -v m
    if ($LASTEXITCODE -ne 0) { Fail 'Windows 自包含发布失败，往上翻日志找 error' }
    if (-not (Test-Path (Join-Path $WindowsPublishDir 'coreclr.dll'))) {
        Fail "Windows 发布目录里没有 coreclr.dll（$WindowsPublishDir），不是自包含包，不能发"
    }
    if (-not (Test-Path (Join-Path $WindowsPublishDir 'KidsSafeDesktop.exe'))) {
        Fail "Windows 发布目录里没有 KidsSafeDesktop.exe（$WindowsPublishDir）"
    }

    # 落盘器：增量更新靠它在主程序退出后替换 dll。
    # 单独发布、直接放进安装目录（不放进安装包的「额外文件」列表，因为它就在发布目录里）。
    # 加了裁剪，约 11 MB；不裁的话自包含单文件 37 MB。
    Step '发布落盘器（KidsSafeUpdater）'
    $updaterProj = Join-Path $PSScriptRoot 'Updater\KidsSafeUpdater.csproj'
    if (Test-Path $updaterProj) {
        & dotnet publish $updaterProj -c Release -r win-x64 --self-contained true `
            -p:PublishSingleFile=true -o $WindowsPublishDir -v m
        if ($LASTEXITCODE -ne 0) { Fail '落盘器发布失败' }
        if (-not (Test-Path (Join-Path $WindowsPublishDir 'KidsSafeUpdater.exe'))) {
            Fail "落盘器没有出现在发布目录里（$WindowsPublishDir）"
        }
        Ok '落盘器已就位'
    } else {
        Warn '没找到 Scripts\Updater，跳过落盘器（这次发的包不支持增量更新）'
    }

    Ok ("Windows 自包含发布完成，用时 " + [int]$sw.Elapsed.TotalSeconds + " 秒")
}

$WindowsOut = $WindowsPublishDir

# ── 3. 取出安卓 APK ─────────────────────────────────────────
Step '收集安装包'

$apkCandidates = @(Get-ChildItem $AndroidOut -Filter '*.apk' -ErrorAction SilentlyContinue |
                   Sort-Object LastWriteTime -Descending)
if ($apkCandidates.Count -eq 0) { Fail "Android 产物目录里没有 .apk：$AndroidOut" }

$apk = $apkCandidates | Where-Object { $_.Name -like '*Signed*' } | Select-Object -First 1
if (-not $apk) {
    $apk = $apkCandidates[0]
    Warn "没找到 *Signed*.apk，改用 $($apk.Name)（可能未签名）"
}
Copy-Item $apk.FullName $ApkPath -Force
Ok "APK → $ApkName"

if (-not (Test-Path $WindowsOut)) { Fail "Windows 产物目录不存在：$WindowsOut" }
if (Test-Path $ZipPath) { Remove-Item $ZipPath -Force }
# 免安装版只装运行必需的文件：WebView2 用户数据目录是本机跑过留下的缓存（约 9MB），
# pdb 是调试符号，两者都不该进发布包。免安装包不写注册表，所以也没有桌面右键菜单。
$zipItems = Get-ChildItem $WindowsOut -Force |
    Where-Object { $_.Name -ne 'KidsSafeDesktop.exe.WebView2' -and $_.Extension -ne '.pdb' }
Compress-Archive -Path $zipItems.FullName -DestinationPath $ZipPath -CompressionLevel Optimal
Ok "ZIP → $ZipName"

# ── 4. 归档旧包 ─────────────────────────────────────────────
Step '归档旧包'

if ($oldVer -ne $Version) {
    $archiveDir = Join-Path $ArchiveRoot "v$oldVer"
    $oldApk = Join-Path $DownloadsDir "KidsSafeDesktop-$oldVer.apk"
    $oldZip = Join-Path $DownloadsDir "KidsSafeDesktop-$oldVer-win.zip"

    if ((Test-Path $oldApk) -or (Test-Path $oldZip)) {
        if (-not (Test-Path $archiveDir)) { New-Item -ItemType Directory -Path $archiveDir -Force | Out-Null }
        foreach ($f in @($oldApk, $oldZip)) {
            if (Test-Path $f) {
                Move-Item $f (Join-Path $archiveDir (Split-Path $f -Leaf)) -Force
                Ok ("归档 " + (Split-Path $f -Leaf))
            }
        }
    } else {
        Warn "下载目录里没有 v$oldVer 的旧包，跳过归档"
    }
} else {
    Ok '版本号未变，不归档'
}

# ── 5. 同步到服务端下载目录 ─────────────────────────────────
Step '同步到服务端目录'

if (Test-Path $ServerDlDir) {
    # 先清掉这个目录里其他版本的包：服务端 /download.html 会把 downloads 下的包全列出来，
    # 留着旧版就会出现「1.17 和 1.18 同时可下」，家长下错版本。
    # 旧包此时已经归档到 _release\v旧版本\，删掉不丢东西。
    $stale = Get-ChildItem $ServerDlDir -File |
             Where-Object { $_.Name -match '^KidsSafeDesktop-\d+\.\d+(-win)?\.(apk|zip)$' -and
                            $_.Name -ne $ApkName -and $_.Name -ne $ZipName }
    if ($stale) {
        foreach ($f in $stale) { Remove-Item $f.FullName -Force }
        Ok ("清理服务端目录旧包 " + $stale.Count + " 个：" + (($stale | ForEach-Object { $_.Name }) -join ', '))
    }

    Copy-Item $ApkPath (Join-Path $ServerDlDir $ApkName) -Force
    Copy-Item $ZipPath (Join-Path $ServerDlDir $ZipName) -Force
    Ok "已复制 2 个文件到 KidsSafeServer\wwwroot\downloads"
} else {
    Warn "没有找到服务端下载目录，跳过：$ServerDlDir"
}

# ── 6. 计算大小与校验值 ─────────────────────────────────────
Step '校验值'

$apkLen  = (Get-Item $ApkPath).Length
$zipLen  = (Get-Item $ZipPath).Length
$apkHash = (Get-FileHash $ApkPath -Algorithm SHA256).Hash
$zipHash = (Get-FileHash $ZipPath -Algorithm SHA256).Hash
$sizeMb  = [int][math]::Round($apkLen / 1MB)

Ok ("APK  " + $apkLen.ToString('N0') + " 字节 / " + $sizeMb + " MB")
Ok ("ZIP  " + $zipLen.ToString('N0') + " 字节")

# ── 7. 回填官网首页与下载页 ─────────────────────────────────
Step '回填 index.html / download.html'

# 两个页面都写版本行与下载链接，规则完全一样，一起处理，避免下载页停在上一个版本
foreach ($page in @(
    @{ Path = $IndexPath;    Name = 'index.html' },
    @{ Path = $DownloadPath; Name = 'download.html' }
)) {
    if (-not (Test-Path $page.Path)) {
        Warn ("没有 " + $page.Name + '，跳过（官网缺少该页）')
        continue
    }

    $html   = Read-Text $page.Path
    $before = $html

    $html = $html -replace '🆕 最新 v\d+\.\d+', "🆕 最新 v$Version"
    $html = $html -replace '<b>v\d+\.\d+</b>（versionCode \d+）　·　约 \d+ MB　·　\d{4}-\d{2}-\d{2} 更新',
                           "<b>v$Version</b>（versionCode $Build）　·　约 $sizeMb MB　·　$Today 更新"
    $html = $html -replace 'downloads/KidsSafeDesktop-\d+\.\d+\.apk',     "downloads/KidsSafeDesktop-$Version.apk"
    $html = $html -replace 'downloads/KidsSafeDesktop-\d+\.\d+-win\.zip', "downloads/KidsSafeDesktop-$Version-win.zip"
    # 安装版链接同样跟着版本走（安装包由 build-installer.bat 产出）
    $html = $html -replace 'downloads/KidsSafeDesktop-\d+\.\d+-setup\.exe', "downloads/KidsSafeDesktop-$Version-setup.exe"
    $html = $html -replace '© 2026 儿童安全桌面 · v\d+\.\d+ ·',         "© 2026 儿童安全桌面 · v$Version ·"

    # 先确认锚点还在：如果连版本行都匹配不到，说明模板被改过，静默跳过最坑人
    if ($html -notmatch '<b>v\d+\.\d+</b>（versionCode \d+）') {
        Write-Text $page.Path $html
        Warn ($page.Name + ' 里找不到「v1.x（versionCode N）」版本行，模板可能改过，请手工检查')
    } elseif ($html -eq $before) {
        Ok ($page.Name + ' 已是目标版本，无需改动')
    } else {
        Write-Text $page.Path $html
        Ok ($page.Name + ": 已更新徽章、版本行（约 $sizeMb MB / $Today）、下载链接、页脚")
    }
}

# ── 8. 回填权限说明页的适用版本 ───────────────────────────
Step '回填 permissions.html'

$PermPath = Join-Path $WebsiteDir 'permissions.html'
if (Test-Path $PermPath) {
    $pm = Read-Text $PermPath
    $pmNew = $pm -replace '适用版本 v\d+\.\d+（versionCode \d+）',
                          "适用版本 v$Version（versionCode $Build）"
    if ($pmNew -ne $pm) {
        Write-Text $PermPath $pmNew
        Ok "已更新权限页适用版本 v$Version / $Build"
    } else {
        Ok '权限页版本号已是目标值'
    }
} else {
    Warn '没有 permissions.html，跳过（官网缺少应用权限说明页）'
}

# ── 9. 回填 changelog 头部 ──────────────────────────────────
Step '回填 changelog.html'

$cl = Read-Text $ChangelogPath
$before = $cl
$idTag = 'v' + ($Version -replace '\.', '-')
$hasEntry = $cl -match ('id="' + $idTag + '"')

$cl = $cl -replace '当前版本 <b>v\d+\.\d+</b>（versionCode \d+）',
                   "当前版本 <b>v$Version</b>（versionCode $Build）"
# 用 -f 拼 HTML 片段：双引号写进 PowerShell 字符串里很容易多出一个 ""
$cl = $cl -replace '<span class="pill">最近更新 \d{4}-\d{2}-\d{2}</span>',
                   ('<span class="pill">最近更新 {0}</span>' -f $Today)
$cl = $cl -replace '从 v1\.0 首发到 v\d+\.\d+', ('从 v1.0 首发到 v' + $Version)

# 「共 N 个版本」按实际条目数算，比 +1 可靠；
# 但本版 <article> 还没写时不能先改这个数，否则「当前版本 v1.17 / 共 17 个版本」自相矛盾
if ($hasEntry) {
    $articleCount = ([regex]::Matches($cl, '<article class="rel"')).Count
    $cl = $cl -replace '<span class="pill">共 \d+ 个版本</span>',
                       ('<span class="pill">共 {0} 个版本</span>' -f $articleCount)
    $countNote = "版本总数 $articleCount"
} else {
    $countNote = '版本总数保持不变（本版条目未写）'
}

if ($cl -notmatch '当前版本 <b>v\d+\.\d+</b>（versionCode \d+）') {
    Write-Text $ChangelogPath $cl
    Warn 'changelog.html 里找不到「当前版本」那一行，模板可能改过，请手工检查'
} elseif ($cl -eq $before) {
    Ok 'changelog.html 已是目标版本，无需改动'
} else {
    Write-Text $ChangelogPath $cl
    Ok "已更新当前版本、最近更新、$countNote"
}

# ── 10. 漏项检查 ────────────────────────────────────────────
Step '漏项检查'

if (Test-Path $ChangelogPath) {
    if (Select-String -Path $ChangelogPath -Pattern ('id="' + $idTag + '"') -Quiet) {
        Ok "changelog.html 已有 $idTag 条目"
    } else {
        Warn "changelog.html 缺少 id=`"$idTag`" 的 <article class=`"rel`"> 段落——需要手工补本版说明"
    }
}

if (Test-Path $ReleasesPath) {
    if (Select-String -Path $ReleasesPath -Pattern ('new\("' + $Version + '"') -Quiet) {
        Ok "Releases.cs 已有 $Version 条目"
    } else {
        Warn "Releases.cs 缺少 new(`"$Version`", $Build, ...) 条目——服务端 /download.html 不会显示本版"
    }
} else {
    Warn "没找到 Releases.cs：$ReleasesPath"
}

# ── 11. 回填 SEO 元信息 ─────────────────────────────────────
# index.html 的 JSON-LD 里有一份 softwareVersion，sitemap.xml 里有 lastmod。
# 这两处不跟着版本走，搜索引擎看到的就是「页面写着 1.17、结构化数据写着 1.15」。
Step '回填 SEO 元信息'

$tv = Read-Text $IndexPath
$tv2 = $tv -replace '("softwareVersion"\s*:\s*")\d+\.\d+(")', ('${1}' + $Version + '${2}')
if ($tv2 -ne $tv) {
    Write-Text $IndexPath $tv2
    Ok "JSON-LD softwareVersion → $Version"
} elseif ($tv -match '"softwareVersion"') {
    Ok 'JSON-LD softwareVersion 已是目标版本'
} else {
    Warn 'index.html 里没有 "softwareVersion" 字段，结构化数据可能被删了'
}

$SitemapPath = Join-Path $WebsiteDir 'sitemap.xml'
if (Test-Path $SitemapPath) {
    $sm = Read-Text $SitemapPath
    $sm2 = $sm
    # 首页、下载页、更新日志、权限页、关于页每次发版都会变；隐私政策不动，保持原 lastmod
    foreach ($page in @('/', '/download.html', '/changelog.html', '/permissions.html', '/about.html')) {
        $esc  = [regex]::Escape("https://kids.siguangblog.cc$page")
        $expr = '(<loc>' + $esc + '</loc>\s*<lastmod>)\d{4}-\d{2}-\d{2}'
        $sm2  = [regex]::Replace($sm2, $expr, ('${1}' + $Today))
    }
    if ($sm2 -ne $sm) {
        Write-Text $SitemapPath $sm2
        Ok "sitemap.xml lastmod → $Today（首页 / 下载页 / changelog / permissions）"
    } else {
        Ok 'sitemap.xml lastmod 已是今天'
    }
} else {
    Warn '没有 sitemap.xml，搜索引擎只能靠外链发现新页面'
}

# 关于页的「N 个已发布版本」要跟着 changelog 的实际条目数走，
# 否则发到 v1.20 时那一页还写着 18
$AboutPath = Join-Path $WebsiteDir 'about.html'
if ((Test-Path $AboutPath) -and (Test-Path $ChangelogPath)) {
    $ab  = Read-Text $AboutPath
    $cnt = ([regex]::Matches((Read-Text $ChangelogPath), '<article class="rel"')).Count
    $ab2 = [regex]::Replace($ab, '(data-count=")\d+(">0</div><div class="t">个已发布版本)',
                             ('${1}' + $cnt + '${2}'))
    if ($ab2 -ne $ab) {
        Write-Text $AboutPath $ab2
        Ok "about.html 版本数 → $cnt"
    } else {
        Ok "about.html 版本数已是 $cnt"
    }
}

# ── 12. 汇总 ────────────────────────────────────────────────
$apkRel = "Website\downloads\$ApkName"
$zipRel = "Website\downloads\$ZipName"

Write-Host ''
Write-Host '==========================================' -ForegroundColor Cyan
Write-Host "  发布包已生成：v$Version / versionCode $Build" -ForegroundColor Cyan
Write-Host '==========================================' -ForegroundColor Cyan
Write-Host ''
Write-Host "  $apkRel"
Write-Host "    $($apkLen.ToString('N0')) 字节 · 约 $sizeMb MB"
Write-Host "    SHA-256  $apkHash"
Write-Host ''
Write-Host "  $zipRel"
Write-Host "    $($zipLen.ToString('N0')) 字节"
Write-Host "    SHA-256  $zipHash"
Write-Host ''
if ($script:WarnCount -gt 0) {
    Write-Host '  下一步：'
    Write-Host '    1. 补 changelog.html 的本版 <article>（脚本不会自动写内容）'
    Write-Host '    2. 补 KidsSafeServer/Releases.cs 的本版要点'
    Write-Host '    3. 真机装上跑一遍验收清单'
} else {
    Write-Host '  本版条目与发布清单都已就位。建议再在真机上跑一遍验收清单。'
}
Write-Host ''

exit 0
