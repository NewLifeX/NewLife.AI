# README 指标同步与校验
#
# 单一事实源：readme-metrics.json。AOT 冒烟场景数、TFM 数量、内置语言数等
# 会随发布变化的数字，不再散落在 11 份 README 里手工维护。
#
# 用法：
#   pwsh -File sync-readme-metrics.ps1            # 按配置写入指标徽章块
#   pwsh -File sync-readme-metrics.ps1 -Check     # 只校验不写入，不一致退出码 1（CI 使用）
param(
    [Switch]$Check,
    [String]$Root = $PSScriptRoot,
    [String]$Config = "readme-metrics.json"
)

$ErrorActionPreference = "Stop"

$configPath = Join-Path $Root $Config
if (!(Test-Path -LiteralPath $configPath)) { throw "未找到指标配置：$configPath" }

$metrics = Get-Content -LiteralPath $configPath -Raw -Encoding UTF8 | ConvertFrom-Json
$values = @{
    "aot-smoke"    = [String]$metrics.values.aotSmoke
    "tfm-count"    = [String]$metrics.values.tfmCount
    "locale-count" = [String]$metrics.values.localeCount
}

$files = @(Get-ChildItem -Path $Root -File | Where-Object {
    $_.Name -match '^(README|Readme)(\.[A-Za-z-]+)?\.md$' -and $_.Name -notmatch '\.nuget\.md$'
} | Sort-Object Name)
if ($files.Count -eq 0) { throw "根目录未找到 README 文件" }

$StartMarker = "<!-- metrics:start -->"
$EndMarker = "<!-- metrics:end -->"
$BlockPattern = "(?s)" + [regex]::Escape($StartMarker) + ".*?" + [regex]::Escape($EndMarker)

# AOT 冒烟徽章里的斜杠需要 URL 编码，否则 shields.io 会把斜杠当路径分隔符
$smokeEncoded = [Uri]::EscapeDataString($values["aot-smoke"])

function Get-MetricsBlock([String]$newline) {
    $lines = @(
        $StartMarker
        "[![AOT smoke](https://img.shields.io/badge/AOT%20smoke-$smokeEncoded-brightgreen?logo=dotnet)](Doc/AOT兼容性分析.md)"
        "[![TFM](https://img.shields.io/badge/TFM-$($values["tfm-count"])%20%28net45%20%E2%86%92%20net10.0%29-512BD4?logo=dotnet)](https://www.nuget.org/packages/NewLife.AI)"
        "[![locales](https://img.shields.io/badge/locales-$($values["locale-count"])-2ea44f?logo=dotnet)](Doc/多语言本地化.md)"
        $EndMarker
    )
    return $lines -join $newline
}

$errors = 0
$changed = 0
foreach ($file in $files) {
    $path = $file.FullName
    $text = [IO.File]::ReadAllText($path)

    if (!$text.Contains($StartMarker) -or !$text.Contains($EndMarker)) {
        $errors++
        Write-Host "✗ $($file.Name) 缺少指标标记块"
        continue
    }

    $newline = if ($text.Contains("`r`n")) { "`r`n" } else { "`n" }
    $block = Get-MetricsBlock $newline
    $newText = [regex]::Replace($text, $BlockPattern, [System.Text.RegularExpressions.MatchEvaluator] {
        param($m) return $block
    })

    if ($text -eq $newText) { continue }

    if ($Check) {
        $errors++
        Write-Host "✗ $($file.Name)"
        Write-Host "  指标块与 readme-metrics.json 不一致，请运行 pwsh -File sync-readme-metrics.ps1"
    }
    else {
        [IO.File]::WriteAllText($path, $newText, (New-Object System.Text.UTF8Encoding($false)))
        $changed++
        Write-Host "✓ $($file.Name)"
    }
}

if ($Check) {
    if ($errors -gt 0) {
        Write-Host "`nREADME 指标不一致：$errors / $($files.Count) 个文件。"
        exit 1
    }
    Write-Host "README 指标一致（$($files.Count) 个文件，AOT $($values["aot-smoke"])，TFM $($values["tfm-count"])，语言 $($values["locale-count"])）"
}
elseif ($changed -eq 0) {
    Write-Host "README 指标已是最新（$($files.Count) 个文件）"
}
