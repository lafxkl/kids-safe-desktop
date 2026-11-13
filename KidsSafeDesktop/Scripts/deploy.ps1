<#
.SYNOPSIS
    把 Website 目录发布到线上 nginx（境外服务器，主攻 Bing）。

.DESCRIPTION
    **不备份、不打包**：直接 scp 覆盖远端同名文件。
    只上传官网静态文件，不碰远端其它目录。

    上传完成后会做两件事：
      1. 逐个请求线上地址，确认 200 且内容确实是新版（带 ?v= 时间戳绕缓存）；
      2. 如果 Website 根目录有 IndexNow 密钥文件，自动向 api.indexnow.org 推一遍 URL——
         Bing / Yandex / Seznam 共用这个接口，收录比等爬虫快得多。

.PARAMETER Server
    user@host，必填（或写进 Scripts\deploy.config.json 的 server 字段）。

.PARAMETER RemotePath
    远端站点根目录，默认 /var/www/kids。

.PARAMETER WithDownloads
    连 downloads/ 一起传（APK 与 ZIP，约 250 MB）。只在发新版时加。

.EXAMPLE
    .\deploy.bat -Server root@1.2.3.4
    .\deploy.bat -Server root@1.2.3.4 -Key C:\Users\me\.ssh\id_ed25519
    .\deploy.bat -Server root@1.2.3.4 -WithDownloads
    .\deploy.bat -DryRun          只打印计划，不连服务器
#>
[CmdletBinding()]
param(
    [string]$Server,
    [int]$Port = 22,
    [string]$RemotePath,
    [string]$Key,
    [switch]$WithDownloads,
    [switch]$DryRun,
    [switch]$SkipVerify,
    [switch]$SkipIndexNow
)

$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$Root    = Split-Path -Parent $PSScriptRoot
$WebDir  = Join-Path $Root 'Website'
$Domain  = 'kids.siguangblog.cc'

function Step($m) { Write-Host ''; Write-Host "=== $m ===" -ForegroundColor Cyan }
function Ok($m)   { Write-Host "  [OK] $m" -ForegroundColor Green }
function Info($m) { Write-Host "       $m" -ForegroundColor Gray }
function Warn($m) { Write-Host "  [!]  $m" -ForegroundColor Yellow }
function Fail($m) { Write-Host "  [X]  $m" -ForegroundColor Red; Write-Host ''; exit 1 }

# ── 0. 读取配置 ──────────────────────────────────────────────
Step '读取配置'

$ConfigPath = Join-Path $PSScriptRoot 'deploy.config.json'
if (Test-Path $ConfigPath) {
    $cfg = Get-Content $ConfigPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if (-not $Server)     { $Server     = $cfg.server }
    if (-not $RemotePath) { $RemotePath = $cfg.remotePath }
    if (-not $Key -and $cfg.key) { $Key = $cfg.key }
    if ($cfg.port)        { $Port       = [int]$cfg.port }
    if ($cfg.domain)      { $Domain     = $cfg.domain }
    Ok "已读取 $ConfigPath"
}
if (-not $RemotePath) { $RemotePath = '/var/www/kids' }
$RemotePath = $RemotePath.TrimEnd('/')

if (-not $Server) {
    Fail '缺少服务器地址。用法：.\deploy.bat -Server user@host  或写 Scripts\deploy.config.json'
}
Ok "目标 $Server`:$RemotePath（端口 $Port）"
Ok "域名 https://$Domain"

# ── 1. 检查环境 ──────────────────────────────────────────────
Step '检查环境'

if (-not (Get-Command scp -ErrorAction SilentlyContinue)) { Fail '没找到 scp，请安装 OpenSSH 客户端（Windows 可选功能里勾选）' }
if (-not (Get-Command ssh -ErrorAction SilentlyContinue)) { Fail '没找到 ssh' }
Ok 'ssh / scp 可用'

if (-not (Test-Path $WebDir)) { Fail "找不到 Website 目录：$WebDir" }

# 站点根目录必备的文件。少一个就是线上 404，宁可在这里拦住
$CoreFiles = @(
    'index.html', 'download.html', 'changelog.html', 'permissions.html', 'privacy.html',
    'about.html', 'care-sources.html', '404.html', 'robots.txt', 'sitemap.xml', 'favicon.ico'
)
foreach ($f in $CoreFiles) {
    if (-not (Test-Path (Join-Path $WebDir $f))) { Fail "缺少 $f（站点根目录必备）" }
}
Ok "$($CoreFiles.Count) 个核心文件齐全"

