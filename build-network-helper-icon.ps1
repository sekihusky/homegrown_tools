$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$directory = Join-Path $PSScriptRoot 'assets/icons'
New-Item -ItemType Directory -Force -Path $directory | Out-Null
$bitmap = New-Object System.Drawing.Bitmap(256,256)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.Clear([System.Drawing.Color]::FromArgb(24,45,72))
$pen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(65,215,184),16)
$graphics.DrawLine($pen,64,180,128,72)
$graphics.DrawLine($pen,128,72,192,180)
$graphics.DrawLine($pen,64,180,192,180)
foreach ($point in @(@(128,64),@(60,184),@(196,184))) { $graphics.FillEllipse([System.Drawing.Brushes]::White,($point[0]-23),($point[1]-23),46,46) }
$png = Join-Path $directory 'network-helper.png'
$bitmap.Save($png,[System.Drawing.Imaging.ImageFormat]::Png)
$bytes = [System.IO.File]::ReadAllBytes($png)
$writer = New-Object System.IO.BinaryWriter([System.IO.File]::Create((Join-Path $directory 'network-helper.ico')))
$writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]1)
$writer.Write([byte]0); $writer.Write([byte]0); $writer.Write([byte]0); $writer.Write([byte]0)
$writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$bytes.Length); $writer.Write([uint32]22); $writer.Write($bytes)
$writer.Dispose(); $pen.Dispose(); $graphics.Dispose(); $bitmap.Dispose()
