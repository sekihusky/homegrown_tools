$ErrorActionPreference = 'Stop'
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$tools = Join-Path $root 'release\VideoCompressor'
$work = Join-Path $root ('.build\video-compressor-test-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $work | Out-Null
& (Join-Path $tools 'ffmpeg.exe') -hide_banner -loglevel error -n -f lavfi -i 'testsrc2=size=320x180:rate=24' -f lavfi -i 'sine=frequency=440:sample_rate=48000' -t 18 -c:v libx264 -crf 10 -c:a aac (Join-Path $work '來源 測試.mp4')
if ($LASTEXITCODE -ne 0) { throw 'Fixture failed' }
& (Join-Path $tools 'ffmpeg.exe') -hide_banner -loglevel error -n -i (Join-Path $work '來源 測試.mp4') -t 1 -an -c:v libx265 -pix_fmt yuv420p10le -color_primaries bt2020 -colorspace bt2020nc -color_trc smpte2084 -x265-params colorprim=bt2020:transfer=smpte2084:colormatrix=bt2020nc (Join-Path $work 'hdr.mp4')
if ($LASTEXITCODE -ne 0) { throw 'HDR fixture failed' }
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll "/reference:$(Join-Path $tools 'VideoCompressor.exe')" "/out:$(Join-Path $work 'Smoke.exe')" (Join-Path $PSScriptRoot 'Smoke.cs')
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed' }
Copy-Item (Join-Path $tools 'VideoCompressor.exe') $work
& (Join-Path $work 'Smoke.exe') $tools $work
if ($LASTEXITCODE -ne 0) { throw 'Smoke tests failed' }
foreach ($name in @('h264.mp4','h265.mp4')) {
    & (Join-Path $tools 'ffmpeg.exe') -hide_banner -v error -xerror -i (Join-Path $work $name) -f null -
    if ($LASTEXITCODE -ne 0) { throw "Decode validation failed: $name" }
}
Write-Host "Decode validation passed. Test artifacts: $work"
