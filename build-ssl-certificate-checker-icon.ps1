$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$directory = Join-Path $PSScriptRoot 'assets/icons'
New-Item -ItemType Directory -Force -Path $directory | Out-Null
$bitmap = New-Object System.Drawing.Bitmap(256,256)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.Clear([System.Drawing.Color]::FromArgb(24,45,72))
$pen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(65,215,184),16)
$graphics.DrawArc($pen,80,40,96,115,180,180)
$graphics.FillRectangle([System.Drawing.Brushes]::White,55,105,146,115)
$graphics.FillEllipse([System.Drawing.Brushes]::Teal,113,138,30,30)
$graphics.FillRectangle([System.Drawing.Brushes]::Teal,122,155,12,34)
$png = Join-Path $directory 'ssl-certificate-checker.png'
$bitmap.Save($png,[System.Drawing.Imaging.ImageFormat]::Png)
$bytes = [System.IO.File]::ReadAllBytes($png)
$writer = New-Object System.IO.BinaryWriter([System.IO.File]::Create((Join-Path $directory 'ssl-certificate-checker.ico')))
$writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]1)
$writer.Write([byte]0); $writer.Write([byte]0); $writer.Write([byte]0); $writer.Write([byte]0)
$writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$bytes.Length); $writer.Write([uint32]22); $writer.Write($bytes)
$writer.Dispose(); $pen.Dispose(); $graphics.Dispose(); $bitmap.Dispose()

