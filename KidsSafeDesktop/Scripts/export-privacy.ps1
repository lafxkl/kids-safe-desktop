# =============================================================================
#  export-privacy.ps1
#
#  从 Models/ComplianceContent.cs 里抽取隐私政策全文，生成安装程序用的
#  Installer/PrivacyPolicy.txt。
#
#  为什么要抽：政策正文只有一处（应用内「隐私政策」页），安装界面必须显示
#  同一份内容。手抄一份到安装器，迟早会和应用里不一致——审核和用户都会看见。
#
#  用法： powershell -File Scripts/export-privacy.ps1
# =============================================================================

$ErrorActionPreference = 'Stop'

$Root = Split-Path -Parent $PSScriptRoot
$Source = Join-Path $Root 'Models\ComplianceContent.cs'
# 合规正文已经拆到 KidsSafeShared（孩子端与家长端共用），老路径留作兜底，
# 免得以后又把脚本改回单工程结构时这里静默失效。
if (-not (Test-Path $Source)) {
    $Source = Join-Path (Split-Path -Parent $Root) 'KidsSafeShared\Models\ComplianceContent.cs'
}
$OutDir = Join-Path $Root 'Installer'
$OutFile = Join-Path $OutDir 'PrivacyPolicy.txt'

if (-not (Test-Path $Source)) { throw "找不到 $Source" }

$src = Get-Content -Raw -Encoding UTF8 $Source

$start = $src.IndexOf('PolicySections')
if ($start -lt 0) { throw 'ComplianceContent.cs 里找不到 PolicySections' }
$end = $src.IndexOf("`n    };", $start)
if ($end -lt 0) { $end = $src.IndexOf('};', $start) }
$block = $src.Substring($start, $end - $start)

$sectionRx = [regex]'new\("(\d+)",\s*"([^"]*)",\s*"([^"]*)",([\s\S]*?)\)\s*,'
$matches = $sectionRx.Matches($block)
if ($matches.Count -eq 0) { throw '没有解析到任何政策章节' }

$sb = [System.Text.StringBuilder]::new()
[void]$sb.AppendLine('儿童安全桌面 · 隐私政策')
[void]$sb.AppendLine('版本 1.0    生效日期 2026 年 11 月 4 日    适用平台 安卓 / Windows')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('摘要：孩子的信息默认只留在这台设备上，不收集身份信息，不投放广告，不向第三方提供。')
[void]$sb.AppendLine('只有你主动开启「家长端服务器」时，配置才会同步到你自己填写的服务器。')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('请在继续安装前完整阅读。安装程序下方的「我接受协议」即表示你已阅读并同意本政策。')

foreach ($m in $matches) {
    $no = $m.Groups[1].Value
    $title = $m.Groups[3].Value
    $bodyRaw = $m.Groups[4].Value

    # 正文是若干个 C# 字符串用 + 拼起来的，逐段取出来再拼回一段文本
    $parts = [regex]::Matches($bodyRaw, '"((?:[^"\\]|\\.)*)"') |
             ForEach-Object { $_.Groups[1].Value }
    $body = ($parts -join '')
    $body = $body -replace '\\n', "`r`n"
    $body = $body -replace '\\"', '"'
    $body = $body -replace '\\\\', '\'

    [void]$sb.AppendLine('')
    [void]$sb.AppendLine('=' * 64)
    [void]$sb.AppendLine("$no  $title")
    [void]$sb.AppendLine('=' * 64)
    [void]$sb.AppendLine($body.TrimEnd())
}

[void]$sb.AppendLine('')
[void]$sb.AppendLine('=' * 64)
[void]$sb.AppendLine('联系我们')
[void]$sb.AppendLine('=' * 64)
[void]$sb.AppendLine('官网：https://kids.siguangblog.cc/')
[void]$sb.AppendLine('应用内也可随时查看：家长面板 → 隐私与合规 → 隐私政策。')

if (-not (Test-Path $OutDir)) { New-Item -ItemType Directory -Path $OutDir | Out-Null }
# Inno Setup 读 UTF-8 需要 BOM，否则中文会乱码
[System.IO.File]::WriteAllText($OutFile, $sb.ToString(), [System.Text.UTF8Encoding]::new($true))

Write-Host "[OK] 已生成 $OutFile（$($matches.Count) 节）"
