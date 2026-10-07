$ErrorActionPreference = 'Stop'
$compiler = @("$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe", "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe") | ? { Test-Path $_ } | select -First 1
if (-not $compiler) { throw 'Windows .NET Framework C# compiler was not found.' }
$out = Join-Path $PSScriptRoot 'release\DevToolbox.exe'
& $compiler /nologo /target:winexe /optimize+ /platform:anycpu /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll "/out:$out" (Join-Path $PSScriptRoot 'DevToolbox.cs')
if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code: $LASTEXITCODE" }
Write-Host "Build complete: $out" -ForegroundColor Green
