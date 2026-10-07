$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$directory = Join-Path $PSScriptRoot 'assets\icons'
New-Item -ItemType Directory -Force -Path $directory | Out-Null
$bitmap = New-Object System.Drawing.Bitmap(256,256)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.Clear([System.Drawing.Color]::Transparent)
$dark = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(28,55,86))
$light = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(73,216,169))
$white = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
$graphics.FillEllipse($dark,8,8,240,240)
$graphics.FillRectangle($light,49,62,158,125)
$graphics.FillRectangle($dark,61,76,134,98)
$graphics.FillPolygon($white,[System.Drawing.Point[]]@((New-Object System.Drawing.Point(108,93)),(New-Object System.Drawing.Point(108,154)),(New-Object System.Drawing.Point(157,124))))
$pen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(73,216,169),10)
$graphics.DrawLine($pen,64,212,105,212)
$graphics.DrawLine($pen,105,212,90,198)
$graphics.DrawLine($pen,105,212,90,226)
$graphics.DrawLine($pen,192,212,151,212)
$graphics.DrawLine($pen,151,212,166,198)
$graphics.DrawLine($pen,151,212,166,226)
$bitmap.Save((Join-Path $directory 'video-compressor.png'),[System.Drawing.Imaging.ImageFormat]::Png)
$bytes = [System.IO.File]::ReadAllBytes((Join-Path $directory 'video-compressor.png'))
$writer = New-Object System.IO.BinaryWriter([System.IO.File]::Create((Join-Path $directory 'video-compressor.ico')))
$writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]1)
$writer.Write([byte]0); $writer.Write([byte]0); $writer.Write([byte]0); $writer.Write([byte]0)
$writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$bytes.Length); $writer.Write([uint32]22); $writer.Write($bytes)
$writer.Dispose(); $pen.Dispose(); $white.Dispose(); $light.Dispose(); $dark.Dispose(); $graphics.Dispose(); $bitmap.Dispose()