# ── 2. 组装上传清单 ──────────────────────────────────────────
Step '组装上传清单'

$corePaths = @()
foreach ($f in $CoreFiles) { $corePaths += (Join-Path $WebDir $f) }

# IndexNow 密钥文件（32 位十六进制 .txt）必须躺在站点根目录
$keyFile = Get-ChildItem $WebDir -Filter '*.txt' -File |
           Where-Object { $_.Name -match '^[0-9a-f]{32}\.txt$' } |
           Select-Object -First 1
if ($keyFile) {
    $corePaths += $keyFile.FullName
    Ok "IndexNow 密钥文件：$($keyFile.Name)"
} else {
    Warn '没找到 IndexNow 密钥文件，跳过主动推送（Bing 收录会慢一些）'
}

$totalBytes = 0
foreach ($p in $corePaths) { $totalBytes += (Get-Item $p).Length }
foreach ($p in $corePaths) { Info ("{0,-42} {1,8:N0} B" -f (Split-Path $p -Leaf), (Get-Item $p).Length) }

$assetsDir = Join-Path $WebDir 'assets'
$assetsBytes = 0
if (Test-Path $assetsDir) {
    $assetsBytes = (Get-ChildItem $assetsDir -Recurse -File | Measure-Object Length -Sum).Sum
    Info ("{0,-42} {1,8:N0} B" -f 'assets/', $assetsBytes)
}

$dlDir = Join-Path $WebDir 'downloads'
$dlBytes = 0
if (Test-Path $dlDir) {
    $dlBytes = (Get-ChildItem $dlDir -File | Measure-Object Length -Sum).Sum
    if ($WithDownloads) { Info ("{0,-42} {1,8:N0} B" -f 'downloads/（本次上传）', $dlBytes) }
    else { Info 'downloads/ 已跳过（要一起传请加 -WithDownloads）' }
}

$totalBytes += $assetsBytes
if ($WithDownloads) { $totalBytes += $dlBytes }
Ok ("合计约 {0:N1} MB" -f ($totalBytes / 1MB))

# ── 3. 连接参数 ──────────────────────────────────────────────
# 只有在指定了私钥时才用 BatchMode=yes（无交互、失败即停）。
# 没给 -Key 时多半要走密码登录，这时绝不能加 BatchMode——它会让 ssh 直接拒绝问密码，
# 报 "Permission denied (publickey)"，看起来像服务器不让登，实际是自己把密码通道关了。
# 顺带加上 ConnectTimeout 与 3 次重试，弱网下不会一断就整段重来。
$sshArgs = @('-o', 'StrictHostKeyChecking=accept-new', '-o', 'ConnectTimeout=15', '-p', "$Port")
$scpArgs = @('-o', 'StrictHostKeyChecking=accept-new', '-o', 'ConnectTimeout=15', '-P', "$Port")
if ($Key) {
    if (-not (Test-Path $Key)) { Fail "找不到私钥文件：$Key" }
    $sshArgs += @('-o', 'BatchMode=yes', '-i', $Key)
    $scpArgs += @('-o', 'BatchMode=yes', '-i', $Key)
} else {
    Info '未指定 -Key：将走密码登录（上传时会提示输入服务器密码）'
}

if ($DryRun) {
    Step 'DryRun：以下命令没有执行'
    Info ("ssh {0} {1} mkdir -p {2}" -f ($sshArgs -join ' '), $Server, $RemotePath)
    if ($Key) {
        Info ("scp {0} <{1} 个文件> {2}:{3}/" -f ($scpArgs -join ' '), $corePaths.Count, $Server, $RemotePath)
        if (Test-Path $assetsDir) { Info ("scp -r {0} {1} {2}:{3}/" -f ($scpArgs -join ' '), $assetsDir, $Server, $RemotePath) }
        if ($WithDownloads) { Info ("scp -r {0} {1} {2}:{3}/" -f ($scpArgs -join ' '), $dlDir, $Server, $RemotePath) }
        Info '共 3 次密码提示（有私钥，实际为免密）'
    } else {
        $preview = @("$($corePaths.Count) 个核心文件")
        if (Test-Path $assetsDir) { $preview += 'assets/' }
        if ($WithDownloads) { $preview += 'downloads/' }
        Info ("scp -r {0} <{1}> {2}:{3}/" -f ($scpArgs -join ' '), ($preview -join ' + '), $Server, $RemotePath)
        Info '共 2 次密码提示：建目录 1 次，上传 1 次'
    }
    Info ('IndexNow：跳过（DryRun）')
    Write-Host ''
    Ok '计划如上，去掉 -DryRun 即开始上传'
    exit 0
}

