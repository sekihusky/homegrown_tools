$ErrorActionPreference = 'Stop'
$version = '8.23.0_4'
$hash = '47bbc89aa5bd40279aa83a7e1988b0148487ba802032670fce3890941ab8f3d2'
$archive = Join-Path $PSScriptRoot ".build/curl-$version-win64-mingw.zip"
$package = Join-Path $PSScriptRoot 'packages/http-protocol-curl'
$output = Join-Path $PSScriptRoot 'release/HttpProtocolChecker'
New-Item -ItemType Directory -Force -Path (Split-Path $archive), $package, $output | Out-Null
if (-not (Test-Path $archive)) {
    & curl.exe --fail --location --silent --show-error "https://curl.se/windows/dl-$version/curl-$version-win64-mingw.zip" -o $archive
    if ($LASTEXITCODE -ne 0) { throw 'curl download failed' }
}
if ((Get-FileHash $archive -Algorithm SHA256).Hash -ne $hash) { throw 'curl SHA256 mismatch' }
Expand-Archive -LiteralPath $archive -DestinationPath $package -Force
$source = Join-Path $package "curl-$version-win64-mingw"
Copy-Item "$source/bin/curl.exe", "$source/bin/libcurl-x64.dll", "$source/COPYING.txt", "$source/BUILD-MANIFEST.txt" -Destination $output -Force
Copy-Item "$source/dep" -Destination $output -Recurse -Force

Add-Type -AssemblyName System.Drawing
$icons = Join-Path $PSScriptRoot 'assets/icons'
New-Item -ItemType Directory -Force -Path $icons | Out-Null
$bitmap = New-Object System.Drawing.Bitmap(256,256)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = 'AntiAlias'
$graphics.Clear([System.Drawing.Color]::FromArgb(20,35,62))
$font = New-Object System.Drawing.Font('Segoe UI',76,[System.Drawing.FontStyle]::Bold)
$graphics.DrawString('H',[System.Drawing.Font]$font,[System.Drawing.Brushes]::White,55,8)
$small = New-Object System.Drawing.Font('Segoe UI',32,[System.Drawing.FontStyle]::Bold)
$graphics.DrawString('1 2 3',$small,[System.Drawing.Brushes]::Turquoise,35,160)
$png = Join-Path $icons 'http-protocol-checker.png'
$bitmap.Save($png,[System.Drawing.Imaging.ImageFormat]::Png)
$bytes = [IO.File]::ReadAllBytes($png)
$icon = Join-Path $icons 'http-protocol-checker.ico'
$writer = New-Object IO.BinaryWriter([IO.File]::Create($icon))
$writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]1)
$writer.Write([byte]0); $writer.Write([byte]0); $writer.Write([byte]0); $writer.Write([byte]0)
$writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$bytes.Length); $writer.Write([uint32]22); $writer.Write($bytes)
$writer.Dispose(); $font.Dispose(); $small.Dispose(); $graphics.Dispose(); $bitmap.Dispose()
$compiler = "$env:WINDIR/Microsoft.NET/Framework64/v4.0.30319/csc.exe"
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll "/win32icon:$icon" "/out:$(Join-Path $output 'HttpProtocolChecker.exe')" (Join-Path $PSScriptRoot 'HttpProtocolChecker.cs')
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
Copy-Item "$PSScriptRoot/docs/http-protocol-checker/README.md" "$output/README.md" -Force
Write-Host "Build complete: $output/HttpProtocolChecker.exe"
