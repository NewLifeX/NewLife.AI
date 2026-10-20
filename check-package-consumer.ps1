# NuGet 包消费级验证
#
# 验证"下游只加 PackageReference 就能用"，覆盖三件事：
#   1. 打包 NewLife.AI，检查 nupkg 的目标框架资产、README 与图标齐全；
#   2. 新建临时项目仅用 PackageReference 引用刚打的包，断言服务商注册表可用；
#   3. 断言包内自带的 31 种词表随包分发（瑞士语回退英文），且下游自带 Locales/*.yml 能自动挂载。
#
# 用法：
#   pwsh -File check-package-consumer.ps1
#   pwsh -File check-package-consumer.ps1 -Keep      # 保留临时目录便于排查
param(
    [String]$Configuration = "Release",
    [String]$Framework = "net10.0",
    [String]$Version,
    [Switch]$Keep
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$work = Join-Path $root ".tmp\package-consumer"
$out = Join-Path $work "out"
$consumer = Join-Path $work "Consumer"

if (Test-Path -LiteralPath $work) { Remove-Item -LiteralPath $work -Recurse -Force }
New-Item -ItemType Directory -Force -Path $out | Out-Null

Write-Host "1/4 打包 NewLife.AI ..."
# 每次用唯一版本号，避免 NuGet 全局缓存命中上一次同版本包导致验证的是旧包
if (!$Version) { $Version = "1.0." + (Get-Date -Format "yyyy.MMdd") + "-beta" + (Get-Date -Format "HHmmss") }
& dotnet pack (Join-Path $root "NewLife.AI\NewLife.AI.csproj") -c $Configuration -o $out -p:Version=$Version --nologo
if ($LASTEXITCODE -ne 0) { throw "打包失败，退出码 $LASTEXITCODE" }

$package = Get-ChildItem -LiteralPath $out -Filter "NewLife.AI.$Version.nupkg" | Select-Object -First 1
if (!$package) { throw "未找到 NewLife.AI nupkg" }

Write-Host "   包：$($package.Name)（版本 $Version）"

Write-Host "2/4 检查 nupkg 内容 ..."
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
        "leaf.png"
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

Write-Host "3/4 创建消费项目（仅 PackageReference）..."
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

Write-Host "4/4 编译并运行消费项目 ..."
$output = & dotnet run --project $csproj -c $Configuration 2>&1
$exit = $LASTEXITCODE
$output | ForEach-Object { Write-Host "   $_" }
if ($exit -ne 0) { throw "消费项目运行失败，退出码 $exit" }
if (($output -join "`n") -notmatch "PASS") { throw "消费项目未输出 PASS" }

Write-Host "通过：下游仅用 PackageReference 即可获得服务商与 31 种词表" -ForegroundColor Green

if (!$Keep) { Remove-Item -LiteralPath $work -Recurse -Force }
else { Write-Host "临时目录已保留：$work" }