# ── 4. 建远端目录 ────────────────────────────────────────────
Step '确认远端目录'

& ssh @sshArgs $Server "mkdir -p '$RemotePath'"
if ($LASTEXITCODE -ne 0) { Fail "ssh 连接失败（退出码 $LASTEXITCODE）。先手动 ssh $Server 试一次，确认密钥/密码不过期" }
Ok "$RemotePath 就绪"

# ── 5. 上传 ──────────────────────────────────────────────────
Step '上传'

# 走密码登录时，每次 ssh/scp 都要重输一遍密码。所以把「核心文件 + assets + downloads」
# 合成一次 scp -r 调用（scp 允许同时给多个源），密码从 4 次降到 2 次。
# 指定了私钥的路由则分开传——反正不用输密码，分开传在大文件失败时更好重试。
$uploadPaths = @($corePaths)
if (Test-Path $assetsDir) { $uploadPaths += $assetsDir }
if ($WithDownloads) { $uploadPaths += $dlDir }

if ($Key) {
    & scp @scpArgs $corePaths "$Server`:$RemotePath/"
    if ($LASTEXITCODE -ne 0) { Fail "核心文件上传失败（退出码 $LASTEXITCODE）" }
    Ok "核心文件 $($corePaths.Count) 个"

    if (Test-Path $assetsDir) {
        & scp @scpArgs -r $assetsDir "$Server`:$RemotePath/"
        if ($LASTEXITCODE -ne 0) { Fail "assets 上传失败（退出码 $LASTEXITCODE）" }
        Ok 'assets/'
    }

    if ($WithDownloads) {
        Info 'downloads/ 约 250 MB，慢一点，别中断……'
        & scp @scpArgs -r $dlDir "$Server`:$RemotePath/"
        if ($LASTEXITCODE -ne 0) { Fail "downloads 上传失败（退出码 $LASTEXITCODE）" }
        Ok 'downloads/'
    }
} else {
    if ($WithDownloads) { Info '一次传完（含 downloads/ 约 250 MB），中途别关窗口……' }
    & scp @scpArgs -r @uploadPaths "$Server`:$RemotePath/"
    if ($LASTEXITCODE -ne 0) { Fail "上传失败（退出码 $LASTEXITCODE）。已传部分文件不会自动回滚，重跑一次即可覆盖" }
    Ok ("已上传 " + $uploadPaths.Count + " 项：核心文件 " + $corePaths.Count + " 个" +
        $(if (Test-Path $assetsDir) { ' + assets/' } else { '' }) +
        $(if ($WithDownloads) { ' + downloads/' } else { '' }))
}

