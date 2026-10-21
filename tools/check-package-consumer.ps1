# NuGet 包消费级验证
#
# 验证"下游只加 PackageReference 就能用"，覆盖四件事：
#   1. 打包 NewLife.AI，检查 nupkg 的目标框架资产、README、图标与 AOT 保根 targets 齐全；
#   2. 新建临时项目仅用 PackageReference 引用刚打的包，断言服务商注册表可用；
#   3. 断言包内自带的 31 种词表随包分发（瑞士语回退英文），且下游自带 Locales/*.yml 能自动挂载；
#   4. 裁剪发布（PublishTrimmed 写在应用项目正文里）后运行，验证包内 buildTransitive targets 自动保根、服务商与词表未被裁掉。
#
# 用法：
#   pwsh -File tools/check-package-consumer.ps1
#   pwsh -File tools/check-package-consumer.ps1 -Keep        # 保留临时目录便于排查
#   pwsh -File tools/check-package-consumer.ps1 -SkipTrim    # 跳过裁剪发布（本机无对应 RID 工具链时）
#
# 默认根目录为脚本所在 tools 的上一级（仓库根），临时产物落仓内 .tmp。
param(
    [String]$Configuration = "Release",
    [String]$Framework = "net10.0",
    [String]$Version,
    [Switch]$Keep,
    [Switch]$SkipTrim
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$work = Join-Path $root ".tmp\package-consumer"
$out = Join-Path $work "out"
$consumer = Join-Path $work "Consumer"

if (Test-Path -LiteralPath $work) { Remove-Item -LiteralPath $work -Recurse -Force }
New-Item -ItemType Directory -Force -Path $out | Out-Null

Write-Host "1/5 打包 NewLife.AI ..."
# 每次用唯一版本号，避免 NuGet 全局缓存命中上一次同版本包导致验证的是旧包
if (!$Version) { $Version = "1.0." + (Get-Date -Format "yyyy.MMdd") + "-beta" + (Get-Date -Format "HHmmss") }
& dotnet pack (Join-Path $root "NewLife.AI\NewLife.AI.csproj") -c $Configuration -o $out -p:Version=$Version --nologo
if ($LASTEXITCODE -ne 0) { throw "打包失败，退出码 $LASTEXITCODE" }

$package = Get-ChildItem -LiteralPath $out -Filter "NewLife.AI.$Version.nupkg" | Select-Object -First 1
if (!$package) { throw "未找到 NewLife.AI nupkg" }

Write-Host "   包：$($package.Name)（版本 $Version）"

Write-Host "2/5 检查 nupkg 内容 ..."
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::OpenRead($package.FullName)
try {
    $entries = @($zip.Entries | ForEach-Object { $_.FullName.Replace('\', '/') })
    $required = @(
        "lib/net45/NewLife.AI.dll",
        "lib/netstandard2.0/NewLife.AI.dll",
        "lib/netstandard2.1/NewLife.AI.dll",
        "lib/net8.0/NewLife.AI.dll",
        "lib/net10.0/NewLife.AI.dll",
        "README.nuget.md",
        "leaf.png",
        "buildTransitive/NewLife.AI.targets"
    )
    foreach ($item in $required) {
        if ($entries -notcontains $item) { throw "nupkg 缺少 $item" }
    }

    # 从 nuspec 取 NewLife.Core 依赖版本，供消费项目直接引用（见步骤 3 说明）
    $nuspec = $zip.Entries | Where-Object { $_.FullName -like '*.nuspec' } | Select-Object -First 1
    $reader = New-Object IO.StreamReader($nuspec.Open())
    try { $nuspecText = $reader.ReadToEnd() } finally { $reader.Close() }
    $coreVersion = [regex]::Match($nuspecText, 'id="NewLife\.Core" version="([^"]+)"').Groups[1].Value
    if (!$coreVersion) { throw "nuspec 未声明 NewLife.Core 依赖" }

    Write-Host "   必需项齐全（5 个目标框架 / README / 图标），NewLife.Core 依赖 $coreVersion"
}
finally {
    $zip.Dispose()
}

Write-Host "3/5 创建消费项目（仅 PackageReference）..."
& dotnet new console -n Consumer -o $consumer -f $Framework --no-restore | Out-Null
if ($LASTEXITCODE -ne 0) { throw "创建临时项目失败" }

$nugetConfig = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="$out" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
"@
[IO.File]::WriteAllText((Join-Path $consumer "NuGet.config"), $nugetConfig, (New-Object System.Text.UTF8Encoding($false)))

& dotnet add (Join-Path $consumer "Consumer.csproj") package NewLife.AI --version $version --no-restore | Out-Null
if ($LASTEXITCODE -ne 0) { throw "添加 NewLife.AI 包引用失败" }

# 直接引用 NewLife.Core：PackageReference 默认 PrivateAssets 含 contentfiles;analyzers;build，
# 源生成器与 build props 不随 NewLife.AI 传递，下游要自带词表必须直接引用 Core
& dotnet add (Join-Path $consumer "Consumer.csproj") package NewLife.Core --version $coreVersion --no-restore | Out-Null
if ($LASTEXITCODE -ne 0) { throw "添加 NewLife.Core 包引用失败" }

# 模拟下游组件：自带语言文件，验证 NewLife.Core 的 build props 自动挂载 Locales/*.yml
$locales = Join-Path $consumer "Locales"
New-Item -ItemType Directory -Force -Path $locales | Out-Null
$yaml = @"
"Downstream probe message: {0}.": "கீழ்நிலை ஆய்வுச் செய்தி: {0}."
"Downstream probe without arguments.": "கீழ்நிலை ஆய்வு, அளவுரு இல்லை."
"@
[IO.File]::WriteAllText((Join-Path $locales "ta.yml"), $yaml, (New-Object System.Text.UTF8Encoding($false)))

$csproj = Join-Path $consumer "Consumer.csproj"
$xml = [IO.File]::ReadAllText($csproj)
if ($xml -match '<AdditionalFiles') { throw "消费项目不应手动声明 AdditionalFiles，自动挂载验证失效" }

$program = @"
using System;
using System.Globalization;
using NewLife;
using NewLife.AI.Clients;

// 1. 包内自带的简体中文词表
Localization.Culture = CultureInfo.GetCultureInfo("zh-CN");
const String Key = "Discord message sent";
if (Localization.Translate(Key) != "Discord 发送成功")
{
    Console.Error.WriteLine("zh-Hans mismatch: " + Localization.Translate(Key));
    return 1;
}

// 2. 包内自带的德语词表
Localization.Culture = CultureInfo.GetCultureInfo("de-DE");
if (Localization.Translate(Key) != "Discord-Nachricht gesendet")
{
    Console.Error.WriteLine("de-DE mismatch: " + Localization.Translate(Key));
    return 1;
}

// 3. 未翻译语言回退英文
Localization.Culture = CultureInfo.GetCultureInfo("sw-KE");
if (Localization.Translate(Key) != Key)
{
    Console.Error.WriteLine("Fallback mismatch: " + Localization.Translate(Key));
    return 1;
}

// 4. 内置服务商注册表随包可用
var count = AiClientRegistry.Default.Descriptors.Count;
if (count < 46)
{
    Console.Error.WriteLine("Provider count mismatch: " + count);
    return 1;
}

// 5. 下游自带 Locales/*.yml 自动挂载
const String Probe = "Downstream probe message: {0}.";
Localization.Culture = CultureInfo.GetCultureInfo("ta-IN");
if (Localization.Translate(Probe, 7) != "கீழ்நிலை ஆய்வுச் செய்தி: 7.")
{
    Console.Error.WriteLine("ta-IN mismatch: " + Localization.Translate(Probe, 7));
    return 1;
}

Console.WriteLine("PASS");
return 0;
"@
[IO.File]::WriteAllText((Join-Path $consumer "Program.cs"), $program, (New-Object System.Text.UTF8Encoding($false)))

Write-Host "4/5 编译并运行消费项目 ..."
$output = & dotnet run --project $csproj -c $Configuration 2>&1
$exit = $LASTEXITCODE
$output | ForEach-Object { Write-Host "   $_" }
if ($exit -ne 0) { throw "消费项目运行失败，退出码 $exit" }
if (($output -join "`n") -notmatch "PASS") { throw "消费项目未输出 PASS" }

if ($SkipTrim) {
    Write-Host "5/5 已跳过裁剪发布验证（-SkipTrim）" -ForegroundColor Yellow
}
else {
    Write-Host "5/5 裁剪发布并在裁剪产物上重跑（验证包内 targets 自动保根）..."
    # 把 PublishTrimmed 写进消费项目正文：包内 props 在正文之前导入会取不到该属性，用 .targets 才可靠
    $xml = [IO.File]::ReadAllText($csproj)
    $xml = $xml.Replace('</Project>', "  <PropertyGroup>`r`n    <PublishTrimmed>true</PublishTrimmed>`r`n  </PropertyGroup>`r`n</Project>")
    [IO.File]::WriteAllText($csproj, $xml, (New-Object System.Text.UTF8Encoding($false)))

    $rid = [System.Runtime.InteropServices.RuntimeInformation]::RuntimeIdentifier
    $trimOut = Join-Path $work "trimmed"
    $trimArgs = @("publish", $csproj, "-c", $Configuration, "-r", $rid, "--self-contained", "true", "-o", $trimOut, "--nologo")
    $trimLog = & dotnet @trimArgs 2>&1
    if ($LASTEXITCODE -ne 0) {
        $trimLog | Select-Object -Last 15 | ForEach-Object { Write-Host "   $_" }
        throw "裁剪发布失败，退出码 $LASTEXITCODE"
    }

    $trimExe = Join-Path $trimOut $(if ($IsWindows) { "Consumer.exe" } else { "Consumer" })
    if (!(Test-Path -LiteralPath $trimExe)) { throw "裁剪产物中未找到 $trimExe" }
    $trimOutput = & $trimExe 2>&1
    $trimExit = $LASTEXITCODE
    $trimOutput | ForEach-Object { Write-Host "   $_" }
    if ($trimExit -ne 0) { throw "裁剪产物运行失败，退出码 $trimExit（可能是包内 buildTransitive targets 未生效，服务商或词表被裁掉）" }
    if (($trimOutput -join "`n") -notmatch "PASS") { throw "裁剪产物未输出 PASS" }
    Write-Host "   裁剪产物校验通过（$rid）"
}

Write-Host "通过：下游仅用 PackageReference 即可获得服务商与 31 种词表，裁剪/AOT 发布自动保根" -ForegroundColor Green

if (!$Keep) { Remove-Item -LiteralPath $work -Recurse -Force }
else { Write-Host "临时目录已保留：$work" }
