param(
    [string]$Png = "",
    [string]$Ico = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
if (-not $Png) { $Png = Join-Path $root "assets\icon.png" }
if (-not $Ico) { $Ico = Join-Path $root "assets\app.ico" }

if (-not (Test-Path $Png)) {
    throw "Icon PNG not found: $Png"
}

Add-Type -AssemblyName System.Drawing

$source = [System.Drawing.Image]::FromFile((Resolve-Path $Png))
try {
    $sizes = @(16, 24, 32, 48, 64, 128, 256)
    $frames = New-Object System.Collections.Generic.List[byte[]]
    foreach ($size in $sizes) {
        $bitmap = New-Object System.Drawing.Bitmap $size, $size
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.Clear([System.Drawing.Color]::Transparent)
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
            $graphics.DrawImage($source, 0, 0, $size, $size)
            $stream = New-Object System.IO.MemoryStream
            $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
            $frames.Add($stream.ToArray())
        }
        finally {
            $graphics.Dispose()
            $bitmap.Dispose()
        }
    }
}
finally {
    $source.Dispose()
}

$count = $frames.Count
$headerSize = 6
$entrySize = 16
$dataOffset = $headerSize + ($entrySize * $count)

$stream = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter $stream
$writer.Write([uint16]0)
$writer.Write([uint16]1)
$writer.Write([uint16]$count)

$offset = $dataOffset
for ($i = 0; $i -lt $count; $i++) {
    $size = $sizes[$i]
    $bytes = $frames[$i]
    $widthByte = if ($size -ge 256) { [byte]0 } else { [byte]$size }
    $heightByte = if ($size -ge 256) { [byte]0 } else { [byte]$size }
    $writer.Write($widthByte)
    $writer.Write($heightByte)
    $writer.Write([byte]0)
    $writer.Write([byte]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]32)
    $writer.Write([uint32]$bytes.Length)
    $writer.Write([uint32]$offset)
    $offset += $bytes.Length
}

foreach ($bytes in $frames) {
    $writer.Write($bytes)
}

$writer.Flush()
[System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($Ico)) | Out-Null
[System.IO.File]::WriteAllBytes($Ico, $stream.ToArray())
$writer.Dispose()
$stream.Dispose()
Write-Host "Wrote $Ico"