# ── 6. 线上校验 ──────────────────────────────────────────────
if (-not $SkipVerify) {
    Step '线上校验'

    $stamp  = [DateTime]::UtcNow.Ticks
    $checks = @(
        @{ Path = '/';             Must = '儿童安全桌面' },
        @{ Path = '/about.html';   Must = '三条不做' },
        @{ Path = '/changelog.html'; Must = '更新日志' },
        @{ Path = '/robots.txt';   Must = 'Sitemap:' },
        @{ Path = '/sitemap.xml';  Must = '<urlset' },
        @{ Path = '/404.html';     Must = '这个页面走丢了' },
        @{ Path = '/favicon.ico';  Must = $null }
    )

    # 用 WebClient 并按 UTF-8 解码：Invoke-WebRequest 在响应头没有 charset 时会按
    # ISO-8859-1 解，中文关键词永远匹配不上（nginx 静态页就是这种头），
    # 于是每页都报「内容检查未命中」，实际页面是好的。别再改回 Invoke-WebRequest。
    function Get-PageText([string]$u) {
        $wc = New-Object System.Net.WebClient
        $wc.Encoding = [System.Text.Encoding]::UTF8
        $wc.Headers.Add('User-Agent', 'KidsSafeDeployCheck/1.0')
        $wc.Headers.Add('Cache-Control', 'no-cache')
        try { return $wc.DownloadString($u) } finally { $wc.Dispose() }
    }

    $bad = 0
    foreach ($c in $checks) {
        $url = "https://$Domain$($c.Path)?v=$stamp"
        try {
            $body   = Get-PageText $url
            $okBody = $true
            if ($c.Must) { $okBody = $body.Contains($c.Must) }
            if ($okBody) {
                Ok ("{0,-22} 200" -f $c.Path)
            } else {
                $bad++
                Warn ("{0,-22} 200 但内容未命中「{1}」（页面可能是旧版）" -f $c.Path, $c.Must)
            }
        } catch {
            $bad++
            Warn ("{0,-22} 请求失败：{1}" -f $c.Path, $_.Exception.Message)
        }
    }

    # 密钥文件必须能被读到，否则 IndexNow 一定报 403
    if ($keyFile) {
        try {
            $k = (Invoke-WebRequest -Uri "https://$Domain/$($keyFile.Name)?v=$stamp" -UseBasicParsing -TimeoutSec 20).Content
            if (([string]$k).Trim() -eq (Get-Content $keyFile.FullName -Raw).Trim()) { Ok 'IndexNow 密钥文件可读' }
            else { Warn 'IndexNow 密钥文件内容与本地不一致' }
        } catch {
            Warn "IndexNow 密钥文件读取失败：$($_.Exception.Message)"
        }
    }

    if ($bad -gt 0) {
        Warn "$bad 个地址校验没过。若页面仍是旧版，先确认 nginx 的 root 指向的就是 $RemotePath"
    } else {
        Ok '全部通过'
    }
} else {
    Step '线上校验'; Info '已跳过（-SkipVerify）'
}

# ── 7. IndexNow 主动推送 ────────────────────────────────────
if (-not $SkipIndexNow -and $keyFile) {
    Step 'IndexNow 推送给 Bing'

    $key  = (Get-Content $keyFile.FullName -Raw).Trim()
    $urls = @()
    $sm   = Get-Content (Join-Path $WebDir 'sitemap.xml') -Raw -Encoding UTF8
    foreach ($m in [regex]::Matches($sm, '<loc>(.*?)</loc>')) { $urls += $m.Groups[1].Value }

    if ($urls.Count -eq 0) {
        Warn 'sitemap.xml 里没有 <loc>，跳过推送'
    } else {
        $payload = @{
            host        = $Domain
            key         = $key
            keyLocation = "https://$Domain/$($keyFile.Name)"
            urlList     = $urls
        } | ConvertTo-Json -Depth 4 -Compress

        try {
            $resp = Invoke-WebRequest -Uri 'https://api.indexnow.org/indexnow' -Method Post `
                        -ContentType 'application/json; charset=utf-8' `
                        -Body ([Text.Encoding]::UTF8.GetBytes($payload)) `
                        -UseBasicParsing -TimeoutSec 25
            Ok "已推送 $($urls.Count) 个 URL（HTTP $($resp.StatusCode)）"
            $urls | ForEach-Object { Info $_ }
        } catch {
            $code = $null
            if ($_.Exception.Response) { $code = [int]$_.Exception.Response.StatusCode }
            Warn "推送失败（HTTP $code）：$($_.Exception.Message)"
            Info '常见原因：密钥文件还没生效 / 域名与实际访问域名不一致。等 1 分钟重试即可，不影响已上传的文件'
        }
    }
} elseif (-not $SkipIndexNow) {
    Step 'IndexNow 推送给 Bing'; Info '没有密钥文件，跳过'
} else {
    Step 'IndexNow 推送给 Bing'; Info '已跳过（-SkipIndexNow）'
}

# ── 8. 收尾 ──────────────────────────────────────────────────
Write-Host ''
Write-Host '==========================================' -ForegroundColor Cyan
Write-Host "  部署完成：https://$Domain" -ForegroundColor Cyan
Write-Host '==========================================' -ForegroundColor Cyan
Write-Host ''
Write-Host '  下一步（Bing 为主）：'
Write-Host '    1. Bing Webmaster Tools → 站点 → 站点地图 → 提交 sitemap.xml'
Write-Host '    2. 首次部署请把 msvalidate.01 验证码填进 about.html 的注释处'
Write-Host '    3. 抓取 → URL 检查 → 对 about.html 手动请求编入索引'
Write-Host ''
exit 0
