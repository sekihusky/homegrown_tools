$ErrorActionPreference = 'Stop'
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$directory = Join-Path $root '.build/network-helper'
New-Item -ItemType Directory -Force -Path $directory | Out-Null
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$exe = Join-Path $directory 'Smoke.exe'
& $compiler /nologo /target:exe /main:Smoke /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll "/out:$exe" (Join-Path $root 'NetworkHelper.cs') (Join-Path $PSScriptRoot 'Smoke.cs')
if ($LASTEXITCODE -ne 0) { throw 'Smoke build failed' }
& $exe (Join-Path $directory 'window.png') (Join-Path $directory 'window-small.png')
if ($LASTEXITCODE -ne 0) { throw 'Smoke checks failed' }
