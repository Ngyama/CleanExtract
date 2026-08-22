param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$SkipFetch,
    [switch]$SkipPack
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$iconPng = Join-Path $root "assets\icon.png"
$iconIco = Join-Path $root "assets\app.ico"

if (-not (Test-Path $iconIco) -or ((Test-Path $iconPng) -and ((Get-Item $iconPng).LastWriteTime -gt (Get-Item $iconIco).LastWriteTime))) {
    Write-Host "Building app.ico from icon.png..."
    & (Join-Path $PSScriptRoot "make-icon.ps1")
}

$publishDir = Join-Path $root "dist\CleanExtract-$Runtime"
& (Join-Path $PSScriptRoot "publish.ps1") -Configuration $Configuration -Runtime $Runtime -Output $publishDir -SkipFetch:$SkipFetch

$exe = Join-Path $publishDir "CleanExtract.exe"
& (Join-Path $PSScriptRoot "sign.ps1") -Path $exe

if ($SkipPack) {
    Write-Host "Skipping installer pack."
    exit 0
}

Push-Location $root
try {
    dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw "dotnet tool restore failed." }

    $csproj = Join-Path $root "src\CleanExtract.App\CleanExtract.csproj"
    $version = (Select-String -Path $csproj -Pattern '<Version>([^<]+)</Version>').Matches[0].Groups[1].Value
    $releases = Join-Path $root "dist\releases"
    New-Item -ItemType Directory -Force -Path $releases | Out-Null

    $packArgs = @(
        "tool", "run", "vpk", "--",
        "pack",
        "--packId", "CleanExtract",
        "--packTitle", "Clean Extract",
        "--packAuthors", "Clean Extract",
        "--packVersion", $version,
        "--packDir", $publishDir,
        "--mainExe", "CleanExtract.exe",
        "--outputDir", $releases,
        "--icon", $iconIco
    )

    if ($env:CLEANEXTRACT_SIGN_PFX -or $env:CLEANEXTRACT_SIGN_THUMBPRINT) {
        $timestamp = if ($env:CLEANEXTRACT_SIGN_TIMESTAMP) { $env:CLEANEXTRACT_SIGN_TIMESTAMP } else { "http://timestamp.digicert.com" }
        $signParams = "/fd SHA256 /td SHA256 /tr $timestamp"
        if ($env:CLEANEXTRACT_SIGN_PFX) {
            $signParams += " /f `"$($env:CLEANEXTRACT_SIGN_PFX)`""
            if ($env:CLEANEXTRACT_SIGN_PASSWORD) {
                $signParams += " /p `"$($env:CLEANEXTRACT_SIGN_PASSWORD)`""
            }
        }
        else {
            $signParams += " /sha1 $($env:CLEANEXTRACT_SIGN_THUMBPRINT)"
        }
        $packArgs += @("--signParams", $signParams)
    }

    Write-Host "Packing installer with Velopack..."
    & dotnet @packArgs
    if ($LASTEXITCODE -ne 0) {
        throw "vpk pack failed with exit code $LASTEXITCODE."
    }

    $setup = Get-ChildItem $releases -Filter "*Setup.exe" -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($setup) {
        & (Join-Path $PSScriptRoot "sign.ps1") -Path $setup.FullName
        Write-Host "Installer: $($setup.FullName)"
    }
    else {
        Write-Host "Velopack releases: $releases"
    }
}
finally {
    Pop-Location
}
