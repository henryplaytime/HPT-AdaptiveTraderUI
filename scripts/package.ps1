# 打包发布版脚本 / Package a release zip
#
# 前置：先运行 scripts\build.ps1，或确保 artifacts\Kaeno-AdaptiveTraderUI.dll 存在。
#
# 用法 / Usage:
#   powershell -NoProfile -ExecutionPolicy Bypass -File scripts\package.ps1
#   （PowerShell 7 也可：pwsh -File scripts\package.ps1）
#
# 产物 / Output:
#   artifacts\Kaeno-AdaptiveTraderUI-v<版本>.zip
#     ├─ BepInEx\plugins\Kaeno-AdaptiveTraderUI.dll
#     ├─ README.md
#     ├─ LICENSE
#     ├─ THIRD-PARTY-NOTICES.md
#     ├─ CHANGELOG.md
#     ├─ config-sample\com.kaeno.TraderScrolling.cfg.sample
#     └─ SHA256SUMS.txt

param(
    [string]$Root = ''
)

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($Root)) {
    $Root = Split-Path -Parent $PSScriptRoot
}

$csproj = Join-Path $Root 'SPT-Kaeno-AdaptiveTraderUI.csproj'
$artifacts = Join-Path $Root 'artifacts'
$dll = Join-Path $artifacts 'Kaeno-AdaptiveTraderUI.dll'

if (-not (Test-Path -LiteralPath $dll)) {
    throw "找不到 $dll，请先运行 scripts\build.ps1"
}

$version = '0.0.0'
try {
    $text = Get-Content -LiteralPath $csproj -Raw -Encoding UTF8
    if ($text -match '<Version>([^<]+)</Version>') {
        $version = $Matches[1].Trim()
    }
} catch {
}

$stage = Join-Path $artifacts ('_stage_v' + $version)
if (Test-Path -LiteralPath $stage) {
    Remove-Item -LiteralPath $stage -Recurse -Force
}

$pluginDir = Join-Path $stage 'BepInEx\plugins'
New-Item -ItemType Directory -Force -Path $pluginDir | Out-Null
Copy-Item -LiteralPath $dll -Destination (Join-Path $pluginDir 'Kaeno-AdaptiveTraderUI.dll') -Force

$docs = @('README.md', 'LICENSE', 'THIRD-PARTY-NOTICES.md', 'CHANGELOG.md')
foreach ($doc in $docs) {
    $src = Join-Path $Root $doc
    if (Test-Path -LiteralPath $src) {
        Copy-Item -LiteralPath $src -Destination (Join-Path $stage $doc) -Force
    }
}

$sample = Join-Path $Root 'config\com.kaeno.TraderScrolling.cfg.sample'
if (Test-Path -LiteralPath $sample) {
    Copy-Item -LiteralPath $sample -Destination (Join-Path $stage 'com.kaeno.TraderScrolling.cfg.sample') -Force
}

$hash = (Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash
@(
    "SHA256 (BepInEx/plugins/Kaeno-AdaptiveTraderUI.dll) = $hash",
    '',
    "# 说明：把本压缩包内的 BepInEx 文件夹解压到 SPT 游戏根目录；",
    '# 旧的 Kaeno-TraderScrolling.dll 必须先删除或改名（本插件沿用了它的 GUID）。'
) | Set-Content -LiteralPath (Join-Path $stage 'SHA256SUMS.txt') -Encoding UTF8

$zip = Join-Path $artifacts ("Kaeno-AdaptiveTraderUI-v$version.zip")
if (Test-Path -LiteralPath $zip) {
    Remove-Item -LiteralPath $zip -Force
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::Open($zip, 'Create')
try {
    $files = Get-ChildItem -LiteralPath $stage -Recurse -File
    foreach ($file in $files) {
        $entry = $file.FullName.Substring($stage.Length + 1).Replace('\', '/')
        [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file.FullName, $entry)
    }
} finally {
    $archive.Dispose()
}

Remove-Item -LiteralPath $stage -Recurse -Force

Write-Host "OK: $zip" -ForegroundColor Green
Get-Item -LiteralPath $zip | Select-Object FullName, Length, LastWriteTime | Format-List

