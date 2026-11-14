$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$iconPath = Join-Path $PSScriptRoot 'assets\icons\api-tester.ico'
$pngPath = Join-Path $PSScriptRoot 'assets\icons\api-tester.png'
$bitmap = New-Object System.Drawing.Bitmap 64,64
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = 'AntiAlias'
$graphics.Clear([System.Drawing.Color]::FromArgb(24,40,65))
$pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(70,205,233)),5
$graphics.DrawLines($pen, [System.Drawing.Point[]]@([System.Drawing.Point]::new(22,18),[System.Drawing.Point]::new(9,32),[System.Drawing.Point]::new(22,46)))
$graphics.DrawLines($pen, [System.Drawing.Point[]]@([System.Drawing.Point]::new(42,18),[System.Drawing.Point]::new(55,32),[System.Drawing.Point]::new(42,46)))
$pen.Color = [System.Drawing.Color]::FromArgb(255,173,80)
$graphics.DrawLine($pen,36,16,28,48)
$bitmap.Save($pngPath, [System.Drawing.Imaging.ImageFormat]::Png)
# Embed the PNG in an ICO container, without native icon handles.
$png = [System.IO.File]::ReadAllBytes($pngPath)
$stream = [System.IO.File]::Create($iconPath)
$writer = New-Object System.IO.BinaryWriter $stream
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]1)
    $writer.Write([byte]64); $writer.Write([byte]64); $writer.Write([byte]0); $writer.Write([byte]0)
    $writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$png.Length); $writer.Write([uint32]22); $writer.Write($png)
} finally { $writer.Dispose(); $graphics.Dispose(); $pen.Dispose(); $bitmap.Dispose() }
$compiler = @("$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe", "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe") | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $compiler) { throw 'Windows .NET Framework C# compiler was not found.' }
$output = Join-Path $PSScriptRoot 'release\ApiTester.exe'
New-Item -ItemType Directory -Force -Path (Split-Path $output) | Out-Null
& $compiler /nologo /target:winexe /optimize+ /platform:anycpu /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Net.Http.dll /reference:System.Web.Extensions.dll /reference:System.Security.dll "/win32icon:$iconPath" "/out:$output" (Join-Path $PSScriptRoot 'ApiTester.cs')
if ($LASTEXITCODE -ne 0) { throw "Build failed: $LASTEXITCODE" }
Write-Host "Build complete: $output"
