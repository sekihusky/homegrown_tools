$ErrorActionPreference = 'Stop'
$output = Join-Path $PSScriptRoot 'release\VideoTrimmer'
$source = Join-Path $PSScriptRoot 'release\VideoCompressor'
if (!(Test-Path (Join-Path $source 'ffmpeg.exe'))) {
    & (Join-Path $PSScriptRoot 'build-video-compressor.ps1')
}
New-Item -ItemType Directory -Force $output | Out-Null
Copy-Item (Join-Path $source 'ffmpeg.exe'), (Join-Path $source 'FFmpeg-LICENSE.txt'), (Join-Path $source 'FFmpeg-README.txt') -Destination $output -Force
$compiler = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
& $compiler /nologo /target:winexe /optimize+ /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:Microsoft.CSharp.dll "/win32icon:$(Join-Path $PSScriptRoot 'assets\icons\video-compressor.ico')" "/out:$(Join-Path $output 'VideoTrimmer.exe')" (Join-Path $PSScriptRoot 'VideoTrimmer.cs')
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
Copy-Item (Join-Path $PSScriptRoot 'docs\video-trimmer\README.md') (Join-Path $output 'README.txt') -Force
Write-Host "Built: $output"
