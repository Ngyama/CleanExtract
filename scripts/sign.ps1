param(
    [Parameter(Mandatory = $true)]
    [string[]]$Path,
    [switch]$Required
)

$ErrorActionPreference = "Stop"

function Find-SignTool {
    $cmd = Get-Command signtool.exe -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }

    $kits = Join-Path ${env:ProgramFiles(x86)} "Windows Kits\10\bin"
    if (Test-Path $kits) {
        $found = Get-ChildItem $kits -Recurse -Filter signtool.exe -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match '\\x64\\signtool.exe$' } |
            Sort-Object FullName -Descending |
            Select-Object -First 1
        if ($found) { return $found.FullName }
    }

    return $null
}

$signTool = Find-SignTool
if (-not $signTool) {
    $message = "signtool.exe was not found. Install the Windows SDK to sign binaries."
    if ($Required) { throw $message }
    Write-Host $message
    exit 0
}

$files = @()
foreach ($item in $Path) {
    if (Test-Path $item) {
        $files += (Resolve-Path $item).Path
    }
}

if ($files.Count -eq 0) {
    if ($Required) { throw "No files to sign." }
    Write-Host "No files to sign."
    exit 0
}

$timestamp = if ($env:CLEANEXTRACT_SIGN_TIMESTAMP) { $env:CLEANEXTRACT_SIGN_TIMESTAMP } else { "http://timestamp.digicert.com" }
$args = @("sign", "/fd", "SHA256", "/td", "SHA256", "/tr", $timestamp)

if ($env:CLEANEXTRACT_SIGN_PFX) {
    if (-not (Test-Path $env:CLEANEXTRACT_SIGN_PFX)) {
        throw "CLEANEXTRACT_SIGN_PFX does not exist: $($env:CLEANEXTRACT_SIGN_PFX)"
    }
    $args += @("/f", $env:CLEANEXTRACT_SIGN_PFX)
    if ($env:CLEANEXTRACT_SIGN_PASSWORD) {
        $args += @("/p", $env:CLEANEXTRACT_SIGN_PASSWORD)
    }
}
elseif ($env:CLEANEXTRACT_SIGN_THUMBPRINT) {
    $args += @("/sha1", $env:CLEANEXTRACT_SIGN_THUMBPRINT)
}
else {
    $message = "No signing certificate configured. Set CLEANEXTRACT_SIGN_PFX or CLEANEXTRACT_SIGN_THUMBPRINT."
    if ($Required) { throw $message }
    Write-Host $message
    exit 0
}

$failed = $false
foreach ($file in $files) {
    Write-Host "Signing $file"
    & $signTool @args $file
    if ($LASTEXITCODE -ne 0) {
        $failed = $true
        Write-Host "signtool failed for $file with exit code $LASTEXITCODE"
    }
}

if ($failed) {
    throw "One or more files could not be signed."
}
