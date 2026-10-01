$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$directory = Join-Path $PSScriptRoot 'assets\icons'
New-Item -ItemType Directory -Force -Path $directory | Out-Null
$bitmap = New-Object System.Drawing.Bitmap(256, 256)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.Clear([System.Drawing.Color]::Transparent)
$brush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(20, 67, 103))
$pen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(104, 212, 229), 7)
$graphics.FillEllipse($brush, 14, 14, 228, 228)
$graphics.DrawEllipse($pen, 30, 30, 196, 196)
$graphics.DrawEllipse($pen, 78, 30, 100, 196)
$graphics.DrawLine($pen, 30, 128, 226, 128)
$graphics.DrawArc($pen, 30, 70, 196, 55, 0, 180)
$graphics.DrawArc($pen, 30, 130, 196, 55, 180, 180)
$pulse = New-Object System.Drawing.Pen([System.Drawing.Color]::White, 11)
$graphics.DrawLines($pulse, [System.Drawing.Point[]]@((New-Object System.Drawing.Point(42, 152)), (New-Object System.Drawing.Point(86, 152)), (New-Object System.Drawing.Point(107, 110)), (New-Object System.Drawing.Point(134, 182)), (New-Object System.Drawing.Point(155, 141)), (New-Object System.Drawing.Point(213, 141))))
$pngPath = Join-Path $directory 'global-url-checker.png'
$bitmap.Save($pngPath, [System.Drawing.Imaging.ImageFormat]::Png)
$bytes = [System.IO.File]::ReadAllBytes($pngPath)
$writer = New-Object System.IO.BinaryWriter([System.IO.File]::Create((Join-Path $directory 'global-url-checker.ico')))
$writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]1)
$writer.Write([byte]0); $writer.Write([byte]0); $writer.Write([byte]0); $writer.Write([byte]0)
$writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$bytes.Length); $writer.Write([uint32]22); $writer.Write($bytes)
$writer.Dispose(); $pulse.Dispose(); $pen.Dispose(); $brush.Dispose(); $graphics.Dispose(); $bitmap.Dispose()
