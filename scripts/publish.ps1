param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$Output = "",
    [switch]$SkipFetch
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$sevenZipDir = Join-Path $root "third_party\7zip"
$sevenZip = Join-Path $sevenZipDir "7zz.exe"
$sevenZipDll = Join-Path $sevenZipDir "7z.dll"

if (-not $SkipFetch -and (-not (Test-Path $sevenZip) -or -not (Test-Path $sevenZipDll))) {
    Write-Host "Bundled 7-Zip is missing. Downloading..."
    & (Join-Path $PSScriptRoot "fetch-7zip.ps1")
}

if (-not (Test-Path $sevenZip) -or -not (Test-Path $sevenZipDll)) {
    throw "7-Zip backend is missing. Run scripts/fetch-7zip.ps1 and retry."
}

$outDir = if ($Output) { $Output } else { Join-Path $root "dist\CleanExtract-$Runtime" }
$iconPng = Join-Path $root "assets\icon.png"
$iconIco = Join-Path $root "assets\app.ico"
if (-not (Test-Path $iconIco) -and (Test-Path $iconPng)) {
    Write-Host "Building app.ico from icon.png..."
    & (Join-Path $PSScriptRoot "make-icon.ps1")
}
if (Test-Path $outDir) {
    Remove-Item $outDir -Recurse -Force
}

$project = Join-Path $root "src\CleanExtract.App\CleanExtract.csproj"
Write-Host "Publishing $project -> $outDir"

dotnet publish $project `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -o $outDir `
    -p:PublishTrimmed=false `
    -p:DebugType=None `
    -p:DebugSymbols=false
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

$exe = Join-Path $outDir "CleanExtract.exe"
$publishedSevenZip = Join-Path $outDir "resources\7zz.exe"
$publishedDll = Join-Path $outDir "resources\7z.dll"
if (-not (Test-Path $exe)) {
    throw "Publish did not produce CleanExtract.exe."
}
if (-not (Test-Path $publishedSevenZip) -or -not (Test-Path $publishedDll)) {
    throw "Publish is missing bundled 7-Zip (resources/7zz.exe and resources/7z.dll)."
}

Write-Host "Published self-contained build to $outDir"
Write-Host "Run: $exe"
& (Join-Path $PSScriptRoot "sign.ps1") -Path $exe
