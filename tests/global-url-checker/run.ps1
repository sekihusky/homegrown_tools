param([switch]$Live)
$ErrorActionPreference = 'Stop'
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Push-Location $root
try {
    New-Item -ItemType Directory -Force -Path '.build/global-url-checker' | Out-Null
    $compiler = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
    $output = Join-Path $root '.build\global-url-checker\CheckerTests.exe'
    & $compiler /nologo /target:exe /main:CheckerTests `
        /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll `
        /reference:System.Net.Http.dll /reference:System.Web.Extensions.dll `
        "/out:$output" (Join-Path $root 'GlobalUrlChecker.cs') (Join-Path $PSScriptRoot 'CheckerTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
    if ($Live) { & $output --live } else { & $output }
    if ($LASTEXITCODE -ne 0) {
        Get-Content '.build/global-url-checker/test-error.txt' -ErrorAction SilentlyContinue
        throw 'Tests failed.'
    }
} finally { Pop-Location }
