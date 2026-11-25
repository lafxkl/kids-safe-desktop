<#
.SYNOPSIS
    把家长服务端（KidsSafeServer）部署到服务器，一条命令搞定。

.DESCRIPTION
    做四件事：发布 → 打包（排除体积巨大的 books/downloads）→ 上传解压 → 重启容器 + 健康检查。

    为什么用容器：目标机是 CentOS 7（glibc 2.17），.NET 8+ 的自包含程序在那上面起不来
    （报 GLIBC_2.28 not found）。镜像里自带 glibc，绕开系统版本问题，也不用在服务器上装 SDK。

    访问方式：容器只监听 127.0.0.1:5180，由 nginx 的 80 端口反代 /api/ 与 /patches/。
    所以现在用 http://<服务器IP>/api/health 就能访问；以后加域名，把同样的 location
    复制到域名 vhost 即可，客户端只改「服务器地址」一个地方。

.PARAMETER Server
    root@1.2.3.4。省略时读 Scripts\deploy.config.json 的 server。

.PARAMETER Key
    SSH 私钥路径。省略时读配置里的 key。

.PARAMETER SkipPublish
    不重新发布，直接用上次的产物重传（只调服务器上的东西时用）。

.EXAMPLE
    .\Scripts\deploy-server.ps1
    .\Scripts\deploy-server.ps1 -Server root@43.153.192.19 -SkipPublish
#>

[CmdletBinding()]
param(
    [string]$Server,
    [int]$Port = 0,
    [string]$Key,
    [string]$RemoteDir = '/opt/kidssafe-server',
    [switch]$SkipPublish,
    [string]$ConfigPath = '',
    [string]$AdminKey = ''
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

function Step([string]$text) { Write-Host ""; Write-Host "  ▸ $text" -ForegroundColor Cyan }
function Ok([string]$text) { Write-Host "    ✓ $text" -ForegroundColor Green }
function Fail([string]$text) { Write-Host ""; Write-Host "  ✗ $text" -ForegroundColor Red; exit 1 }

$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $ConfigPath) { $ConfigPath = Join-Path $PSScriptRoot 'deploy.config.json' }

if (Test-Path $ConfigPath) {
    $cfg = Get-Content $ConfigPath -Raw | ConvertFrom-Json
    if (-not $Server) { $Server = $cfg.server }
    if (-not $Key) { $Key = $cfg.key }
    if ($Port -eq 0) { $Port = if ($cfg.port) { [int]$cfg.port } else { 22 } }
}
if ($Port -eq 0) { $Port = 22 }
if (-not $Server) { Fail '没给服务器地址：-Server root@1.2.3.4，或在 deploy.config.json 里配 server' }
if (-not $Key) { Fail '没给 SSH 私钥：-Key C:\path\id_rsa' }
if (-not (Test-Path $Key)) { Fail "私钥不存在：$Key" }

$sshArgs = @('-i', $Key, '-o', 'BatchMode=yes', '-o', 'StrictHostKeyChecking=no', '-p', $Port)
$scpArgs = @('-i', $Key, '-o', 'StrictHostKeyChecking=no', '-P', $Port)

$buildDir = Join-Path $repoRoot '_release\server'
$tarball = Join-Path $repoRoot '_release\server.tar.gz'

if (-not $SkipPublish) {
    Step '发布服务端（框架依赖，跑在 aspnet:10.0 镜像里）'
    # 服务端工程是仓库里的兄弟目录（KidsSafeDesktop/..\KidsSafeServer），不是本工程的子目录
    $project = Join-Path $repoRoot '..\KidsSafeServer\KidsSafeServer.csproj'
    $project = [System.IO.Path]::GetFullPath($project)
    if (-not (Test-Path $project)) { Fail "找不到服务端工程：$project" }

    if (Test-Path $buildDir) { Remove-Item $buildDir -Recurse -Force }
    & dotnet publish $project -c Release -o $buildDir -v q --nologo
    if ($LASTEXITCODE -ne 0) { Fail 'dotnet publish 失败' }
    Ok "发布完成：$buildDir"

    Step '打包（排除 wwwroot\books、wwwroot\downloads 与线上配置）'
    if (Test-Path $tarball) { Remove-Item $tarball -Force }
    Push-Location $buildDir
    try {
        # appsettings.json 必须排除：服务器上那份是「真配置」（Mxnzp 密钥、AdminPin、
        # Feedback:AdminKey 都在里面）。发布目录里的同名文件来自仓库，内容往往不一样，
        # 打进去就会在解压时把线上配置整个覆盖掉 —— 2026-11-24 实际踩过一次。
        & tar -czf $tarball --exclude=./wwwroot/books --exclude=./wwwroot/downloads --exclude=./appsettings.json .
        if ($LASTEXITCODE -ne 0) { Fail 'tar 打包失败' }
    }
    finally { Pop-Location }
    Ok ("包大小 {0:N1} MB" -f ((Get-Item $tarball).Length / 1MB))
}

