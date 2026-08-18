param(
    [string]$Version = "26.02"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$tools = Join-Path $root "tools"
$dest = Join-Path $root "third_party\7zip"
New-Item -ItemType Directory -Force -Path $tools, $dest | Out-Null

$tag = $Version
$base = "https://github.com/ip7z/7zip/releases/download/$tag"
$ProgressPreference = "SilentlyContinue"

$sevenZr = Join-Path $tools "7zr.exe"
$installer = Join-Path $tools "7z$($Version.Replace('.',''))-x64.exe"
# 26.02 -> 2602
$numeric = ($Version -split '\.' | ForEach-Object { $_.PadLeft(1, '0') }) -join ''
if ($Version -match '^(\d+)\.(\d+)$') {
    $numeric = '{0}{1:d2}' -f [int]$Matches[1], [int]$Matches[2]
}
$installer = Join-Path $tools "7z$numeric-x64.exe"

Invoke-WebRequest "$base/7zr.exe" -OutFile $sevenZr
Invoke-WebRequest "$base/7z$numeric-x64.exe" -OutFile $installer

$extract = Join-Path $tools "7z-full"
if (Test-Path $extract) { Remove-Item $extract -Recurse -Force }
& $sevenZr x $installer "-o$extract" -y | Out-Host

Copy-Item (Join-Path $extract "7z.exe") (Join-Path $dest "7zz.exe") -Force
Copy-Item (Join-Path $extract "7z.dll") (Join-Path $dest "7z.dll") -Force
Copy-Item (Join-Path $extract "License.txt") (Join-Path $dest "License.txt") -Force

Write-Host "Updated $dest"
