$ErrorActionPreference = 'Stop'
$compiler = @("$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe", "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe") | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $compiler) { throw 'Windows .NET Framework C# compiler was not found.' }
& (Join-Path $PSScriptRoot 'build-network-helper-icon.ps1')
$outputDirectory = Join-Path $PSScriptRoot 'release'
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
& $compiler /nologo /target:winexe /optimize+ /platform:anycpu /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll "/win32icon:$(Join-Path $PSScriptRoot 'assets/icons/network-helper.ico')" "/win32manifest:$(Join-Path $PSScriptRoot 'NetworkHelper.manifest')" "/out:$(Join-Path $outputDirectory 'NetworkHelper.exe')" (Join-Path $PSScriptRoot 'NetworkHelper.cs')
if ($LASTEXITCODE -ne 0) { throw "Build failed: $LASTEXITCODE" }
Write-Host 'Build complete: release/NetworkHelper.exe'