if (-not (Test-Path $tarball)) { Fail "没有可用的包：$tarball（去掉 -SkipPublish 重新发布）" }

Step "上传到 $Server"
& scp @scpArgs $tarball "${Server}:/tmp/kidssafe-server.tar.gz"
if ($LASTEXITCODE -ne 0) { Fail 'scp 失败' }
Ok '上传完成'

Step '解压并重启容器'

# 反馈管理密钥优先用参数：给了就用环境变量覆盖配置文件，
# 不给就沿用服务器 appsettings.json 里已有的 Feedback:AdminKey。
$envArgs = '-e ASPNETCORE_URLS=http://0.0.0.0:5180 -e ASPNETCORE_ENVIRONMENT=Production'
if ($AdminKey) { $envArgs += " -e Feedback__AdminKey=$AdminKey" }

$remote = @"
set -e
mkdir -p $RemoteDir/app
tar xzf /tmp/kidssafe-server.tar.gz -C $RemoteDir/app
rm -f /tmp/kidssafe-server.tar.gz
mkdir -p $RemoteDir/app/wwwroot/patches $RemoteDir/app/data
docker rm -f kidssafe-server >/dev/null 2>&1 || true
docker run -d --name kidssafe-server --restart unless-stopped \
  -p 127.0.0.1:5180:5180 \
  -v $RemoteDir/app:/app \
  $envArgs \
  -w /app \
  mcr.microsoft.com/dotnet/aspnet:10.0 dotnet KidsSafeServer.dll >/dev/null
sleep 5
echo '--- health ---'
curl -sS --max-time 8 http://127.0.0.1:5180/api/health || (docker logs --tail 30 kidssafe-server; exit 1)
"@
& ssh @sshArgs $Server $remote
if ($LASTEXITCODE -ne 0) { Fail '远端部署失败，看上面 docker logs' }
Ok '容器已重启'

Step '从公网验证（走域名与 IP）'
$hostOnly = ($Server -split '@')[-1]
$domain   = 'kids.siguangblog.cc'

# 域名优先：官网的反馈页 / 评论区都调同源 /api/，这条通则整条链路才算通。
$bases = @("https://$domain", "http://$hostOnly")
$verified = $false
foreach ($base in $bases) {
    try {
        $health = Invoke-RestMethod -Uri "$base/api/health" -TimeoutSec 12
        Ok ("{0}/api/health ok，服务端 {1}，账号 {2} 个，补丁 windows={3} android={4}" -f `
            $base, $health.serverVersion, $health.accountCount, $health.patchVersion.windows, $health.patchVersion.android)
        $verified = $true
        break
    }
    catch {
        Write-Host "    ! $base 访问失败：$($_.Exception.Message)" -ForegroundColor Yellow
    }
}
if (-not $verified) {
    Write-Host '      容器本身已起来的话，检查 nginx 的 location /api/ 与安全组是否放行 80/443。' -ForegroundColor Yellow
}

Write-Host ""
Write-Host "  客户端「家长服务器」地址填：https://$domain" -ForegroundColor Cyan
Write-Host "  反馈管理页：https://$domain/feedback-admin.html（密钥见服务器 appsettings.json 的 Feedback:AdminKey）" -ForegroundColor Cyan
Write-Host "  发补丁：build-patch.bat … 然后把输出目录同步到 $RemoteDir/app/wwwroot/patches/<平台>/current" -ForegroundColor Cyan
Write-Host ""
