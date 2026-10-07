$ErrorActionPreference = 'Stop'
$cache = Join-Path $PSScriptRoot 'packages\video-compressor'
$output = Join-Path $PSScriptRoot 'release\VideoCompressor'
New-Item -ItemType Directory -Force $cache,$output | Out-Null
$zip = Join-Path $cache 'ffmpeg.zip'
if (!(Test-Path $zip)) {
    Invoke-WebRequest 'https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip' -OutFile $zip
    Invoke-WebRequest 'https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip.sha256' -OutFile "$zip.sha256"
}
$expected = ((Get-Content "$zip.sha256" -Raw).Trim() -split '\s+')[0]
$actual = (Get-FileHash $zip -Algorithm SHA256).Hash
if ($actual -ne $expected) { throw 'FFmpeg SHA256 mismatch.' }
$unpacked = Join-Path $cache ('unpacked-' + $actual.Substring(0,16))
if (!(Test-Path $unpacked)) { Expand-Archive -LiteralPath $zip -DestinationPath $unpacked }
$bundle = Get-ChildItem $unpacked -Directory | Select-Object -First 1
Copy-Item (Join-Path $bundle.FullName 'bin\ffmpeg.exe'),(Join-Path $bundle.FullName 'bin\ffprobe.exe') -Destination $output -Force
Copy-Item (Join-Path $bundle.FullName 'LICENSE') -Destination (Join-Path $output 'FFmpeg-LICENSE.txt') -Force
Copy-Item (Join-Path $bundle.FullName 'README.txt') -Destination (Join-Path $output 'FFmpeg-README.txt') -Force
Copy-Item "$zip.sha256" (Join-Path $output 'FFmpeg-download.sha256') -Force
& (Join-Path $PSScriptRoot 'build-video-compressor-icon.ps1')
$compiler = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
& $compiler /nologo /target:winexe /optimize+ /platform:anycpu /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll "/win32icon:$(Join-Path $PSScriptRoot 'assets\icons\video-compressor.ico')" "/out:$(Join-Path $output 'VideoCompressor.exe')" (Join-Path $PSScriptRoot 'VideoCompressor.cs')
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
Copy-Item (Join-Path $PSScriptRoot 'docs\video-compressor\README.md') (Join-Path $output '使用說明.txt') -Force
Write-Host "Built: $output"
