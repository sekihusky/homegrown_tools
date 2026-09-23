$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$iconDirectory = Join-Path $PSScriptRoot 'assets\icons'
New-Item -ItemType Directory -Force -Path $iconDirectory | Out-Null
$bitmap = New-Object System.Drawing.Bitmap(64, 64)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = 'AntiAlias'
$graphics.Clear([System.Drawing.Color]::Transparent)
$background = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(28, 48, 75))
$accent = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(83, 223, 184), 5)
$graphics.FillRectangle($background, 4, 8, 56, 48)
$graphics.DrawLine($accent, 16, 23, 26, 32)
$graphics.DrawLine($accent, 26, 32, 16, 41)
$graphics.DrawLine($accent, 33, 41, 48, 41)
$pngPath = Join-Path $iconDirectory 'ssh-manager.png'
$bitmap.Save($pngPath, [System.Drawing.Imaging.ImageFormat]::Png)
$pngBytes = [System.IO.File]::ReadAllBytes($pngPath)
$stream = [System.IO.File]::Create((Join-Path $iconDirectory 'ssh-manager.ico'))
$writer = New-Object System.IO.BinaryWriter($stream)
$writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]1)
$writer.Write([byte]64); $writer.Write([byte]64); $writer.Write([byte]0); $writer.Write([byte]0)
$writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$pngBytes.Length); $writer.Write([uint32]22)
$writer.Write($pngBytes)
$writer.Dispose(); $graphics.Dispose(); $background.Dispose(); $accent.Dispose(); $bitmap.Dispose()
