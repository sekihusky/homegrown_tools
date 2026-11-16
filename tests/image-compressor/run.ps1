$ErrorActionPreference = 'Stop'
$root = Split-Path (Split-Path $PSScriptRoot)
$bin = Join-Path $root '.build\image-compressor-tests'
New-Item -ItemType Directory -Force $bin | Out-Null
Copy-Item "$root\release\ImageCompressor\*.dll" $bin
$facade = Get-ChildItem "$env:WINDIR\Microsoft.NET\assembly\GAC_MSIL\netstandard" -Filter netstandard.dll -Recurse | Select-Object -First 1 -ExpandProperty FullName
$compiler = Join-Path $root 'packages\image-compressor\compiler\tasks\net472\csc.exe'
& $compiler /nologo /target:exe /platform:x64 /main:ImageCompressionSmoke /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll "/reference:$facade" "/reference:$bin\Magick.NET.Core.dll" "/reference:$bin\Magick.NET-Q8-x64.dll" "/out:$bin\Smoke.exe" "$root\ImageCompressor.cs" "$PSScriptRoot\Smoke.cs"
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed' }
& "$bin\Smoke.exe" $bin
if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
