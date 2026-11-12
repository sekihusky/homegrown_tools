$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$directory = Join-Path $PSScriptRoot 'assets\icons'
New-Item -ItemType Directory -Force -Path $directory | Out-Null
$bitmap = New-Object System.Drawing.Bitmap(256,256)
$g = [System.Drawing.Graphics]::FromImage($bitmap)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([System.Drawing.Color]::Transparent)
$blue = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(37,99,235))
$white = [System.Drawing.Brushes]::White
$g.FillEllipse($blue,8,8,240,240)
$g.FillRectangle($white,48,78,160,98)
$g.FillEllipse($white,48,146,160,38)
$g.FillRectangle($blue,65,95,126,18)
$green = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(34,197,94),19)
$g.DrawLines($green,[System.Drawing.Point[]]@([System.Drawing.Point]::new(134,166),[System.Drawing.Point]::new(159,191),[System.Drawing.Point]::new(211,133)))
$png = Join-Path $directory 'disk-cleanup-helper.png'
$bitmap.Save($png,[System.Drawing.Imaging.ImageFormat]::Png)
$data = [System.IO.File]::ReadAllBytes($png)
$writer = [System.IO.BinaryWriter]::new([System.IO.File]::Create((Join-Path $directory 'disk-cleanup-helper.ico')))
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]1)
    $writer.Write([byte]0); $writer.Write([byte]0); $writer.Write([byte]0); $writer.Write([byte]0)
    $writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$data.Length); $writer.Write([uint32]22); $writer.Write($data)
} finally { $writer.Dispose(); $green.Dispose(); $blue.Dispose(); $g.Dispose(); $bitmap.Dispose() }
