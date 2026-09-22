$ErrorActionPreference = 'Stop'

$compilerCandidates = @(
    "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe",
    "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe"
)
$compiler = $compilerCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $compiler) { throw 'Windows .NET Framework C# compiler was not found.' }

$outputDirectory = Join-Path $PSScriptRoot 'release'
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
$outputFile = Join-Path $outputDirectory 'LanDeviceScanner.exe'

& $compiler /nologo /target:winexe /optimize+ /platform:anycpu `
    /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll `
    "/win32icon:$(Join-Path $PSScriptRoot 'assets\icons\lan-device-scanner.ico')" `
    "/out:$outputFile" (Join-Path $PSScriptRoot 'LanDeviceScanner.cs')

if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code: $LASTEXITCODE" }
Write-Host "Build complete: $outputFile" -ForegroundColor Green
