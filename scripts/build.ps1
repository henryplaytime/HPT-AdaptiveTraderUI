# 构建脚本 / Build script
#
# 用法 / Usage（Windows PowerShell 5.1 或 PowerShell 7 均可）：
#   powershell -NoProfile -ExecutionPolicy Bypass -File scripts\build.ps1 -GameDir "D:\SPT\Escape From Tarkov_4.1.6"
#   （PowerShell 7 也可：pwsh -File scripts\build.ps1 -GameDir "...")
#   $env:SPT_GAME_DIR = "D:\SPT\Escape From Tarkov_4.1.6"; powershell -File scripts\build.ps1
#
# 产物 / Output:
#   artifacts\HPT-AdaptiveTraderUI.dll
#   （构建中间产物仍在 bin\ 与 obj\，已加入 .gitignore）

param(
    [string]$GameDir = $env:SPT_GAME_DIR,
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$csproj = Join-Path $root 'HPT-AdaptiveTraderUI.csproj'
$artifacts = Join-Path $root 'artifacts'
$assemblyName = 'HPT-AdaptiveTraderUI'

if (-not (Test-Path -LiteralPath $csproj)) {
    throw "找不到 csproj: $csproj"
}

if ([string]::IsNullOrWhiteSpace($GameDir)) {
    Write-Host '未指定 SPT 安装目录。' -ForegroundColor Yellow
    Write-Host '请这样使用 / Usage:' -ForegroundColor Yellow
    Write-Host '  powershell -NoProfile -ExecutionPolicy Bypass -File scripts\build.ps1 -GameDir "D:\你的SPT目录\Escape From Tarkov_4.1.6"' -ForegroundColor Yellow
    Write-Host '或设置环境变量 SPT_GAME_DIR 后直接运行本脚本。' -ForegroundColor Yellow
    exit 1
}

if (-not (Test-Path -LiteralPath $GameDir)) {
    throw "GameDir 不存在: $GameDir"
}

Write-Host '== HPT-AdaptiveTraderUI build ==' -ForegroundColor Cyan
Write-Host "GameDir = $GameDir"
Write-Host "csproj  = $csproj"

dotnet build $csproj -c $Configuration -p:GameDir="$GameDir"
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build 失败（exit $LASTEXITCODE）"
}

$candidates = @(
    (Join-Path $root "bin\$Configuration\$assemblyName.dll"),
    (Join-Path $root "bin\$Configuration\netstandard2.1\$assemblyName.dll")
)
$dll = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $dll) {
    $dll = Get-ChildItem -Path (Join-Path $root 'bin') -Recurse -Filter "$assemblyName.dll" -ErrorAction SilentlyContinue |
        Select-Object -First 1 -ExpandProperty FullName
}
if (-not $dll) {
    throw "构建成功但找不到 $assemblyName.dll"
}

New-Item -ItemType Directory -Force -Path $artifacts | Out-Null
$outDll = Join-Path $artifacts "$assemblyName.dll"
Copy-Item -LiteralPath $dll -Destination $outDll -Force

Write-Host "OK: $outDll" -ForegroundColor Green
Get-FileHash -LiteralPath $outDll -Algorithm SHA256 | Format-List

