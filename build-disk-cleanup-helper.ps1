$ErrorActionPreference = 'Stop'
$compiler = @("$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe", "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe") | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $compiler) { throw 'Windows .NET Framework C# compiler was not found.' }
& (Join-Path $PSScriptRoot 'build-disk-cleanup-helper-icon.ps1')
New-Item -ItemType Directory -Force -Path (Join-Path $PSScriptRoot 'release') | Out-Null
& $compiler /nologo /target:winexe /optimize+ /platform:anycpu /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll "/win32icon:$(Join-Path $PSScriptRoot 'assets\icons\disk-cleanup-helper.ico')" "/out:$(Join-Path $PSScriptRoot 'release\DiskCleanupHelper.exe')" (Join-Path $PSScriptRoot 'DiskCleanupHelper.cs')
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Write-Host 'Build complete: release/DiskCleanupHelper.exe' -ForegroundColor Green
