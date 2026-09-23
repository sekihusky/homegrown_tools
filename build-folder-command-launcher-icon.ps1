$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$iconDirectory = Join-Path $PSScriptRoot 'assets\icons'
$sourcePath = Join-Path $iconDirectory 'folder-command-launcher-source.png'
$pngPath = Join-Path $iconDirectory 'folder-command-launcher.png'
$icoPath = Join-Path $iconDirectory 'folder-command-launcher.ico'
if (-not (Test-Path -LiteralPath $sourcePath)) {
    throw "Icon source image was not found: $sourcePath"
}

$source = [System.Drawing.Image]::FromFile($sourcePath)
$bitmap = New-Object System.Drawing.Bitmap(256, 256)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
$graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
$graphics.DrawImage($source, 0, 0, 256, 256)
$bitmap.Save($pngPath, [System.Drawing.Imaging.ImageFormat]::Png)

# ICO files may embed a PNG image directly.  This preserves the generated image's alpha channel.
$pngBytes = [System.IO.File]::ReadAllBytes($pngPath)
$stream = [System.IO.File]::Create($icoPath)
$writer = New-Object System.IO.BinaryWriter($stream)
$writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]1)
$writer.Write([byte]0); $writer.Write([byte]0); $writer.Write([byte]0); $writer.Write([byte]0)
$writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$pngBytes.Length); $writer.Write([uint32]22)
$writer.Write($pngBytes)
$writer.Dispose(); $graphics.Dispose(); $bitmap.Dispose(); $source.Dispose()
Write-Host "Icon complete: $icoPath" -ForegroundColor Green
